using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AgriDirect.Data;
using AgriDirect.Models.ViewModels;
using AgriDirect.Services;

namespace AgriDirect.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ISystemAutomationService _automationService;

        public AdminController(AppDbContext context, ISystemAutomationService automationService)
        {
            _context = context;
            _automationService = automationService;
        }

        // GET: /Admin/Dashboard
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalFarmers = await _context.Users.CountAsync(u => u.Role == "Farmer");
            var totalConsumers = await _context.Users.CountAsync(u => u.Role == "Consumer");
            var totalProducts = await _context.Products.CountAsync(p => p.Status != "Removed");
            var pendingApprovals = await _context.Users.CountAsync(u => u.Status == "PendingApproval");

            var recentUsers = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .Take(6)
                .ToListAsync();

            var recentProducts = await _context.Products
                .Include(p => p.Farmer)
                .OrderByDescending(p => p.CreatedAt)
                .Take(6)
                .ToListAsync();

            var pendingFarmersList = await _context.Users
                .Where(u => u.Role == "Farmer" && u.Status == "PendingApproval")
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            var viewModel = new AdminDashboardViewModel
            {
                TotalUsers = totalUsers,
                TotalFarmers = totalFarmers,
                TotalConsumers = totalConsumers,
                TotalProducts = totalProducts,
                PendingUserApprovals = pendingApprovals,
                RecentUsers = recentUsers,
                RecentProducts = recentProducts,
                PendingFarmers = pendingFarmersList
            };

            return View(viewModel);
        }

        // GET: /Admin/Users (R.7.1)
        [HttpGet]
        public async Task<IActionResult> Users(string? role, string? status, string? search)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(role) && role != "All")
            {
                query = query.Where(u => u.Role == role);
            }
            if (!string.IsNullOrWhiteSpace(status) && status != "All")
            {
                query = query.Where(u => u.Status == status);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower().Trim();
                query = query.Where(u => u.FullName.ToLower().Contains(s) || u.Email.ToLower().Contains(s));
            }

            var users = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();
            ViewBag.CurrentRole = role;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentSearch = search;

            return View(users);
        }

        // POST: /Admin/UpdateUserStatus (R.7.1)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserStatus(int userId, string status)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (user.Role == "Admin")
            {
                TempData["Error"] = "Cannot modify Administrator status.";
                return RedirectToAction(nameof(Users));
            }

            user.Status = status;
            await _context.SaveChangesAsync();

            await _automationService.SendNotificationAsync(
                user.Id,
                "Account Status Update",
                $"Your account status has been updated to '{status}' by administration.",
                "Account"
            );

            TempData["Success"] = $"User '{user.FullName}' status updated to {status}.";
            return RedirectToAction(nameof(Users));
        }

        // POST: /Admin/UpdateUserRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserRole(int userId, string newRole)
        {
            if (newRole != "Consumer" && newRole != "Farmer" && newRole != "Admin")
            {
                TempData["Error"] = "Invalid role specified.";
                return RedirectToAction(nameof(Users));
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            user.Role = newRole;
            await _context.SaveChangesAsync();

            await _automationService.SendNotificationAsync(
                user.Id,
                "Account Role Updated",
                $"Your account role has been updated to '{newRole}' by an Administrator.",
                "Account"
            );

            TempData["Success"] = $"User '{user.FullName}' role changed to '{newRole}'.";
            return RedirectToAction(nameof(Users));
        }

        // POST: /Admin/DeleteUser (R.7.1 & R.7.4 - Deletes or deactivates user accounts upon request)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (user.Role == "Admin")
            {
                TempData["Error"] = "Cannot delete Administrator accounts.";
                return RedirectToAction(nameof(Users));
            }

            user.Status = "Deactivated";
            user.FullName = "[Deleted User]";
            await _context.SaveChangesAsync();

            TempData["Success"] = "User account has been deleted/deactivated.";
            return RedirectToAction(nameof(Users));
        }

        // GET: /Admin/Products (R.7.2)
        [HttpGet]
        public async Task<IActionResult> Products(string? category, string? status)
        {
            var query = _context.Products
                .Include(p => p.Farmer)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(category) && category != "All")
            {
                query = query.Where(p => p.Category == category);
            }
            if (!string.IsNullOrWhiteSpace(status) && status != "All")
            {
                query = query.Where(p => p.Status == status);
            }

            var products = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
            ViewBag.CurrentCategory = category;
            ViewBag.CurrentStatus = status;

            return View(products);
        }

        // POST: /Admin/UpdateProductStatus (R.7.2)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProductStatus(int productId, string status)
        {
            var product = await _context.Products
                .Include(p => p.Farmer)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null) return NotFound();

            product.Status = status;
            await _context.SaveChangesAsync();

            await _automationService.SendNotificationAsync(
                product.FarmerId,
                "Product Status Update",
                $"Your product listing '{product.Name}' status is now '{status}'.",
                "System",
                $"/Product/Details/{product.Id}"
            );

            TempData["Success"] = $"Product '{product.Name}' marked as {status}.";
            return RedirectToAction(nameof(Products));
        }

        // POST: /Admin/DeleteProduct (R.7.2 - Remove product listing)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int productId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return NotFound();

            product.Status = "Removed";
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Product '{product.Name}' removed from marketplace.";
            return RedirectToAction(nameof(Products));
        }

        // GET: /Admin/Orders (R.7.3)
        [HttpGet]
        public async Task<IActionResult> Orders(string? orderStatus, string? paymentStatus)
        {
            var query = _context.Orders
                .Include(o => o.Buyer)
                .Include(o => o.Farmer)
                .Include(o => o.Product)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(orderStatus) && orderStatus != "All")
            {
                query = query.Where(o => o.OrderStatus == orderStatus);
            }
            if (!string.IsNullOrWhiteSpace(paymentStatus) && paymentStatus != "All")
            {
                query = query.Where(o => o.PaymentStatus == paymentStatus);
            }

            var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
            ViewBag.CurrentOrderStatus = orderStatus;
            ViewBag.CurrentPaymentStatus = paymentStatus;

            return View(orders);
        }

        // POST: /Admin/UpdatePaymentStatus (R.7.3)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePaymentStatus(int orderId, string paymentStatus)
        {
            await _automationService.ProcessPaymentStatusAsync(orderId, paymentStatus);
            TempData["Success"] = $"Payment status updated to {paymentStatus}.";
            return RedirectToAction(nameof(Orders));
        }

        // GET: /Admin/Reports (Generate Reports)
        [HttpGet]
        public async Task<IActionResult> Reports()
        {
            var orders = await _context.Orders
                .Include(o => o.Product)
                .Include(o => o.Farmer)
                .ToListAsync();

            var totalSales = orders.Where(o => o.PaymentStatus == "Paid").Sum(o => o.TotalAmount);
            var completedCount = orders.Count(o => o.OrderStatus == "Delivered");
            var pendingCount = orders.Count(o => o.OrderStatus == "Pending" || o.OrderStatus == "Accepted" || o.OrderStatus == "Packed" || o.OrderStatus == "Shipped");
            var cancelledCount = orders.Count(o => o.OrderStatus == "Cancelled");

            var productsByCategory = await _context.Products
                .GroupBy(p => p.Category)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Category, x => x.Count);

            var salesByCategory = orders
                .Where(o => o.Product != null && o.PaymentStatus == "Paid")
                .GroupBy(o => o.Product!.Category)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.TotalAmount));

            var farmers = await _context.Users
                .Where(u => u.Role == "Farmer")
                .Include(f => f.FarmerOrders)
                .ToListAsync();

            var topFarmers = farmers
                .Select(f => new FarmerPerformanceReportItem
                {
                    FarmerName = f.FullName,
                    FarmName = f.FarmName ?? "N/A",
                    TotalOrders = f.FarmerOrders.Count,
                    TotalRevenue = f.FarmerOrders.Where(o => o.PaymentStatus == "Paid").Sum(o => o.TotalAmount),
                    AverageRating = f.AverageRating
                })
                .OrderByDescending(f => f.TotalRevenue)
                .Take(10)
                .ToList();

            var viewModel = new ReportsViewModel
            {
                TotalSalesRevenue = totalSales,
                CompletedOrdersCount = completedCount,
                PendingOrdersCount = pendingCount,
                CancelledOrdersCount = cancelledCount,
                ProductsByCategory = productsByCategory,
                SalesByCategory = salesByCategory,
                TopFarmers = topFarmers
            };

            return View(viewModel);
        }
    }
}
