using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ST10442835_CLDV6212_POE.Models;
using ST10442835_CLDV6212_POE.Services;

namespace ST10442835_CLDV6212_POE.Controllers
{
    public class ProductController : Controller
    {
        private readonly IAzureStorageService _storageService;
        private readonly ILogger<ProductController> _logger;

        public ProductController(IAzureStorageService storageService, ILogger<ProductController> logger)
        {
            _storageService = storageService;
            _logger = logger;
        }

        // ============================
        // Admin only - product management
        // ============================
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var products = await _storageService.GetAllEntitiesAsync<Product>();
            return View(products);
        }

        [Authorize(Roles = "admin")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                if (product.Price <= 0)
                {
                    ModelState.AddModelError("Price", "Price must be greater than $0.00");
                    return View(product);
                }

                if (imageFile != null && imageFile.Length > 0)
                {
                    var imageUrl = await _storageService.UploadImageAsync(imageFile, "product-images");
                    product.ImageUrl = imageUrl;
                }

                await _storageService.AddEntityAsync(product);
                TempData["Success"] = $"Product '{product.ProductName}' created successfully with price {product.Price:C}!";
                return RedirectToAction(nameof(Index));
            }

            return View(product);
        }

        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var product = await _storageService.GetEntityAsync<Product>("Product", id);
            if (product == null)
                return NotFound();

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Edit(Product product, IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                var originalProduct = await _storageService.GetEntityAsync<Product>("Product", product.RowKey);
                if (originalProduct == null)
                    return NotFound();

                originalProduct.ProductName = product.ProductName;
                originalProduct.Description = product.Description;
                originalProduct.Price = product.Price;
                originalProduct.StockAvailable = product.StockAvailable;

                if (imageFile != null && imageFile.Length > 0)
                {
                    var imageUrl = await _storageService.UploadImageAsync(imageFile, "product-images");
                    originalProduct.ImageUrl = imageUrl;
                }

                await _storageService.UpdateEntityAsync(originalProduct);
                TempData["Success"] = "Product updated successfully!";
                return RedirectToAction(nameof(Index));
            }

            return View(product);
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _storageService.DeleteEntityAsync<Product>("Product", id);
                TempData["Success"] = "Product deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting product: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        // ============================
        // Customer only - view products to buy
        // ============================
        [Authorize(Roles = "customer")]
        public async Task<IActionResult> CustomerIndex()
        {
            var products = await _storageService.GetAllEntitiesAsync<Product>();
            return View(products); // create a new view CustomerIndex.cshtml
        }
    }
}
