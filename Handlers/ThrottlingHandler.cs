using DCI.SystemEvents.Extensions;
using DCI.SystemEvents.Settings;
using Microsoft.Azure.Cosmos;
using Polly;
using Serilog;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DCI.SystemEvents.Handlers
{


    /// <summary>
    /// Using Polly to retry on Throttles.
    /// </summary>
    class ThrottlingHandler : RequestHandler
    {
        private readonly ThrottlingHandlerSettings settings;
        private readonly Random random = new Random();
        public ThrottlingHandler(ThrottlingHandlerSettings settings = default)
        {
            this.settings = settings ?? new ThrottlingHandlerSettings();
        }
        public override Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            return Policy
                .Handle<CosmosException>((e) => (int)e.StatusCode == 429)
                .Or<HttpRequestException>()
                .Or<WebException>()
                .OrResult<ResponseMessage>(e => !e.IsSuccessStatusCode)
                .WaitAndRetryAsync(settings.MaximumExponentialRetries, (retryAttempt) =>
                {
                    if (retryAttempt <= settings.MaximumExponentialRetries)
                    {
                        /* offset differential */
                        var ms = (random.Next(0,2) *2 -1) // +/-
                                 * random.Next(Math.Min(settings.RandomizedMinThresholdInMilliseconds,
                                    settings.RandomizedMaxThresholdInMilliseconds),
                                    Math.Max(settings.RandomizedMinThresholdInMilliseconds,
                                    settings.RandomizedMaxThresholdInMilliseconds)); 
                        Log.Warning($"CosmosDB nerfed us this is the {retryAttempt.Ordinal()} attempt.");
                        return TimeSpan.FromMilliseconds(Math.Abs(
                            Math.Pow(settings.ExponentialRetryInMilliseconds, retryAttempt) 
                            + ms)); 
                    }
                    /* After you've exhausted retry efforts throw exception */
                    throw new TransientException($"Failed to persist to Cosmos after {retryAttempt.Ordinal()} attempts");
                }).ExecuteAsync(() => base.SendAsync(request, cancellationToken));
        }
    }
}
