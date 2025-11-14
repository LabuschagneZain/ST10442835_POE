using ST10442835_CLDV6212_POE.Data;
using ST10442835_CLDV6212_POE.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ST10442835_CLDV6212_POE
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ===============================
            // Add services
            // ===============================
            builder.Services.AddControllersWithViews();

            // ===============================
            // Register DbContext for authentication
            // ===============================
            builder.Services.AddDbContext<AuthDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("AuthDbConnection")));

            // ===============================
            // Add session support
            // ===============================
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(60);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            // ===============================
            // Add cookie-based authentication
            // ===============================
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Login/Index";
                    options.AccessDeniedPath = "/Login/AccessDenied";
                });

            // ===============================
            // Register IHttpClientFactory + named client for Azure Functions
            // ===============================
            builder.Services.AddHttpClient("Functions", client =>
            {
                client.BaseAddress = new Uri("https://your-azure-functions-url/"); // Replace with your Azure Functions URL
            });

            // ===============================
            // Register custom services
            // ===============================
            builder.Services.AddScoped<IAzureStorageService, AzureStorageService>();
            builder.Services.AddScoped<IFunctionApi, FunctionApiClient>(); // Use your FunctionApiClient implementation

            builder.Services.AddLogging();

            // ===============================
            // Build the app
            // ===============================
            var app = builder.Build();

            // ===============================
            // Set default culture
            // ===============================
            var culture = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            // ===============================
            // Middleware pipeline
            // ===============================
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            // Must be before UseAuthorization
            app.UseSession();
            app.UseAuthentication();
            app.UseAuthorization();

            // ===============================
            // Map default route
            // ===============================
            app.MapControllerRoute(
     name: "default",
     pattern: "{controller=Home}/{action=Index}/{id?}");


            app.Run();
        }
    }
}
