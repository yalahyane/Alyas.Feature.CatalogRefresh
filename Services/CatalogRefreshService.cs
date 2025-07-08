using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Alyas.Feature.CatalogRefresh.Models;
using System.Text;
using OrderCloud.SDK;
using Sitecore.Diagnostics;

namespace Alyas.Feature.CatalogRefresh.Services
{
    public class CatalogRefreshService : ICatalogRefreshService
    {
        public async Task<List<CatalogRefreshRecord>> GetMostRecentRefreshRecords()
        {
            var refreshList = new List<CatalogRefreshRecord>();
            using (var sqlConnection =
                   new SqlConnection(ConfigurationManager.ConnectionStrings["AlyasCatalogRefreshDb"]?.ConnectionString ??
                                     string.Empty))
            {
                await sqlConnection.OpenAsync();
                using (var sqlCommand = new SqlCommand(GetMostRecentRefreshRecordsCommandText(), sqlConnection))
                {
                    sqlCommand.CommandType = CommandType.Text;
                    using (var reader = await sqlCommand.ExecuteReaderAsync())
                    {
                        if (reader.HasRows)
                        {
                            while (await reader.ReadAsync())
                            {
                                refreshList.Add(new CatalogRefreshRecord
                                {
                                    Status = reader.GetString(0),
                                    Source = reader.GetString(1),
                                    Destination = reader.GetString(2),
                                    StartTime = reader.GetDateTime(3),
                                    EndTime = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4),
                                    ErrorMessage = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                                });
                            }
                        }
                    }
                }
            }

            return refreshList;
        }

        public async Task<int> CreateProcessingRecords(string source, string destination)
        {
            return await CreateRefreshRecord(new CatalogRefreshRecord
            {
                Status = "Processing",
                Source = source,
                Destination = destination,
                StartTime = DateTime.UtcNow
            }).ConfigureAwait(false);
        }

        public async Task<List<CatalogRefreshRecord>> RefreshCatalog(string source, List<KeyValuePair<int, string>> destinations)
        {
            var tasks = destinations.Select(async dest =>
            {
                var recordId = dest.Key;
                var destination = dest.Value;
                try
                {
                    if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(destination))
                    {
                        await UpdateRefreshRecord(recordId, "Error", DateTime.UtcNow, "Source and Destination Environments are mandatory");
                        return;
                    }

                    var sourceUrl = Sitecore.Configuration.Settings.GetSetting($"CatalogRefresh.BaseUrl.{source.ToUpper()}");
                    var sourceClientId = Sitecore.Configuration.Settings.GetSetting($"CatalogRefresh.ClientId.{source.ToUpper()}");
                    var sourceClientSecret = Sitecore.Configuration.Settings.GetSetting($"CatalogRefresh.ClientSecret.{source.ToUpper()}");
                    var destinationUrl = Sitecore.Configuration.Settings.GetSetting($"CatalogRefresh.BaseUrl.{destination.ToUpper()}");
                    var destinationClientId = Sitecore.Configuration.Settings.GetSetting($"CatalogRefresh.ClientId.{destination.ToUpper()}");
                    var destinationClientSecret = Sitecore.Configuration.Settings.GetSetting($"CatalogRefresh.ClientSecret.{destination.ToUpper()}");
                    var catalogId = Sitecore.Configuration.Settings.GetSetting("CatalogRefresh.CatalogId");

                    if (string.IsNullOrEmpty(sourceUrl) ||
                       string.IsNullOrEmpty(sourceClientId) ||
                       string.IsNullOrEmpty(sourceClientSecret) ||
                       string.IsNullOrEmpty(destinationUrl) ||
                       string.IsNullOrEmpty(destinationClientId) ||
                       string.IsNullOrEmpty(destinationClientSecret))
                    {
                        await UpdateRefreshRecord(recordId, "Error", DateTime.UtcNow, "Environment Configuration is missing!");
                        return;
                    }
                    var sourceClient = new OrderCloudClient(new OrderCloudClientConfig
                    {
                        ApiUrl = sourceUrl,
                        AuthUrl = sourceUrl,
                        ClientId = sourceClientId,
                        ClientSecret = sourceClientSecret,
                        Roles = new[] { ApiRole.FullAccess }
                    });

                    var destinationClient = new OrderCloudClient(new OrderCloudClientConfig
                    {
                        ApiUrl = destinationUrl,
                        AuthUrl = destinationUrl,
                        ClientId = destinationClientId,
                        ClientSecret = destinationClientSecret,
                        Roles = new[] { ApiRole.FullAccess }
                    });



                    await CleanupCatalog(destinationClient, catalogId);
                    await CloneCatalog(sourceClient, destinationClient, catalogId);
                    await UpdateRefreshRecord(recordId, "Success", DateTime.UtcNow);
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to Refresh Catalog. Exception: {ex}. ", this);
                    await UpdateRefreshRecord(recordId, "Error", DateTime.UtcNow, ex.Message);
                }

            }).ToList();

            await Task.WhenAll(tasks);

            return await GetMostRecentRefreshRecords();

        }

        private async Task CleanupCatalog(IOrderCloudClient client, string catalogId)
        {
            await DeletePromotions(client);
            await DeleteProductsFacets(client);
            await DeleteSpecs(client);
            await DeletePriceSchedules(client);
            await DeleteCategories(client, catalogId);
            await DeleteProducts(client);
            await DeleteParentProducts(client);
        }

        private async Task CloneCatalog(IOrderCloudClient sourceClient, IOrderCloudClient destinationClient, string catalogId)
        {
            await CloneParentProducts(sourceClient, destinationClient);
            await CloneProducts(sourceClient, destinationClient);
            await CloneCategories(catalogId, sourceClient, destinationClient);
            await CloneCategoryAssignments(catalogId, sourceClient, destinationClient);
            await CloneCategoryProductAssignments(catalogId, sourceClient, destinationClient);
            await ClonePriceSchedules(sourceClient, destinationClient);
            await CloneSpecs(sourceClient, destinationClient);
            await CloneProductsFacets(sourceClient, destinationClient);
            await ClonePromotions(sourceClient, destinationClient);
            await CloneCatalogProductAssignments(sourceClient, destinationClient);
            await CloneProductAssignments(sourceClient, destinationClient);
        }

        private async Task DeletePromotions(IOrderCloudClient client)
        {
            var firstPass = true;
            var promotions = await client.Promotions.ListAsync(pageSize: 100, sortBy: "ID");
            var originalTotal = promotions.Meta.TotalCount;

            while (promotions.Items.Any() && (firstPass || originalTotal > promotions.Meta.TotalCount))
            {
                firstPass = false;
                originalTotal = promotions.Meta.TotalCount;
                var lastId = string.Empty;
                foreach (var promotion in promotions.Items)
                {
                    lastId = promotion.ID;
                    try
                    {
                        await client.Promotions.DeleteAsync(promotion.ID);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to delete Promotion: {promotion.ID}. Exception: {e}", this);
                    }
                }
                promotions = await client.Promotions.ListAsync(pageSize: 100, sortBy: "ID", filters: new { ID = $">{lastId}" });
            }
        }

        private async Task DeleteProductsFacets(IOrderCloudClient client)
        {
            var firstPass = true;
            var productFacets = await client.ProductFacets.ListAsync(pageSize: 100, sortBy: "ID");
            var originalTotal = productFacets.Meta.TotalCount;

            while (productFacets.Items.Any() && (firstPass || originalTotal > productFacets.Meta.TotalCount))
            {
                firstPass = false;
                originalTotal = productFacets.Meta.TotalCount;
                var lastId = string.Empty;
                foreach (var productFacet in productFacets.Items)
                {
                    lastId = productFacet.ID;
                    try
                    {
                        await client.ProductFacets.DeleteAsync(productFacet.ID);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to delete Product Facet: {productFacet.ID}. Exception: {e}", this);
                    }
                }
                productFacets = await client.ProductFacets.ListAsync(pageSize: 100, sortBy: "ID", filters: new { ID = $">{lastId}" });
            }
        }

        private async Task DeleteSpecs(IOrderCloudClient client)
        {
            var firstPass = true;
            var specs = await client.Specs.ListAsync(pageSize: 100, sortBy: "ID");
            var originalTotal = specs.Meta.TotalCount;

            while (specs.Items.Any() && (firstPass || originalTotal > specs.Meta.TotalCount))
            {
                firstPass = false;
                originalTotal = specs.Meta.TotalCount;
                var lastId = string.Empty;
                foreach (var spec in specs.Items)
                {
                    lastId = spec.ID;
                    try
                    {
                        await client.Specs.DeleteAsync(spec.ID);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to delete Spec: {spec.ID}. Exception: {e}", this);
                    }
                }
                specs = await client.Specs.ListAsync(pageSize: 100, sortBy: "ID", filters: new { ID = $">{lastId}" });
            }
        }

        private async Task DeletePriceSchedules(IOrderCloudClient client)
        {
            var firstPass = true;
            var priceSchedules = await client.PriceSchedules.ListAsync(pageSize: 100, sortBy: "ID");
            var originalTotal = priceSchedules.Meta.TotalCount;

            while (priceSchedules.Items.Any() && (firstPass || originalTotal > priceSchedules.Meta.TotalCount))
            {
                firstPass = false;
                originalTotal = priceSchedules.Meta.TotalCount;
                var lastId = string.Empty;
                foreach (var priceSchedule in priceSchedules.Items)
                {
                    lastId = priceSchedule.ID;
                    try
                    {
                        await client.PriceSchedules.DeleteAsync(priceSchedule.ID);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to delete PriceSchedule: {priceSchedule.ID}. Exception: {e}", this);
                    }
                }
                priceSchedules = await client.PriceSchedules.ListAsync(pageSize: 100, sortBy: "ID", filters: new { ID = $">{lastId}" });
            }
        }

        private async Task DeleteCategories(IOrderCloudClient client, string catalogId)
        {
            var firstPass = true;
            var categories = await client.Categories.ListAsync(catalogId, pageSize: 100, sortBy: "ID");
            var originalTotal = categories.Meta.TotalCount;

            while (categories.Items.Any() && (firstPass || originalTotal > categories.Meta.TotalCount))
            {
                firstPass = false;
                originalTotal = categories.Meta.TotalCount;
                var lastId = string.Empty;
                foreach (var category in categories.Items)
                {
                    lastId = category.ID;
                    try
                    {
                        await client.Categories.DeleteAsync(catalogId, category.ID);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to delete Category: {category.ID}. Exception: {e}", this);
                    }
                }
                categories = await client.Categories.ListAsync(catalogId, pageSize: 100, sortBy: "ID", filters: new { ID = $">{lastId}" });
            }
        }

        private async Task DeleteProducts(IOrderCloudClient client)
        {
            var firstPass = true;
            var products = await client.Products.ListAsync(pageSize: 100, sortBy: "ID", filters: new { IsParent = false });
            var originalTotal = products.Meta.TotalCount;

            while (products.Items.Any() && (firstPass || originalTotal > products.Meta.TotalCount))
            {
                firstPass = false;
                originalTotal = products.Meta.TotalCount;
                var lastId = string.Empty;
                foreach (var product in products.Items)
                {
                    lastId = product.ID;
                    try
                    {
                        await client.Products.DeleteAsync(product.ID);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to delete product: {product.ID}. Exception: {e}", this);
                    }
                }
                products = await client.Products.ListAsync(pageSize: 100, sortBy: "ID", filters: new { IsParent = false, ID = $">{lastId}" });
            }
        }

        private async Task DeleteParentProducts(IOrderCloudClient client)
        {
            var firstPass = true;
            var products = await client.Products.ListAsync(pageSize: 100, sortBy: "ID", filters: new { IsParent = true });
            var originalTotal = products.Meta.TotalCount;

            while (products.Items.Any() && (firstPass || originalTotal > products.Meta.TotalCount))
            {
                firstPass = false;
                originalTotal = products.Meta.TotalCount;
                var lastId = string.Empty;
                foreach (var product in products.Items)
                {
                    lastId = product.ID;
                    try
                    {
                        await client.Products.DeleteAsync(product.ID);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to delete product: {product.ID}. Exception: {e}", this);
                    }
                }
                products = await client.Products.ListAsync(pageSize: 100, filters: new { IsParent = true, ID = $">{lastId}" });
            }
        }

        private async Task CloneParentProducts(IOrderCloudClient sourceClient, IOrderCloudClient destinationClient)
        {
            var pageNumber = 1;
            var products = await sourceClient.Products.ListAsync(page: pageNumber, pageSize: 100, filters: new { IsParent = true });
            while (products.Items.Any())
            {
                pageNumber++;
                foreach (var product in products.Items)
                {
                    product.OwnerID = null;
                    try
                    {
                        await destinationClient.Products.CreateAsync(product);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to create parent product: {product.ID}. Exception: {e}", this);
                    }
                    await CloneInventoryRecords(product.ID, sourceClient, destinationClient);
                    await CloneInventoryRecordsAssignments(product.ID, sourceClient, destinationClient);
                }
                products = await sourceClient.Products.ListAsync(page: pageNumber, pageSize: 100, filters: new { IsParent = true });
            }
        }

        private async Task CloneProducts(IOrderCloudClient sourceClient, IOrderCloudClient destinationClient)
        {
            var pageNumber = 1;
            var products = await sourceClient.Products.ListAsync(page: pageNumber, pageSize: 100, filters: new { IsParent = false });
            while (products.Items.Any())
            {
                pageNumber++;
                foreach (var product in products.Items)
                {
                    product.OwnerID = null;
                    try
                    {
                        await destinationClient.Products.CreateAsync(product);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to create product: {product.ID}. Exception: {e}", this);
                    }
                    await CloneInventoryRecords(product.ID, sourceClient, destinationClient);
                    await CloneInventoryRecordsAssignments(product.ID, sourceClient, destinationClient);
                }
                products = await sourceClient.Products.ListAsync(page: pageNumber, pageSize: 100, filters: new { IsParent = false });
            }
        }

        private async Task CloneInventoryRecords(string productId, IOrderCloudClient sourceClient, IOrderCloudClient destinationClient)
        {
            var pageNumber = 1;
            var productInventories = await sourceClient.InventoryRecords.ListAsync(productId, page: pageNumber, pageSize: 100);
            while (productInventories.Items.Any())
            {
                pageNumber++;
                foreach (var productInventory in productInventories.Items)
                {
                    productInventory.OwnerID = null;
                    try
                    {
                        await destinationClient.InventoryRecords.CreateAsync(productId, productInventory);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to create product inventory: {productInventory.ID}. Exception: {e}", this);
                    }
                }
                productInventories = await sourceClient.InventoryRecords.ListAsync(productId, page: pageNumber, pageSize: 100);
            }
        }

        private async Task CloneInventoryRecordsAssignments(string productId, IOrderCloudClient sourceClient, IOrderCloudClient destinationClient)
        {
            var pageNumber = 1;
            var assignments = await sourceClient.InventoryRecords.ListAssignmentsAsync(productId, page: pageNumber, pageSize: 100);
            while (assignments.Items.Any())
            {
                pageNumber++;
                foreach (var assignment in assignments.Items)
                {
                    try
                    {
                        await destinationClient.InventoryRecords.SaveAssignmentAsync(productId, assignment);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to create assignment for inventory: {assignment.InventoryRecordID}. Exception: {e}", this);
                    }
                }
                assignments = await sourceClient.InventoryRecords.ListAssignmentsAsync(productId, page: pageNumber, pageSize: 100);
            }
        }

        private async Task CloneCategories(string catalogId, IOrderCloudClient sourceClient, IOrderCloudClient destinationClient)
        {
            var pageNumber = 1;
            var categories = await sourceClient.Categories.ListAsync(catalogId, page: pageNumber, pageSize: 100, depth: "5");
            while (categories.Items.Any())
            {
                pageNumber++;
                foreach (var category in categories.Items)
                {
                    try
                    {
                        await destinationClient.Categories.CreateAsync(catalogId, category);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to create Category: {category.ID}. Exception: {e}", this);
                    }
                }
                categories = await sourceClient.Categories.ListAsync(catalogId, page: pageNumber, pageSize: 100, depth: "5");
            }
        }

        private async Task CloneCategoryAssignments(string catalogId, IOrderCloudClient sourceClient, IOrderCloudClient destinationClient)
        {
            var pageNumber = 1;
            var assignments = await sourceClient.Categories.ListAssignmentsAsync(catalogId, page: pageNumber, pageSize: 100);
            while (assignments.Items.Any())
            {
                pageNumber++;
                foreach (var assignment in assignments.Items)
                {
                    try
                    {
                        await destinationClient.Categories.SaveAssignmentAsync(catalogId, assignment);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to create Assignment for category: {assignment.CategoryID}. Exception: {e}", this);
                    }
                }
                assignments = await sourceClient.Categories.ListAssignmentsAsync(catalogId, page: pageNumber, pageSize: 100);
            }
        }

        private async Task CloneCategoryProductAssignments(string catalogId, IOrderCloudClient sourceClient, IOrderCloudClient destinationClient)
        {
            var pageNumber = 1;
            var assignments = await sourceClient.Categories.ListProductAssignmentsAsync(catalogId, page: pageNumber, pageSize: 100);
            while (assignments.Items.Any())
            {
                pageNumber++;
                foreach (var assignment in assignments.Items)
                {
                    try
                    {
                        await destinationClient.Categories.SaveProductAssignmentAsync(catalogId, assignment);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to create Assignment for category and product: {assignment.CategoryID}|{assignment.ProductID}. Exception: {e}", this);
                    }
                }
                assignments = await sourceClient.Categories.ListProductAssignmentsAsync(catalogId, page: pageNumber, pageSize: 100);
            }
        }

        private async Task ClonePriceSchedules(IOrderCloudClient sourceClient, IOrderCloudClient destinationClient)
        {
            var pageNumber = 1;
            var priceSchedules = await sourceClient.PriceSchedules.ListAsync(page: pageNumber, pageSize: 100);
            while (priceSchedules.Items.Any())
            {
                pageNumber++;
                foreach (var priceSchedule in priceSchedules.Items)
                {
                    try
                    {
                        priceSchedule.OwnerID = null;
                        await destinationClient.PriceSchedules.CreateAsync(priceSchedule);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to create Price Schedule : {priceSchedule.ID}. Exception: {e}", this);
                    }
                }
                priceSchedules = await sourceClient.PriceSchedules.ListAsync(page: pageNumber, pageSize: 100);
            }
        }

        private async Task CloneSpecs(IOrderCloudClient sourceClient, IOrderCloudClient destinationClient)
        {
            var pageNumber = 1;
            var specs = await sourceClient.Specs.ListAsync(page: pageNumber, pageSize: 100);
            while (specs.Items.Any())
            {
                pageNumber++;
                foreach (var spec in specs.Items)
                {
                    try
                    {
                        spec.OwnerID = null;
                        await destinationClient.Specs.CreateAsync(spec);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to create Spec : {spec.ID}. Exception: {e}", this);
                    }
                }
                specs = await sourceClient.Specs.ListAsync(page: pageNumber, pageSize: 100);
            }
        }

        private async Task CloneProductsFacets(IOrderCloudClient sourceClient, IOrderCloudClient destinationClient)
        {
            var pageNumber = 1;
            var productFacets = await sourceClient.ProductFacets.ListAsync(page: pageNumber, pageSize: 100);
            while (productFacets.Items.Any())
            {
                pageNumber++;
                foreach (var productFacet in productFacets.Items)
                {
                    try
                    {
                        await destinationClient.ProductFacets.CreateAsync(productFacet);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to create Product Facet : {productFacet.ID}. Exception: {e}", this);
                    }
                }
                productFacets = await sourceClient.ProductFacets.ListAsync(page: pageNumber, pageSize: 100);
            }
        }

        private async Task ClonePromotions(IOrderCloudClient sourceClient, IOrderCloudClient destinationClient)
        {
            var pageNumber = 1;
            var promotions = await sourceClient.Promotions.ListAsync(page: pageNumber, pageSize: 100);
            while (promotions.Items.Any())
            {
                pageNumber++;
                foreach (var promotion in promotions.Items)
                {
                    try
                    {
                        promotion.OwnerID = null;
                        await destinationClient.Promotions.CreateAsync(promotion);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to create Promotion: {promotion.ID}. Exception: {e}", this);
                    }
                }
                promotions = await sourceClient.Promotions.ListAsync(page: pageNumber, pageSize: 100);
            }
        }

        private async Task CloneCatalogProductAssignments(IOrderCloudClient sourceClient, IOrderCloudClient destinationClient)
        {
            var pageNumber = 1;
            var assignments = await sourceClient.Catalogs.ListProductAssignmentsAsync(page: pageNumber, pageSize: 100);
            while (assignments.Items.Any())
            {
                pageNumber++;
                foreach (var assignment in assignments.Items)
                {
                    try
                    {
                        await destinationClient.Catalogs.SaveProductAssignmentAsync(assignment);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to create Catalog Product Assignment: {assignment.CatalogID}|{assignment.ProductID}. Exception: {e}", this);
                    }
                }
                assignments = await sourceClient.Catalogs.ListProductAssignmentsAsync(page: pageNumber, pageSize: 100);
            }
        }

        private async Task CloneProductAssignments(IOrderCloudClient sourceClient, IOrderCloudClient destinationClient)
        {
            var pageNumber = 1;
            var assignments = await sourceClient.Products.ListAssignmentsAsync(page: pageNumber, pageSize: 100);
            while (assignments.Items.Any())
            {
                pageNumber++;
                foreach (var assignment in assignments.Items)
                {
                    try
                    {
                        assignment.SellerID = null;
                        await destinationClient.Products.SaveAssignmentAsync(assignment);
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Failed to create Product Assignment: {assignment.ProductID}. Exception: {e}", this);
                    }
                }
                assignments = await sourceClient.Products.ListAssignmentsAsync(page: pageNumber, pageSize: 100);
            }
        }

        private async Task<int> CreateRefreshRecord(CatalogRefreshRecord record)
        {
            using (var sqlConnection =
                   new SqlConnection(ConfigurationManager.ConnectionStrings["AlyasCatalogRefreshDb"]?.ConnectionString ??
                                     string.Empty))
            {
                await sqlConnection.OpenAsync();
                using (var sqlCommand = new SqlCommand(CreateRefreshRecordCommandText(), sqlConnection))
                {
                    sqlCommand.Parameters.Add("@Status", SqlDbType.NVarChar, 50).Value = record.Status;
                    sqlCommand.Parameters.Add("@Source", SqlDbType.NVarChar, 50).Value = record.Source;
                    sqlCommand.Parameters.Add("@Destination", SqlDbType.NVarChar, 50).Value = record.Destination;
                    sqlCommand.Parameters.Add("@StartTime", SqlDbType.DateTime2).Value = record.StartTime;
                    return Convert.ToInt32(await sqlCommand.ExecuteScalarAsync());
                }
            }
        }

        private async Task UpdateRefreshRecord(int recordId, string status, DateTime endTime, string errorMessage = "")
        {
            using (var sqlConnection =
                   new SqlConnection(ConfigurationManager.ConnectionStrings["AlyasCatalogRefreshDb"]?.ConnectionString ??
                                     string.Empty))
            {
                await sqlConnection.OpenAsync();
                using (var sqlCommand = new SqlCommand(UpdateRefreshCommandText(), sqlConnection))
                {
                    sqlCommand.Parameters.Add("@Status", SqlDbType.NVarChar, 50).Value = status;
                    sqlCommand.Parameters.Add("@EndTime", SqlDbType.DateTime2).Value = endTime;
                    sqlCommand.Parameters.Add("@ErrorMessage", SqlDbType.NVarChar, -1).Value = !string.IsNullOrEmpty(errorMessage) ? (object)errorMessage : DBNull.Value;
                    sqlCommand.Parameters.Add("@Id", SqlDbType.Int).Value = recordId;
                    await sqlCommand.ExecuteNonQueryAsync();
                }
            }
        }

        private static string GetMostRecentRefreshRecordsCommandText()
        {
            var sb = new StringBuilder();
            sb.Append("SELECT TOP(10) Status,Source, Destination,StartTime, EndTime, ErrorMessage ");
            sb.Append("FROM CatalogRefreshHistory ");
            sb.Append("ORDER BY Id desc");
            return sb.ToString();
        }

        private static string CreateRefreshRecordCommandText()
        {
            var sb = new StringBuilder();
            sb.Append("INSERT INTO CatalogRefreshHistory ");
            sb.Append("(Status, Source, Destination, StartTime) ");
            sb.Append("OUTPUT INSERTED.Id ");
            sb.Append("VALUES ");
            sb.Append("(@Status, @Source, @Destination, @StartTime) ");
            return sb.ToString();
        }

        private static string UpdateRefreshCommandText()
        {
            var sb = new StringBuilder();
            sb.Append("UPDATE CatalogRefreshHistory ");
            sb.Append("SET Status = @Status, EndTime = @EndTime, ErrorMessage = @ErrorMessage ");
            sb.Append("Where Id = @Id ");
            return sb.ToString();
        }
    }
}