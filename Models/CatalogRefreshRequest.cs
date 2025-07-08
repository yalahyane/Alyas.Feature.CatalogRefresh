using System.Collections.Generic;

namespace Alyas.Feature.CatalogRefresh.Models
{
    public class CatalogRefreshRequest
    {
        public string Source { get; set; }
        public List<string> Destinations { get; set; }
    }
}