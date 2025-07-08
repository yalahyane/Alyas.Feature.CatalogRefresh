using Sitecore.DependencyInjection;
using Sitecore.Jobs;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using System;
using Alyas.Feature.CatalogRefresh.Models;
using Alyas.Feature.CatalogRefresh.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Alyas.Feature.CatalogRefresh.Controllers.Api
{
    public class CatalogRefreshApiController : ApiController
    {
        private readonly ICatalogRefreshService _catalogRefreshService = ServiceLocator.ServiceProvider.GetService<ICatalogRefreshService>();

        [HttpGet]
        public async Task<HttpResponseMessage> GetCatalogRefreshHistory()
        {
            var response = await this._catalogRefreshService.GetMostRecentRefreshRecords();
            return Request.CreateResponse(HttpStatusCode.OK, response);
        }

        [HttpPost]
        public async Task<IHttpActionResult> CatalogRefresh([FromBody] CatalogRefreshRequest request)
        {
            var model = new CatalogRefreshModel
            {
                Source = request.Source
            };

            foreach (var destination in request.Destinations)
            {
                var recordId = await _catalogRefreshService.CreateProcessingRecords(request.Source, destination);
                model.Destinations.Add(new KeyValuePair<int, string>(recordId, destination));
            }
            var options = new DefaultJobOptions(
                "Catalog Refresh",
                "catalog-refresh",
                Sitecore.Context.Site.Name,
                this,
                nameof(RunRefreshJob)
            )
            {
                CustomData = model,
                AfterLife = TimeSpan.FromHours(2)
            };
            JobManager.Start(options);

            var history = await _catalogRefreshService
                .GetMostRecentRefreshRecords()
                .ConfigureAwait(false);

            return Ok(history);
        }

        public void RunRefreshJob()
        {
            var model = (CatalogRefreshModel)Sitecore.Context.Job.Options.CustomData;
            _catalogRefreshService
                .RefreshCatalog(model.Source, model.Destinations)
                .GetAwaiter()
                .GetResult();
        }
    }
}