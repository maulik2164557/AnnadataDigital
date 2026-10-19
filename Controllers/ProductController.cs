using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AgriDirect.Data;
using AgriDirect.Models;
using AgriDirect.Models.ViewModels;

namespace AgriDirect.Controllers
{
    public class ProductController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ProductController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // GET: /Product/MyProducts (Farmer only)
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> MyProducts()
        {
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Products", "Admin");
            }
            if (!User.IsInRole("Farmer"))
            {
                return Forbid();
            }

            var farmerId = GetCurrentUserId();
            var products = await _context.Products
                .Where(p => p.FarmerId == farmerId && p.Status != "Removed")
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(products);
        }

        // GET: /Product/Create (Farmer only)
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Products", "Admin");
            }
            if (!User.IsInRole("Farmer"))
            {
                return Forbid();
            }

            var farmerId = GetCurrentUserId();
            var farmer = await _context.Users.FindAsync(farmerId);
            if (farmer == null || farmer.Status != "Active")
            {
                TempData["Error"] = "Your farmer profile is currently pending Admin approval. You can add produce once your profile is approved.";
                return RedirectToAction("FarmerDashboard", "Account");
            }

            return View(new ProductFormViewModel());
        }

        // POST: /Product/Create
        [Authorize(Roles = "Farmer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductFormViewModel model)
        {
            var farmerId = GetCurrentUserId();
            var farmer = await _context.Users.FindAsync(farmerId);
            if (farmer == null || farmer.Status != "Active")
            {
                TempData["Error"] = "Your farmer profile is currently pending Admin approval. You can add produce once your profile is approved.";
                return RedirectToAction("FarmerDashboard", "Account");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string imageUrl = "/images/default-product.svg";
            var additionalImagesList = new List<string>();

            var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "products");
            Directory.CreateDirectory(uploadsDir);

            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                var fileName = $"prod_{farmerId}_{Guid.NewGuid().ToString()[..8]}{Path.GetExtension(model.ImageFile.FileName)}";
                var filePath = Path.Combine(uploadsDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ImageFile.CopyToAsync(stream);
                }

                imageUrl = $"/uploads/products/{fileName}";
            }

            if (model.AdditionalImageFiles != null)
            {
                foreach (var file in model.AdditionalImageFiles)
                {
                    if (file.Length > 0)
                    {
                        var fileName = $"prod_{farmerId}_{Guid.NewGuid().ToString()[..8]}{Path.GetExtension(file.FileName)}";
                        var filePath = Path.Combine(uploadsDir, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        additionalImagesList.Add($"/uploads/products/{fileName}");
                    }
                }
            }

            if (imageUrl == "/images/default-product.svg" && additionalImagesList.Count > 0)
            {
                imageUrl = additionalImagesList[0];
            }

            // Automatically set measurement unit based on category: Grains -> quintal, Vegetables -> kg
            string productUnit = model.Category switch
            {
                "Grains" => "quintal",
                "Vegetables" => "kg",
                _ => string.IsNullOrWhiteSpace(model.Unit) ? "kg" : model.Unit
            };

            var product = new Product
            {
                FarmerId = farmerId,
                Name = model.Name.Trim(),
                Category = model.Category,
                Price = model.Price,
                QuantityInStock = model.QuantityInStock,
                Unit = productUnit,
                HarvestDate = model.HarvestDate,
                Description = model.Description?.Trim() ?? string.Empty,
                ImageUrl = imageUrl,
                AdditionalImageUrls = additionalImagesList.Count > 0 ? string.Join(";", additionalImagesList) : null,
                Status = "Approved", // Auto-approved or pending admin review
                CreatedAt = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Product '{product.Name}' added successfully to the marketplace!";
            return RedirectToAction(nameof(MyProducts));
        }

        // GET: /Product/Edit/{id}
        [Authorize(Roles = "Farmer")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var farmerId = GetCurrentUserId();
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.FarmerId == farmerId);
            if (product == null)
            {
                return NotFound();
            }

            var model = new ProductFormViewModel
            {
                Id = product.Id,
                Name = product.Name,
                Category = product.Category,
                Price = product.Price,
                QuantityInStock = product.QuantityInStock,
                Unit = product.Unit,
                HarvestDate = product.HarvestDate,
                Description = product.Description,
                ExistingImageUrl = product.ImageUrl,
                ExistingAdditionalImageUrls = product.AdditionalImageUrls
            };

            return View(model);
        }

        // POST: /Product/Edit/{id}
        [Authorize(Roles = "Farmer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductFormViewModel model)
        {
            if (id != model.Id) return BadRequest();

            var farmerId = GetCurrentUserId();
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.FarmerId == farmerId);
            if (product == null) return NotFound();

            if (!ModelState.IsValid)
            {
                model.ExistingImageUrl = product.ImageUrl;
                model.ExistingAdditionalImageUrls = product.AdditionalImageUrls;
                return View(model);
            }

            var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "products");
            Directory.CreateDirectory(uploadsDir);

            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                var fileName = $"prod_{farmerId}_{Guid.NewGuid().ToString()[..8]}{Path.GetExtension(model.ImageFile.FileName)}";
                var filePath = Path.Combine(uploadsDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ImageFile.CopyToAsync(stream);
                }

                product.ImageUrl = $"/uploads/products/{fileName}";
            }

            if (model.AdditionalImageFiles != null && model.AdditionalImageFiles.Count > 0)
            {
                var additionalImagesList = new List<string>();
                if (!string.IsNullOrWhiteSpace(product.AdditionalImageUrls))
                {
                    additionalImagesList.AddRange(product.AdditionalImageUrls.Split(';', StringSplitOptions.RemoveEmptyEntries));
                }

                foreach (var file in model.AdditionalImageFiles)
                {
                    if (file.Length > 0)
                    {
                        var fileName = $"prod_{farmerId}_{Guid.NewGuid().ToString()[..8]}{Path.GetExtension(file.FileName)}";
                        var filePath = Path.Combine(uploadsDir, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        additionalImagesList.Add($"/uploads/products/{fileName}");
                    }
                }

                product.AdditionalImageUrls = string.Join(";", additionalImagesList);
            }

            string productUnit = model.Category switch
            {
                "Grains" => "quintal",
                "Vegetables" => "kg",
                _ => string.IsNullOrWhiteSpace(model.Unit) ? product.Unit : model.Unit
            };

            product.Name = model.Name.Trim();
            product.Category = model.Category;
            product.Price = model.Price;
            product.QuantityInStock = model.QuantityInStock;
            product.Unit = productUnit;
            product.HarvestDate = model.HarvestDate;
            product.Description = model.Description?.Trim() ?? string.Empty;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Product '{product.Name}' updated successfully!";
            return RedirectToAction(nameof(MyProducts));
        }

        // POST: /Product/Delete/{id}
        [Authorize(Roles = "Farmer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var farmerId = GetCurrentUserId();
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.FarmerId == farmerId);
            if (product == null) return NotFound();

            // Soft-delete: mark status as Removed
            product.Status = "Removed";
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Product '{product.Name}' was removed from the marketplace.";
            return RedirectToAction(nameof(MyProducts));
        }

        // GET: /Product/Details/{id} (R.2.5 - View Product Details)
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Farmer)
                .Include(p => p.Reviews)
                    .ThenInclude(r => r.Buyer)
                .FirstOrDefaultAsync(p => p.Id == id && p.Status != "Removed");

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : 0;
        }
    }
}
