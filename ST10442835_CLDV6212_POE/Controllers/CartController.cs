using ST10442835_CLDV6212_POE.Data;
using ST10442835_CLDV6212_POE.Models;
using ST10442835_CLDV6212_POE.Models.ViewModels;
using ST10442835_CLDV6212_POE.Services;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ST10442835_CLDV6212_POE.Controllers
{
    [Authorize(Roles = "customer")]
    public class CartController : Controller
    {
        private readonly AuthDbContext _db;
        private readonly IAzureStorageService _storageService;

        public CartController(AuthDbContext db, IAzureStorageService storageService)
        {
            _db = db;
            _storageService = storageService;
        }

        public async Task<IActionResult> Index()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Index", "Login");

            var cartItems = await _db.Cart
                .Where(c => c.CustomerUsername == username)
                .ToListAsync();

            var viewModelList = new List<CartItemViewModel>();

            foreach (var item in cartItems)
            {
                var product = await _storageService.GetEntityAsync<Product>("Product", item.ProductId);
                if (product == null) continue;

                viewModelList.Add(new CartItemViewModel
                {
                    ProductId = product.RowKey,
                    ProductName = product.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = (decimal)product.Price
                });
            }

            return View(new CartPageViewModel { Items = viewModelList });
        }

        public async Task<IActionResult> Add(string productId)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(productId))
                return RedirectToAction("Index", "Product");

            var product = await _storageService.GetEntityAsync<Product>("Product", productId);
            if (product == null)
                return NotFound();

            var existing = await _db.Cart.FirstOrDefaultAsync(c =>
                c.ProductId == productId && c.CustomerUsername == username);

            if (existing != null)
            {
                existing.Quantity += 1;
            }
            else
            {
                _db.Cart.Add(new Cart
                {
                    CustomerUsername = username,
                    ProductId = productId,
                    Quantity = 1
                });
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = $"{product.ProductName} added to cart.";
            return RedirectToAction("Index", "Product");
        }

        [HttpPost]
        public async Task<IActionResult> Checkout()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Index", "Login");

            // BYPASS CUSTOMER CHECK - Use username as customer ID if customer not found
            var allCustomers = await _storageService.GetAllEntitiesAsync<Customer>();
            var customer = allCustomers.FirstOrDefault(c =>
                c.Username != null && c.Username.ToLower() == username.ToLower());

            string customerId;
            if (customer == null)
            {
                // If customer not found, use username as customer ID for the order
                customerId = username;
            }
            else
            {
                customerId = customer.RowKey;
            }

            var cartItems = await _db.Cart
                .Where(c => c.CustomerUsername == username)
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["Error"] = "Your cart is empty.";
                return RedirectToAction("Index");
            }

            foreach (var item in cartItems)
            {
                var product = await _storageService.GetEntityAsync<Product>("Product", item.ProductId);
                if (product == null) continue;

                if (product.StockAvailable < item.Quantity)
                {
                    TempData["Error"] = $"Insufficient stock for {product.ProductName}. Available: {product.StockAvailable}";
                    return RedirectToAction("Index");
                }

                var order = new Order
                {
                    PartitionKey = "Order",
                    RowKey = Guid.NewGuid().ToString(),
                    CustomerId = customerId, // Use either real customer ID or username
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    OrderDate = DateTime.UtcNow,
                    Status = "Completed"
                };

                await _storageService.AddEntityAsync(order);

                product.StockAvailable -= item.Quantity;
                if (product.StockAvailable < 0) product.StockAvailable = 0;

                await _storageService.UpdateEntityAsync(product);

                await _storageService.SendMessageAsync("order-notifications",
                    $"New order: {item.Quantity}x {product.ProductName} for {username}");
            }

            _db.Cart.RemoveRange(cartItems);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Order placed successfully!";
            return RedirectToAction("Confirmation");
        }

        public IActionResult Confirmation()
        {
            ViewBag.Message = TempData["SuccessMessage"] ?? "Thank you for your purchase!";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Remove(string productId)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Index");

            var item = await _db.Cart.FirstOrDefaultAsync(c =>
                c.CustomerUsername == username && c.ProductId == productId);

            if (item != null)
            {
                _db.Cart.Remove(item);
                await _db.SaveChangesAsync();
                TempData["Success"] = "Item removed from cart.";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateQuantities(List<CartItemViewModel> items)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Index");

            foreach (var item in items)
            {
                var cartItem = await _db.Cart.FirstOrDefaultAsync(c =>
                    c.CustomerUsername == username && c.ProductId == item.ProductId);

                if (cartItem != null)
                {
                    cartItem.Quantity = item.Quantity;
                }
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Cart updated successfully.";
            return RedirectToAction("Index");
        }
    }
}