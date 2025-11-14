using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs.Models;
using Humanizer;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using ST10442835_CLDV6212_POE.Models;
using System.Net.Http.Headers;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using static ST10442835_CLDV6212_POE.Models.Order;
using static System.Net.WebRequestMethods;

namespace ST10442835_CLDV6212_POE.Services
{
    public class FunctionApiClient : IFunctionApi
    {
        private readonly HttpClient _http;
        private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

        private const string CustomersRoute = "api/customers";
        private const string ProductsRoute = "api/products";
        private const string OrdersRoute = "api/orders";
        private const string UploadsRoute = "api/uploads/proof-of-payment";

        public FunctionApiClient(IHttpClientFactory factory)
        {
            _http = factory.CreateClient("Functions");
        }

        private static HttpContent JsonBody(object obj)
            => new StringContent(JsonSerializer.Serialize(obj, _json), Encoding.UTF8, "application/json");

        private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage resp)
        {
            resp.EnsureSuccessStatusCode();
            var stream = await resp.Content.ReadAsStreamAsync();
            var data = await JsonSerializer.DeserializeAsync<T>(stream, _json);
            return data!;
        }

        public async Task<List<Customer>> GetCustomersAsync()
            => await ReadJsonAsync<List<Customer>>(await _http.GetAsync(CustomersRoute));

        public async Task<Customer?> GetCustomerAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            try
            {
                // Proper URL encoding for special characters
                var encodedId = Uri.EscapeDataString(id);
                var resp = await _http.GetAsync($"{CustomersRoute}/{encodedId}");

                Console.WriteLine($"Customer API call: {CustomersRoute}/{encodedId}, Status: {resp.StatusCode}");

                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"Customer not found with ID: {id}");
                    return null;
                }

                resp.EnsureSuccessStatusCode();
                return await ReadJsonAsync<Customer>(resp);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting customer {id}: {ex.Message}");
                return null;
            }
        }

        public async Task<Customer> CreateCustomerAsync(Customer c)
            => await ReadJsonAsync<Customer>(await _http.PostAsync(CustomersRoute, JsonBody(new
            {
                name = c.Name,
                surname = c.Surname,
                username = c.Username,
                email = c.Email,
                shippingAddress = c.ShippingAddress
            })));

        public async Task<Customer> UpdateCustomerAsync(string id, Customer c)
            => await ReadJsonAsync<Customer>(await _http.PutAsync($"{CustomersRoute}/{id}", JsonBody(new
            {
                name = c.Name,
                surname = c.Surname,
                username = c.Username,
                email = c.Email,
                shippingAddress = c.ShippingAddress
            })));

        public async Task DeleteCustomerAsync(string id)
            => (await _http.DeleteAsync($"{CustomersRoute}/{id}")).EnsureSuccessStatusCode();

        public async Task<List<Product>> GetProductsAsync()
            => await ReadJsonAsync<List<Product>>(await _http.GetAsync(ProductsRoute));

        public async Task<Product?> GetProductAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            try
            {
                // Proper URL encoding
                var encodedId = Uri.EscapeDataString(id);
                var resp = await _http.GetAsync($"{ProductsRoute}/{encodedId}");

                Console.WriteLine($"Product API call: {ProductsRoute}/{encodedId}, Status: {resp.StatusCode}");

                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"Product not found with ID: {id}");
                    return null;
                }

                resp.EnsureSuccessStatusCode();
                return await ReadJsonAsync<Product>(resp);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting product {id}: {ex.Message}");
                return null;
            }
        }

        public async Task<Product> CreateProductAsync(Product p, IFormFile? imageFile)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(p.ProductName), "ProductName");
            form.Add(new StringContent(p.Description ?? string.Empty), "Description");
            form.Add(new StringContent(p.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)), "Price");
            form.Add(new StringContent(p.StockAvailable.ToString(System.Globalization.CultureInfo.InvariantCulture)), "StockAvailable");
            if (!string.IsNullOrWhiteSpace(p.ImageUrl)) form.Add(new StringContent(p.ImageUrl), "ImageUrl");
            if (imageFile is not null && imageFile.Length > 0)
            {
                var file = new StreamContent(imageFile.OpenReadStream());
                file.Headers.ContentType = new MediaTypeHeaderValue(imageFile.ContentType ?? "application/octet-stream");
                form.Add(file, "ImageFile", imageFile.FileName);
            }
            return await ReadJsonAsync<Product>(await _http.PostAsync(ProductsRoute, form));
        }

        public async Task<Product> UpdateProductAsync(string id, Product p, IFormFile? imageFile)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(p.ProductName), "ProductName");
            form.Add(new StringContent(p.Description ?? string.Empty), "Description");
            form.Add(new StringContent(p.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)), "Price");
            form.Add(new StringContent(p.StockAvailable.ToString(System.Globalization.CultureInfo.InvariantCulture)), "StockAvailable");
            if (!string.IsNullOrWhiteSpace(p.ImageUrl)) form.Add(new StringContent(p.ImageUrl), "ImageUrl");
            if (imageFile is not null && imageFile.Length > 0)
            {

                var file = new StreamContent(imageFile.OpenReadStream());
                file.Headers.ContentType = new MediaTypeHeaderValue(imageFile.ContentType ?? "application/octet-stream");
                form.Add(file, "ImageFile", imageFile.FileName);
            }
            return await ReadJsonAsync<Product>(await _http.PutAsync($"{ProductsRoute}/{id}", form));
        }

        public async Task DeleteProductAsync(string id)
        {
            var response = await _http.DeleteAsync($"{ProductsRoute}/{id}");
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to delete product with RowKey {id}. Status: {response.StatusCode}");
        }

        public async Task<List<Order>> GetOrdersAsync()
        {
            var dtos = await ReadJsonAsync<List<OrderDto>>(await _http.GetAsync(OrdersRoute));
            return dtos.Select(ToOrder).ToList();
        }

        public async Task<Order?> GetOrderAsync(string id)
        {
            var resp = await _http.GetAsync($"{OrdersRoute}/{id}");
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            var dto = await ReadJsonAsync<OrderDto>(resp);
            return ToOrder(dto);
        }

        public async Task<Order> CreateOrderAsync(string customerId, string productId, int quantity)
        {
            var payload = new { customerId, productId, quantity };
            var dto = await ReadJsonAsync<OrderDto>(await _http.PostAsync(OrdersRoute, JsonBody(payload)));
            return ToOrder(dto);
        }

        public async Task UpdateOrderStatusAsync(string id, string newStatus)
        {
            var payload = new { status = newStatus };
            (await _http.PatchAsync($"{OrdersRoute}/{id}/status", JsonBody(payload))).EnsureSuccessStatusCode();
        }

        public async Task DeleteOrderAsync(string id)
        => (await _http.DeleteAsync($"{OrdersRoute}/{id}")).EnsureSuccessStatusCode();

        public async Task<string> UploadProofOfPaymentAsync(IFormFile file, string? orderId, string? customerName)
        {
            using var form = new MultipartFormDataContent();
            var sc = new StreamContent(file.OpenReadStream());
            sc.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
            form.Add(sc, "ProofOfPayment", file.FileName);
            if (!string.IsNullOrWhiteSpace(orderId)) form.Add(new StringContent(orderId), "OrderId");
            if (!string.IsNullOrWhiteSpace(customerName)) form.Add(new StringContent(customerName), "CustomerName");

            var resp = await _http.PostAsync(UploadsRoute, form);
            resp.EnsureSuccessStatusCode();

            var doc = await ReadJsonAsync<Dictionary<string, string>>(resp);
            return doc.TryGetValue("fileName", out var name) ? name : file.FileName;
        }

        private static Order ToOrder(OrderDto d)
        {
            var status = Enum.TryParse<Order.OrderStatus>(d.Status, ignoreCase: true, out var s)
                ? s : Order.OrderStatus.Submitted;

            return new Order
            {
                RowKey = d.Id,
                CustomerId = d.CustomerId,
                ProductId = d.ProductId,
                ProductName = d.ProductName,
                Quantity = d.Quantity,
                UnitPrice = (double)d.UnitPrice,
                OrderDate = d.OrderDateUtc.DateTime,
                Status = status.ToString()
                // TotalPrice is computed automatically, so remove this line
            };
        }

        private sealed record OrderDto(
            string Id,
            string CustomerId,
            string ProductId,
            string ProductName,
            int Quantity,
            decimal UnitPrice,
            DateTimeOffset OrderDateUtc,
            string Status);
    }
}

internal static class HttpClientPatchExtensions
{
    public static Task<HttpResponseMessage> PatchAsync(this HttpClient client, string requestUri, HttpContent content)
=> client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, requestUri) { Content = content });
}