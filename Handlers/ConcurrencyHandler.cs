using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace DCI.SystemEvents.Handlers
{
    class ConcurrencyHandler : RequestHandler
    {
        // Lifted directly from Cosmos documentation
        // https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/Microsoft.Azure.Cosmos.Samples/Usage/Handlers/ConcurrencyHandler.cs

        public override async Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {

            ResponseMessage response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
            {
                response.Headers.Set("x-ms-substatus", "999");
            }
            return response;
        }
    }
}
