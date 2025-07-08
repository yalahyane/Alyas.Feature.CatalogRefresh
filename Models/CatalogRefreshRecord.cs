using System;

namespace Alyas.Feature.CatalogRefresh.Models
{
    public class CatalogRefreshRecord
    {
        public string Status { get; set; }
        public string Source { get; set; }
        public string Destination { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string ErrorMessage { get; set; }
    }
}