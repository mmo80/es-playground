using Marten;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using static System.Formats.Asn1.AsnWriter;

namespace WebApi.Health
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly IDocumentStore _store;

        public DatabaseHealthCheck(IDocumentStore store)
        {
            _store = store;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, 
            CancellationToken cancellationToken = new())
        {
            try
            {
                await using var session = _store.QuerySession();
                await session.QueryAsync<int>("select 1", cancellationToken);
                return HealthCheckResult.Healthy();
            }
            catch (Exception e)
            {
                return HealthCheckResult.Unhealthy(exception: e);
            }
        }
    }
}
