using Alyas.Feature.CatalogRefresh.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Alyas.Feature.CatalogRefresh.Services
{
    public interface ICatalogRefreshService
    {
        Task<List<CatalogRefreshRecord>> GetMostRecentRefreshRecords();
        Task<int> CreateProcessingRecords(string source, string destination);
        Task<List<CatalogRefreshRecord>> RefreshCatalog(string source, List<KeyValuePair<int, string>> destinations);
    }
}
