using ABCRetailers.Functions.Entities;
using ABCRetailers.Functions.Helpers;
using ABCRetailers.Functions.Models;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace ABCRetailers.Functions.Functions
{
    public class OrdersFunctions
    {
        private readonly string _conn;
        private readonly string _ordersTable;
        private readonly string _productsTable;
        private readonly string _customersTable;
        private readonly string _queueOrder;
        private readonly string _queueStock;

        public OrdersFunctions(IConfiguration cfg)
        {
            _conn = cfg["STORAGE_CONNECTION"] ?? throw new InvalidOperationException("STORAGE_CONNECTION missing");
            _ordersTable = cfg["TABLE_ORDER"] ?? "Order";
            _productsTable = cfg["TABLE_PRODUCT"] ?? "Product";
            _customersTable = cfg["TABLE_CUSTOMER"] ?? "Customer";
            _queueOrder = cfg["QUEUE_ORDER_NOTIFICATIONS"] ?? "order-notifications";
            _queueStock = cfg["QUEUE_STOCK_UPDATES"] ?? "stock-updates";
        }

        [Function("Orders_List")]
        public async Task<HttpResponseData> List(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders")] HttpRequestData req)
        {
            var table = new TableClient(_conn, _ordersTable);
            await table.CreateIfNotExistsAsync();

            var items = new List<OrderDto>();
            await foreach (var e in table.QueryAsync<OrderEntity>(x => x.PartitionKey == "Order"))
            {
                items.Add(Map.ToDto(e));
            }

            var ordered = items.OrderByDescending(o => o.OrderDateUtc).ToList();
            return await HttpJson.Ok(req, ordered);
        }

        [Function("Orders_Get")]
        public async Task<HttpResponseData> Get(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{id}")] HttpRequestData req, string id)
        {
            var table = new TableClient(_conn, _ordersTable);
            try
            {
                var e = await table.GetEntityAsync<OrderEntity>("Order", id);
                return await HttpJson.Ok(req, Map.ToDto(e.Value));
            }
            catch
            {
                return await HttpJson.NotFound(req, "Order not found");
            }
        }

        public record OrderCreate(string CustomerId, string ProductId, int Quantity);

        [Function("Orders_Create")]
        public async Task<HttpResponseData> Create(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequestData req)
        {
            var input = await HttpJson.ReadAsync<OrderCreate>(req);
            if (input is null || string.IsNullOrWhiteSpace(input.CustomerId) || string.IsNullOrWhiteSpace(input.ProductId) || input.Quantity < 1)
                return await HttpJson.Bad(req, "CustomerId, ProductId, and Quantity >= 1 required");

            var orders = new TableClient(_conn, _ordersTable);
            var products = new TableClient(_conn, _productsTable);
            var customers = new TableClient(_conn, _customersTable);

            await orders.CreateIfNotExistsAsync();
            await products.CreateIfNotExistsAsync();
            await customers.CreateIfNotExistsAsync();

            // FIXED: Simplified customer lookup - direct entity retrieval only
            CustomerEntity? customer = null;
            try
            {
                var result = await customers.GetEntityAsync<CustomerEntity>("Customer", input.CustomerId);
                customer = result.Value;
                Console.WriteLine($"Customer found: {customer.Name} {customer.Surname}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Customer lookup failed for ID '{input.CustomerId}': {ex.Message}");
                return await HttpJson.Bad(req, $"Invalid CustomerId: {input.CustomerId}. Customer not found.");
            }

            // FIXED: Simplified product lookup - direct entity retrieval only
            ProductEntity? product = null;
            try
            {
                var result = await products.GetEntityAsync<ProductEntity>("Product", input.ProductId);
                product = result.Value;
                Console.WriteLine($"Product found: {product.ProductName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Product lookup failed for ID '{input.ProductId}': {ex.Message}");
                return await HttpJson.Bad(req, $"Invalid ProductId: {input.ProductId}. Product not found.");
            }

            // Validate stock
            if (product.StockAvailable < input.Quantity)
                return await HttpJson.Bad(req, $"Insufficient stock. Available: {product.StockAvailable}");

            // Create Order
            var order = new OrderEntity
            {
                CustomerId = input.CustomerId,
                ProductId = input.ProductId,
                ProductName = product.ProductName,
                Quantity = input.Quantity,
                UnitPrice = product.Price,
                OrderDate = DateTimeOffset.UtcNow,
                Status = "Submitted"
            };

            await orders.AddEntityAsync(order);

            // Update stock
            product.StockAvailable -= input.Quantity;
            await products.UpdateEntityAsync(product, product.ETag, TableUpdateMode.Replace);

            // Queue updates
            var queueOrder = new QueueClient(_conn, _queueOrder, new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
            var queueStock = new QueueClient(_conn, _queueStock, new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
            await queueOrder.CreateIfNotExistsAsync();
            await queueStock.CreateIfNotExistsAsync();

            var orderMsg = new
            {
                Type = "OrderCreated",
                OrderId = order.RowKey,
                order.CustomerId,
                CustomerName = $"{customer.Name} {customer.Surname}",
                order.ProductId,
                ProductName = product.ProductName,
                order.Quantity,
                order.UnitPrice,
                TotalAmount = order.UnitPrice * order.Quantity,
                OrderDateUtc = order.OrderDate,
                order.Status
            };
            await queueOrder.SendMessageAsync(JsonSerializer.Serialize(orderMsg));

            var stockMsg = new
            {
                Type = "StockUpdated",
                productId = product.RowKey,
                ProductName = product.ProductName,
                PreviousStock = product.StockAvailable + input.Quantity,
                NewStock = product.StockAvailable,
                UpdatedDateUtc = DateTimeOffset.UtcNow,
                UpdatedBy = "Order System"
            };
            await queueStock.SendMessageAsync(JsonSerializer.Serialize(stockMsg));

            return await HttpJson.Created(req, Map.ToDto(order));
        }

        public record OrderStatusUpdate(string Status);

        [Function("Orders_UpdateStatus")]
        public async Task<HttpResponseData> UpdateStatus(
            [HttpTrigger(AuthorizationLevel.Anonymous, "patch", "post", "put", Route = "orders/{id}/status")] HttpRequestData req, string id)
        {
            var input = await HttpJson.ReadAsync<OrderStatusUpdate>(req);
            if (input is null || string.IsNullOrWhiteSpace(input.Status))
                return await HttpJson.Bad(req, "Status is required");

            var orders = new TableClient(_conn, _ordersTable);
            try
            {
                var resp = await orders.GetEntityAsync<OrderEntity>("Order", id);
                var e = resp.Value;
                var previous = e.Status;

                e.Status = input.Status;
                await orders.UpdateEntityAsync(e, e.ETag, TableUpdateMode.Replace);

                var queueOrder = new QueueClient(_conn, _queueOrder, new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 });
                await queueOrder.CreateIfNotExistsAsync();
                var statusMsg = new
                {
                    Type = "OrderStatusUpdated",
                    OrderId = e.RowKey,
                    PreviousStatus = previous,
                    NewStatus = e.Status,
                    UpdatedDateUtc = DateTimeOffset.UtcNow,
                    UpdatedBy = "System"
                };
                await queueOrder.SendMessageAsync(JsonSerializer.Serialize(statusMsg));
                return await HttpJson.Ok(req, Map.ToDto(e));
            }
            catch
            {
                return await HttpJson.NotFound(req, "Order not found");
            }
        }

        [Function("Orders_Delete")]
        public async Task<HttpResponseData> Delete(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "orders/{id}")] HttpRequestData req, string id)
        {
            var table = new TableClient(_conn, _ordersTable);
            await table.DeleteEntityAsync("Order", id);
            return HttpJson.NoContent(req);
        }
    }
}