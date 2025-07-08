using Alyas.Feature.CatalogRefresh.Services;
using Microsoft.Extensions.DependencyInjection;
using Sitecore.DependencyInjection;

namespace Alyas.Feature.CatalogRefresh.ServiceConfigurators
{
    public class CatalogRefreshServiceConfigurator : IServicesConfigurator
    {
        public void Configure(IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<ICatalogRefreshService, CatalogRefreshService>();
        }
    }
}