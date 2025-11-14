using Microsoft.Azure.Functions.Worker.Http;
using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ABCRetailers.Functions.Helpers
{
    public static class HttpJson
    {
        static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
        public static async Task<T?> ReadAsync<T>(HttpRequestData req)
        {
            using var s = req.Body;
            return await JsonSerializer.DeserializeAsync<T>(s, _json);
        }

        public static async Task<HttpResponseData> Ok<T>(HttpRequestData req, T body)
            => await WriteAsync(req, HttpStatusCode.OK, body);

        public static async Task<HttpResponseData> Created<T>(HttpRequestData req, T body)
            => await WriteAsync(req, HttpStatusCode.Created, body);

        public static async Task<HttpResponseData> Bad(HttpRequestData req, string message)
            => await TextAsync(req, HttpStatusCode.BadRequest, message);

        public static async Task<HttpResponseData> NotFound(HttpRequestData req, string message = "Not Found")
            => await TextAsync(req, HttpStatusCode.NotFound, message);

        public static HttpResponseData NoContent(HttpRequestData req)
        {
            var r = req.CreateResponse(HttpStatusCode.NoContent);
            return r;
        }

        public static async Task<HttpResponseData> TextAsync(HttpRequestData req, HttpStatusCode code, string message)
        {
            var r = req.CreateResponse(code);
            r.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            await r.WriteStringAsync(message, Encoding.UTF8);
            return r;
        }

        private static async Task<HttpResponseData> WriteAsync<T>(HttpRequestData req, HttpStatusCode code, T body)
        {
            var r = req.CreateResponse(code);
            r.Headers.Add("Content-Type", "application/json; charset=utf-8");
            var json = JsonSerializer.Serialize(body, _json);
            await r.WriteStringAsync(json, Encoding.UTF8);
            return r;
        }
    }
}
