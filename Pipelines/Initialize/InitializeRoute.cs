using System.Web.Http;
using System.Web.Routing;
using Sitecore.Pipelines;
using Sitecore.XA.Foundation.SitecoreExtensions.Session;

namespace Alyas.Feature.CatalogRefresh.Pipelines.Initialize
{
    public class InitializeRoute
    {
        public void Process(PipelineArgs args)
        {
            RouteTable.Routes.MapHttpRoute("AlyasInternalApiRoute", "api/alyas/{controller}/{action}").RouteHandler = new SessionHttpControllerRouteHandler();
        }
    }
}