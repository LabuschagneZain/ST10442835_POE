using ST10442835_CLDV6212_POE.Data;
using ST10442835_CLDV6212_POE.Models;
using ST10442835_CLDV6212_POE.Models.ViewModels;
using ST10442835_CLDV6212_POE.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ST10442835_CLDV6212_POE.Controllers
{
    public class LoginController : Controller
    {
        private readonly AuthDbContext _db;
        private readonly IFunctionApi _functionsApi;
        private readonly ILogger<LoginController> _logger;

        public LoginController(AuthDbContext db, IFunctionApi functionsApi, ILogger<LoginController> logger)
        {
            _db = db;
            _functionsApi = functionsApi;
            _logger = logger;
        }

        // GET: /Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Index(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel());
        }

        // POST: /Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == model.Username);
                if (user == null || user.PasswordHash != model.Password)
                {
                    ViewBag.Error = "Invalid username or password.";
                    return View(model);
                }

                string customerId = "";
                if (user.Role.Trim() == "Customer")
                {
                    Customer? customer = null;
                    try
                    {
                        customer = await _functionsApi.GetCustomerAsync(user.Username);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Azure API error for {Username}", user.Username);
                    }

                    if (customer == null)
                    {
                        _logger.LogWarning("No Azure customer found for {Username}", user.Username);
                        ViewBag.Error = "No customer record found in the system. Please contact support.";
                        return View(model);
                    }

                    customerId = customer.CustomerId;
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role.Trim().ToLower())
                };

                if (!string.IsNullOrEmpty(customerId))
                    claims.Add(new Claim("CustomerId", customerId));

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(60)
                    });

                HttpContext.Session.SetString("Username", user.Username);
                HttpContext.Session.SetString("Role", user.Role.Trim());
                if (!string.IsNullOrEmpty(customerId))
                    HttpContext.Session.SetString("CustomerId", customerId);

                _logger.LogInformation("User {Username} logged in as {Role}", user.Username, user.Role);

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return user.Role.Trim() == "Admin"
                    ? RedirectToAction("AdminDashboard", "Home")
                    : RedirectToAction("CustomerDashboard", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed for user {Username}", model.Username);
                ViewBag.Error = $"Login error: {ex.Message}";
                return View(model);
            }
        }

        // GET: /Login/Register
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        // POST: /Login/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var exists = await _db.Users.AnyAsync(u => u.Username == model.Username);
            if (exists)
            {
                ViewBag.Error = "Username already exists.";
                return View(model);
            }

            try
            {
                var user = new User
                {
                    Username = model.Username,
                    PasswordHash = model.Password,
                    Role = model.Role.ToLower()
                };
                _db.Users.Add(user);
                await _db.SaveChangesAsync();

                _logger.LogInformation("User saved to SQL: {Username} as {Role}", model.Username, model.Role);

                if (model.Role.Trim() == "Customer")
                {
                    var customer = new Customer
                    {
                        Username = model.Username,
                        Name = model.FirstName,
                        Surname = model.LastName,
                        Email = model.Email ?? "",
                        ShippingAddress = model.ShippingAddress ?? ""
                    };

                    try
                    {
                        var createdCustomer = await _functionsApi.CreateCustomerAsync(customer);
                        _logger.LogInformation("Customer created in Azure with ID: {Id}", createdCustomer.CustomerId);
                    }
                    catch (Exception azureEx)
                    {
                        _logger.LogError(azureEx, "Failed to create customer in Azure for {Username}", model.Username);
                    }
                }

                TempData["Success"] = "Registration successful! Please log in.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration failed for user {Username}", model.Username);
                ViewBag.Error = $"Could not complete registration: {ex.Message}";
                return View(model);
            }
        }

        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied() => View();
    }
}
