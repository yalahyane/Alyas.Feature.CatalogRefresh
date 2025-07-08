using System.Collections.Generic;

namespace Alyas.Feature.CatalogRefresh.Models
{
    public class CatalogRefreshModel
    {
        public string Source { get; set; }
        public List<KeyValuePair<int, string>> Destinations { get; set; } = new List<KeyValuePair<int, string>>();
    }
}