using DCI.Core.Enums.DB;
using DCI.Core.ViewModels;
using DCI.Core.ViewModels.Data;
using DCI.Core.ViewModels.Data.EventsModel;
using DCI.Data.BAL;
using DCI.SystemEvents.Extensions;
using DCI.SystemEvents.Handlers;
using DCI.SystemEvents.Settings;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Cosmos.Linq;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Container = Microsoft.Azure.Cosmos.Container;
using Log = Serilog.Log;

namespace DCI.SystemEvents
{
    public class Events : IDisposable
    {
        private readonly string defaultDatabaseName = "Annotations";
        private readonly string defaultCollectionName = "EventsData";
        private static CosmosClient cosmosClient;
        private bool disposedValue;
        
        public Container CosmosContainer => cosmosClient?
            .GetDatabase(defaultDatabaseName)?
            .GetContainer(defaultCollectionName);

        public Events(string connectionString = "")
        {
            var settingsBuilder = new DbConnectionStringBuilder(false)
            {
                ConnectionString = !string.IsNullOrWhiteSpace(connectionString)
                    ? connectionString
                    : Core.Settings.Configurations.ConnectionString_DB_Events
            };
            // in the instance of the client already being established - maintain a record of settings (non-static instance)
            ThrottleSettings = ThrottlingSettingsFromConnection(settingsBuilder);

            if (cosmosClient == null)
            {
                Initialize(ThrottleSettings, settingsBuilder);
            }

        }

        private static void Initialize(ThrottlingHandlerSettings throttleSettings, DbConnectionStringBuilder settingsBuilder)
        {
            var builder = new CosmosClientBuilder(settingsBuilder.ConnectionString);
            
            builder.AddCustomHandlers(
                    new ThrottlingHandler(throttleSettings),
                    new ConcurrencyHandler())
                .WithBulkExecution(true)
                .WithConnectionModeDirect(
                    new TimeSpan(0, throttleSettings.MaxIdleTimeoutMinutes, 0),
                    new TimeSpan(0, 0, 0, throttleSettings.OpenTcpConnectionTimeoutSec),
                    throttleSettings.MaxRequestsPerTcpConnection,
                    throttleSettings.MaxTcpConnectionsPerEndpoint,
                    throttleSettings.PortReuseMode)
                .WithThrottlingRetryOptions(new TimeSpan(0, 0, throttleSettings.RandomizedMaxThresholdInMilliseconds / 1000), 25);
            cosmosClient = builder.Build();
            Log.Debug($"{throttleSettings.ToDebugStatement()}");
        }

        public ThrottlingHandlerSettings ThrottleSettings
        {
            get;
        }

        public async Task SaveEvents(modelEvents inputData)
        {
            try
            {
                ClsDCIEvent dciEvents = new ClsDCIEvent();
                await dciEvents.CreateDCIEvent(inputData);
            }
            catch (Exception ex)
            {
                Log.ForContext("ItemID", inputData.ItemId)
                    .ForContext("ItemType", inputData.ItemType)
                    .ForContext("ProfileID", inputData.ProfileId)
                    .Error(ex, "An error occurred when saving an event");

                throw;
            }
        }

        public async Task SaveMultipleEvents(List<modelEvents> inputDataList, CancellationToken cancellationToken = default)
        {
            if (inputDataList != null && inputDataList.Any())
            {
                var tasks = new List<Task>(inputDataList.Count);
                tasks.AddRange(inputDataList.Select(SaveEvents));
                await Task.WhenAny(Task.WhenAll(tasks), cancellationToken.AsTask());
            }
        }

        public async Task<EventsResponse> GetEventRecords(DateTime? fromDate,
            DateTime? toDate,
            int? itemType = default,
            int? itemId = default,
            string searchSubject = default,
            List<int> listProfileIds = null,
            int lastTotalCount = default,
            //string paginationToken = null,
            int maxRecordCount = 200)
            {

            try
            {


                ClsDCIEvent dciEvents = new ClsDCIEvent();
                DCIEventSearch searchCriteria = new DCIEventSearch();
                searchCriteria.fromDate = fromDate;
                searchCriteria.toDate = toDate;
                searchCriteria.itemType = itemType.Value;
                searchCriteria.itemId = itemId.Value;
                searchCriteria.subject = searchSubject;
                searchCriteria.offset = lastTotalCount;
                searchCriteria.rowsPerPage = maxRecordCount;


                var events = dciEvents.SearchDCIEvents(searchCriteria, out int totalRecords);
                EventsResponse response = new EventsResponse();
                response.TotalCount = totalRecords;

                if(listProfileIds == null)
                {
                    listProfileIds = new List<int>();

                }

                // As to reduce the work required to refactor the codebase to change EventResponse_Annotation to DCIEvent, I am simply converting this here.
                // Not my proudest moment
                List<EventsResponse_Annotations> eventResponses = new List<EventsResponse_Annotations>();
                foreach (DCIEvent e in events)
                {
                    //Limit the returned data to only the profiles passed in, if there were any
                    if (listProfileIds.Any() == false || listProfileIds.Contains(e.ProfileId))
                    {
                        EventsResponse_Annotations eventResponse = new EventsResponse_Annotations();
                        eventResponse.Subject = e.Subject;
                        eventResponse.FullName = e.AddedBy;
                        eventResponse.ProfileId = e.ProfileId;
                        eventResponse.AppVersion = !string.IsNullOrEmpty(e.AppVersion) ? e.AppVersion : string.Empty;
                        eventResponse.Created = e.Created;
                        eventResponse.OS = (e.OS.HasValue && e.OS.Value != 0
                            ? clsEnum.GetAnnotationOS(e.OS.Value)
                            : string.Empty);
                        eventResponse.OSVersion = !string.IsNullOrEmpty(e.OSVersion) ? e.OSVersion : string.Empty;
                        eventResponse.ItemId = e.ItemId;
                        eventResponse.Data = e.Data;
                        eventResponse.InputType = (e.InputType.HasValue ? clsEnum.GetInputMethodType(e.InputType.Value) : string.Empty);

                        eventResponses.Add(eventResponse);

                    }
                }

                response.listItems = eventResponses;

                return response;
            }
            catch (Exception ex)
            {

                Log.Error(ex, "An error occurred when attempting to search and process DCI events for display");

                throw;

            }
        }

        public async Task<List<ProfileEventReport>> GetProfileAuditingEventsRecords(DateTime? fromDate,
            DateTime? toDate,
            int? profileId,
            string searchSubject)
        {
            var response = new List<ProfileEventReport>();
            try
            {

                var groupKey = $"{(int)Core.Enums.DB.clsEnum.enumAnnotationItemType.Profile}-{profileId}";
                var qry = CosmosContainer
                    .GetItemLinqQueryable<ProfileEventReport>(false,
                        null,
                        new QueryRequestOptions
                        {
                            MaxItemCount = -1,
                            PartitionKey = new PartitionKey(groupKey)
                        })
                    .Where(x => x.GroupKey == groupKey)
                    .WhereIf(fromDate.HasValue, (e) => e.Created >= fromDate.Value)
                    .WhereIf(toDate.HasValue, (e) => e.Created <= toDate.Value)
                    .WhereIf(!string.IsNullOrWhiteSpace(searchSubject),
                        (e) => e.Subject.ToLower().Contains(searchSubject.ToLower()))
                    .OrderByDescending(e => e.Created);

                using (var feedIterator = qry.ToFeedIterator())
                {
                    while (feedIterator.HasMoreResults)
                    {
                        var currentResultSet = await feedIterator.ReadNextAsync();
                        Log.Debug($"Page take: {currentResultSet.Diagnostics.GetClientElapsedTime()} (t/x) - {currentResultSet.RequestCharge} rus");
                        response.AddRange(currentResultSet.Select(x => new ProfileEventReport()
                        {
                            Created = x.Created,
                            Data = x.Data,
                            ProfileId = x.ProfileId,
                            Subject = x.Subject,
                            FullName = string.Empty
                        }));
                    }
                }

            }
            catch (Exception ex)
            {
                Log.Error(ex, "Cosmos Profile Audit");
            }
            return response;
        }

        public async Task<List<LoginEventsReport>> GetLoginEventsRecords(DateTime? fromDate,
            DateTime? toDate,
            int? employeeId,
            int? clientId,
            string searchSubject,
            int? addedBy)
        {
            var response = new List<LoginEventsReport>();
            var subjects = new List<string>
            {
                "logged in",
                "logged out",
                "session expired"
            };
            const int itemType = (int)Core.Enums.DB.clsEnum.enumAnnotationItemType.Profile;

            var listItemIds = new List<string>()
                .AppendIf(employeeId.HasValue, $"{itemType}-{employeeId}")
                .AppendIf(clientId.HasValue, $"{itemType}-{clientId}");

            // some notes about the cosmos-linq provider (ToLowerInvariant and First is not available)
            var listIdFirst = listItemIds.Any() ? listItemIds.First() : "";
            try
            {

                var qry = CosmosContainer
                    .GetItemLinqQueryable<LoginEventsReport>(false,
                        null,
                        new QueryRequestOptions
                        {
                            MaxItemCount = -1 //<-- since the original method did not specify, using recommended dynamic setting
                        })
                    .WhereIf(string.IsNullOrWhiteSpace(searchSubject), e => string.Join("|", subjects).Contains(e.Subject.ToLower()))
                    .WhereIf(!string.IsNullOrWhiteSpace(searchSubject), e => e.Subject.ToLower().Contains(searchSubject.ToLower()))
                    .WhereIf(fromDate.HasValue, (e) => e.Created >= fromDate.Value)
                    .WhereIf(!listItemIds.Any(), e => e.GroupKey.Contains($"{itemType}-"))
                    .WhereIf(listItemIds.Count == 1, e => e.GroupKey == listIdFirst)
                    .WhereIf(listItemIds.Count > 1, e => listItemIds.Contains(e.GroupKey))
                    .WhereIf(toDate.HasValue, (e) => e.Created <= toDate.Value)
                    .OrderByDescending(e => e.Created);

                using (var feedIterator = qry.ToFeedIterator())
                {
                    while (feedIterator.HasMoreResults)
                    {
                        var currentResultSet = await feedIterator.ReadNextAsync();
                        Log.Debug($"Page take: {currentResultSet.Diagnostics.GetClientElapsedTime()} (t/x) - {currentResultSet.RequestCharge} rus");
                        response.AddRange(currentResultSet.Select(x => new LoginEventsReport()
                        {
                            Created = x.Created,
                            Subject = x.Subject,
                            ProfileId = x.ProfileId,
                            ClientName = string.Empty,
                            EmployeeName = string.Empty,
                            FullName = string.Empty,
                            Data = x.Data,
                        }));
                    }
                }

            }
            catch (Exception ex)
            {
                Log.Error(ex, "Cosmos Login Events");
            }
            return response;
        }

        private async Task<modelEvents> ScrubInputEvent(modelEvents modelEvent)
        {
            await Task.Run(() =>
            {
                modelEvent.Id = GetSequentialGuid().ToString();
                modelEvent.GroupKey = $"{modelEvent.ItemType}-{modelEvent.ItemId}";

                // Prod #2545 - Exception handling
                if (modelEvent.InputType == Convert.ToInt32(Core.Enums.DB.clsEnum.enumInputMethodType.Mobile_App))
                {
                    modelEvent.OS = modelEvent.OS == 0
                        ? null
                        : modelEvent.OS;
                    modelEvent.OSVersion = !string.IsNullOrWhiteSpace(modelEvent.OSVersion)
                        ? modelEvent.OSVersion.Trim()
                        : string.Empty;
                    modelEvent.AppVersion = !string.IsNullOrWhiteSpace(modelEvent.AppVersion)
                        ? modelEvent.AppVersion.Trim()
                        : string.Empty;
                }
                else if (modelEvent.InputType == Convert.ToInt32(Core.Enums.DB.clsEnum.enumInputMethodType.Web_Portal))
                {
                    modelEvent.OS = null;
                }
            });
            return modelEvent;
        }

        private static Guid GetSequentialGuid()
        {
            var guidBytes = Guid.NewGuid().ToByteArray();
            var counter = DateTime.UtcNow.Ticks;
            var counterBytes = BitConverter.GetBytes(Interlocked.Increment(ref counter));

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(counterBytes);
            }

            guidBytes[08] = counterBytes[1];
            guidBytes[09] = counterBytes[0];
            guidBytes[10] = counterBytes[7];
            guidBytes[11] = counterBytes[6];
            guidBytes[12] = counterBytes[5];
            guidBytes[13] = counterBytes[4];
            guidBytes[14] = counterBytes[3];
            guidBytes[15] = counterBytes[2];

            return new Guid(guidBytes);
        }

        private static ThrottlingHandlerSettings ThrottlingSettingsFromConnection(DbConnectionStringBuilder settings)
        {
            var throttle = new ThrottlingHandlerSettings();
            if (settings?.Keys != null)
            {
                foreach (var settingsKey in settings.Keys)
                {
                    if (typeof(ThrottlingHandlerSettings).HasProperty(settingsKey.ToString()))
                    {
                        if (throttle.TrySetProperty(settingsKey.ToString(), settings[settingsKey.ToString()]))
                        {
                            Log.Verbose($"Assigned value for {settingsKey} : {settings[settingsKey.ToString()]}");
                        }
                    }
                }
            }

            return throttle;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    // this should not be explicitly called - as there will be multiple instances using the same static process instance
                    // we can not reliably tell when or if all processing has completed - verified this with multiple tests running in same process - it has
                    // the potential for causing more harm than good - objectdisposedexception across instances  - trust the SDK
                    // this.CosmosContainer?.Database?.Client?.Dispose(); // don't explicitly force dispose if not doing internally already
                }
                disposedValue = true;

            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~Events()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
