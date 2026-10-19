using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AgriDirect.Data;
using AgriDirect.Models;
using AgriDirect.Models.ViewModels;
using AgriDirect.Services;

namespace AgriDirect.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ISystemAutomationService _automationService;

        public OrderController(AppDbContext context, ISystemAutomationService automationService)
        {
            _context = context;
            _automationService = automationService;
        }

        // GET: /Order/Checkout/{productId} (R.5.1)
        [Authorize(Roles = "Consumer")]
        [HttpGet]
        public async Task<IActionResult> Checkout(int productId, decimal quantity = 1)
        {
            var product = await _context.Products
                .Include(p => p.Farmer)
                .FirstOrDefaultAsync(p => p.Id == productId && p.Status == "Approved");

            if (product == null) return NotFound();

            var buyerId = GetCurrentUserId();
            var buyer = await _context.Users.FindAsync(buyerId);

            if (buyer == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(buyer.UpiId))
            {
                TempData["Error"] = "Please add your UPI ID in your profile before placing an order.";
                return RedirectToAction("Profile", "Account", new { returnUrl = Url.Action("Checkout", "Order", new { productId = productId, quantity = quantity }) });
            }

            var model = new OrderCheckoutViewModel
            {
                ProductId = product.Id,
                Product = product,
                Quantity = quantity > 0 ? quantity : 1,
                UnitPrice = product.Price,
                DeliveryAddress = buyer.Address ?? "",
                PhoneNumber = buyer.PhoneNumber ?? "",
                PaymentMethod = "CashOnDelivery",
                BuyerUpiId = buyer.UpiId
            };

            return View(model);
        }

        // POST: /Order/PlaceOrder (R.5.1)
        [Authorize(Roles = "Consumer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(OrderCheckoutViewModel model)
        {
            var product = await _context.Products
                .Include(p => p.Farmer)
                .FirstOrDefaultAsync(p => p.Id == model.ProductId && p.Status == "Approved");

            if (product == null) return NotFound();

            if (model.Quantity <= 0)
            {
                ModelState.AddModelError("Quantity", "Order quantity must be greater than zero.");
            }

            if (model.Quantity > product.QuantityInStock)
            {
                ModelState.AddModelError("Quantity", $"Requested quantity ({model.Quantity} {product.Unit}) exceeds available inventory ({product.QuantityInStock} {product.Unit}).");
            }

            var buyerId = GetCurrentUserId();
            var buyer = await _context.Users.FindAsync(buyerId);
            if (buyer == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(buyer.UpiId))
            {
                TempData["Error"] = "Please add your UPI ID in your profile before placing an order.";
                return RedirectToAction("Profile", "Account", new { returnUrl = Url.Action("Checkout", "Order", new { productId = model.ProductId, quantity = model.Quantity }) });
            }

            if (!ModelState.IsValid)
            {
                model.Product = product;
                model.UnitPrice = product.Price;
                model.BuyerUpiId = buyer.UpiId;
                return View("Checkout", model);
            }

            var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
            var totalAmount = model.Quantity * product.Price;

            var order = new Order
            {
                OrderNumber = orderNumber,
                BuyerId = buyerId,
                FarmerId = product.FarmerId,
                ProductId = product.Id,
                Quantity = model.Quantity,
                UnitPrice = product.Price,
                TotalAmount = totalAmount,
                OrderStatus = "Pending", // initial status Pending farmer confirmation
                PaymentStatus = "Pending", // Payment is pending until confirmed/paid
                PaymentMethod = model.PaymentMethod,
                DeliveryAddress = model.DeliveryAddress.Trim(),
                TrackingNotes = string.IsNullOrWhiteSpace(model.Notes) ? "Order placed by buyer." : model.Notes.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Deduct product stock
            product.QuantityInStock -= model.Quantity;

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Send automated notification to farmer for the incoming order
            await _automationService.SendNotificationAsync(
                product.FarmerId,
                "🛒 New Farm Order Received!",
                $"New order #{order.OrderNumber} received from {buyer.FullName} for {order.Quantity} {product.Unit} of {product.Name}. Please review and confirm the order.",
                "Order",
                $"/Order/Details/{order.Id}"
            );

            // Send automated notification to consumer that order is placed
            await _automationService.SendNotificationAsync(
                buyerId,
                "📋 Order Placed Successfully",
                $"Your order #{order.OrderNumber} for {order.Quantity} {product.Unit} of {product.Name} has been submitted and is awaiting confirmation from farmer {product.Farmer?.FullName}.",
                "Order",
                $"/Order/Details/{order.Id}"
            );

            TempData["Success"] = $"Order #{order.OrderNumber} placed successfully! The farmer has been notified to confirm your order.";
            return RedirectToAction("Details", new { id = order.Id });
        }

        // GET: /Order/History (R.5.3 - Buyer Order History)
        [HttpGet]
        public async Task<IActionResult> History()
        {
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Orders", "Admin");
            }

            var userId = GetCurrentUserId();
            var orders = await _context.Orders
                .Include(o => o.Product)
                .Include(o => o.Farmer)
                .Include(o => o.Reviews)
                .Where(o => o.BuyerId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // GET: /Order/FarmerOrders (Farmer Order Fulfillment Dashboard)
        [HttpGet]
        public async Task<IActionResult> FarmerOrders()
        {
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Orders", "Admin");
            }
            if (!User.IsInRole("Farmer"))
            {
                return Forbid();
            }

            var farmerId = GetCurrentUserId();
            var orders = await _context.Orders
                .Include(o => o.Product)
                .Include(o => o.Buyer)
                .Where(o => o.FarmerId == farmerId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // GET: /Order/Details/{id} (R.5.2 - Order Tracking View for Buyer & Farmer)
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetCurrentUserId();
            var order = await _context.Orders
                .Include(o => o.Product)
                .Include(o => o.Farmer)
                .Include(o => o.Buyer)
                .Include(o => o.Reviews)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            // Authorization check
            bool isAdmin = User.IsInRole("Admin");
            if (!isAdmin && order.BuyerId != userId && order.FarmerId != userId)
            {
                return Forbid();
            }

            return View(order);
        }

        // POST: /Order/ConfirmOrder (Farmer confirms the order)
        [Authorize(Roles = "Farmer,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmOrder(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Product)
                .Include(o => o.Farmer)
                .Include(o => o.Buyer)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && order.FarmerId != currentUserId)
            {
                return Forbid();
            }

            var farmer = await _context.Users.FindAsync(order.FarmerId);
            if (farmer == null) return NotFound();

            // Requirement: Farmer must have UPI ID in profile before confirming order
            if (string.IsNullOrWhiteSpace(farmer.UpiId))
            {
                TempData["Error"] = "You must edit your profile and add your UPI ID before confirming this order so the consumer can make payment.";
                return RedirectToAction("Profile", "Account", new { returnUrl = Url.Action("Details", "Order", new { id = order.Id }) });
            }

            if (order.OrderStatus != "Pending")
            {
                TempData["Error"] = $"Order #{order.OrderNumber} is already in '{order.OrderStatus}' status.";
                return RedirectToAction(nameof(Details), new { id = order.Id });
            }

            order.OrderStatus = "Accepted";
            order.ConfirmedAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;
            order.TrackingNotes = "Order confirmed by farmer.";

            await _context.SaveChangesAsync();

            // Notify consumer that order is confirmed
            string paymentNote = (order.PaymentMethod == "UPI")
                ? $" Your order #{order.OrderNumber} is confirmed! Please pay ₹{order.TotalAmount:0.##} via UPI to farmer's UPI ID: {farmer.UpiId} and click 'Payment Completed'."
                : $" Your Cash on Delivery order #{order.OrderNumber} is confirmed! The farmer is preparing your produce for dispatch.";

            await _automationService.SendNotificationAsync(
                order.BuyerId,
                "🎉 Order Confirmed by Farmer!",
                $"Great news! Farmer {farmer.FullName} has confirmed your order #{order.OrderNumber}.{paymentNote}",
                "Order",
                $"/Order/Details/{order.Id}"
            );

            TempData["Success"] = $"Order #{order.OrderNumber} confirmed successfully! Consumer has been notified.";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        // POST: /Order/DispatchOrder (Farmer dispatches order with transporter details)
        [Authorize(Roles = "Farmer,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DispatchOrder(int orderId, string transporterName, string transporterContact, string? notes)
        {
            var order = await _context.Orders
                .Include(o => o.Product)
                .Include(o => o.Farmer)
                .Include(o => o.Buyer)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && order.FarmerId != currentUserId)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(transporterName) || string.IsNullOrWhiteSpace(transporterContact))
            {
                TempData["Error"] = "Please provide both Transporter/Driver Name and Contact Number before dispatching the order.";
                return RedirectToAction(nameof(Details), new { id = order.Id });
            }

            // If online UPI and payment not completed, farmer should verify payment
            if (order.PaymentMethod == "UPI" && order.PaymentStatus != "Paid")
            {
                TempData["Warning"] = "Note: Buyer has not marked payment completed yet. You may proceed if payment received offline.";
            }

            order.OrderStatus = "Dispatched";
            order.TransporterName = transporterName.Trim();
            order.TransporterContact = transporterContact.Trim();
            order.DispatchedAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(notes))
            {
                order.TrackingNotes = notes.Trim();
            }
            else
            {
                order.TrackingNotes = $"Dispatched with Transporter {order.TransporterName} ({order.TransporterContact}).";
            }

            await _context.SaveChangesAsync();

            // Notify consumer that order is dispatched with transporter details
            await _automationService.SendNotificationAsync(
                order.BuyerId,
                "🚚 Order Dispatched!",
                $"Your order #{order.OrderNumber} has been dispatched! Transporter: {order.TransporterName}, Phone: {order.TransporterContact}. You can now download your Invoice Bill.",
                "Order",
                $"/Order/Details/{order.Id}"
            );

            TempData["Success"] = $"Order #{order.OrderNumber} dispatched! Transporter details saved and buyer notified.";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        // POST: /Order/MarkDelivered (Consumer marks order as delivered)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkDelivered(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Product)
                .Include(o => o.Farmer)
                .Include(o => o.Buyer)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            bool isBuyer = (order.BuyerId == currentUserId);
            bool isAdmin = User.IsInRole("Admin");

            if (!isBuyer && !isAdmin)
            {
                return Forbid();
            }

            if (order.OrderStatus == "Delivered")
            {
                TempData["Info"] = "Order is already marked as Delivered.";
                return RedirectToAction(nameof(Details), new { id = order.Id });
            }

            order.OrderStatus = "Delivered";
            order.DeliveredAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;
            order.TrackingNotes = "Order delivered and confirmed by consumer.";

            await _context.SaveChangesAsync();

            // Notify farmer that order has been delivered
            await _automationService.SendNotificationAsync(
                order.FarmerId,
                "📦 Order Delivered!",
                $"Consumer {order.Buyer?.FullName} has confirmed receipt of order #{order.OrderNumber} ({order.Quantity} {order.Product?.Unit} of {order.Product?.Name}).",
                "Order",
                $"/Order/Details/{order.Id}"
            );

            TempData["Success"] = $"Order #{order.OrderNumber} marked as Delivered! Thank you for confirming.";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        // POST: /Order/CompletePayment (Consumer marks online UPI or COD payment completed)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompletePayment(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Product)
                .Include(o => o.Farmer)
                .Include(o => o.Buyer)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            bool isBuyer = (order.BuyerId == currentUserId);
            bool isFarmer = (order.FarmerId == currentUserId);
            bool isAdmin = User.IsInRole("Admin");

            if (!isBuyer && !isFarmer && !isAdmin)
            {
                return Forbid();
            }

            order.PaymentStatus = "Paid";
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Notify farmer that consumer has completed payment
            await _automationService.SendNotificationAsync(
                order.FarmerId,
                "💰 Payment Completed by Consumer",
                $"Payment of ₹{order.TotalAmount:0.##} for order #{order.OrderNumber} has been marked completed by buyer {order.Buyer?.FullName} on your UPI ID ({order.Farmer?.UpiId ?? "UPI"}).",
                "Payment",
                $"/Order/Details/{order.Id}"
            );

            // Notify buyer
            await _automationService.SendNotificationAsync(
                order.BuyerId,
                "✅ Payment Recorded",
                $"Your payment of ₹{order.TotalAmount:0.##} for order #{order.OrderNumber} has been recorded.",
                "Payment",
                $"/Order/Details/{order.Id}"
            );

            TempData["Success"] = $"Payment for order #{order.OrderNumber} recorded as completed. Farmer has been notified.";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        // GET: /Order/Invoice/{id} (Consumer or Farmer downloads/views printable Invoice Bill)
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Invoice(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Product)
                .Include(o => o.Farmer)
                .Include(o => o.Buyer)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            bool isBuyer = (order.BuyerId == currentUserId);
            bool isFarmer = (order.FarmerId == currentUserId);
            bool isAdmin = User.IsInRole("Admin");

            if (!isBuyer && !isFarmer && !isAdmin)
            {
                return Forbid();
            }

            return View(order);
        }

        // POST: /Order/UpdateStatus (Legacy & Admin/Farmer multi-status updater)
        [Authorize(Roles = "Farmer,Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(OrderStatusUpdateViewModel model)
        {
            var order = await _context.Orders
                .Include(o => o.Product)
                .Include(o => o.Buyer)
                .Include(o => o.Farmer)
                .FirstOrDefaultAsync(o => o.Id == model.OrderId);

            if (order == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            if (!User.IsInRole("Admin") && order.FarmerId != currentUserId)
            {
                return Forbid();
            }

            var validStatuses = new[] { "Pending", "Accepted", "Dispatched", "Delivered", "Cancelled" };
            if (!validStatuses.Contains(model.NewStatus))
            {
                TempData["Error"] = "Invalid status specified.";
                return RedirectToAction(nameof(Details), new { id = model.OrderId });
            }

            if (model.NewStatus == "Accepted")
            {
                var farmer = await _context.Users.FindAsync(order.FarmerId);
                if (farmer == null || string.IsNullOrWhiteSpace(farmer.UpiId))
                {
                    TempData["Error"] = "You must edit your profile and add your UPI ID before confirming this order so the consumer can make payment.";
                    return RedirectToAction("Profile", "Account", new { returnUrl = Url.Action("Details", "Order", new { id = order.Id }) });
                }
                order.ConfirmedAt = DateTime.UtcNow;
            }

            if (model.NewStatus == "Dispatched")
            {
                if (string.IsNullOrWhiteSpace(model.TransporterName) || string.IsNullOrWhiteSpace(model.TransporterContact))
                {
                    TempData["Error"] = "Transporter Name and Contact Number are required to dispatch this order.";
                    return RedirectToAction(nameof(Details), new { id = model.OrderId });
                }
                order.TransporterName = model.TransporterName.Trim();
                order.TransporterContact = model.TransporterContact.Trim();
                order.DispatchedAt = DateTime.UtcNow;
            }

            if (model.NewStatus == "Delivered")
            {
                order.DeliveredAt = DateTime.UtcNow;
            }

            order.OrderStatus = model.NewStatus;
            order.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(model.Notes))
            {
                order.TrackingNotes = model.Notes.Trim();
            }

            await _context.SaveChangesAsync();

            // Send notification to buyer
            await _automationService.SendNotificationAsync(
                order.BuyerId,
                $"📦 Order Status: {model.NewStatus}",
                $"Your order #{order.OrderNumber} status is now '{model.NewStatus}'. {(string.IsNullOrWhiteSpace(model.Notes) ? "" : "Note: " + model.Notes)}",
                "Order",
                $"/Order/Details/{order.Id}"
            );

            TempData["Success"] = $"Order #{order.OrderNumber} status updated to {model.NewStatus}.";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        // POST: /Order/CancelOrder (Buyer can cancel order ONLY IF not dispatched)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            bool isAdmin = User.IsInRole("Admin");

            if (!isAdmin && order.BuyerId != currentUserId && order.FarmerId != currentUserId)
            {
                return Forbid();
            }

            // Requirement: If consumer wants to Cancel the order he/she can only cancel if not dispatched.
            // If order is dispatched, cannot cancel.
            if (order.OrderStatus == "Dispatched" || order.OrderStatus == "Delivered" || order.OrderStatus == "Cancelled")
            {
                TempData["Error"] = $"Cannot cancel order #{order.OrderNumber} because it is already '{order.OrderStatus}'. Orders cannot be cancelled after dispatch.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            order.OrderStatus = "Cancelled";
            order.UpdatedAt = DateTime.UtcNow;
            order.TrackingNotes = "Order was cancelled by buyer.";

            // Return stock back to inventory
            if (order.Product != null)
            {
                order.Product.QuantityInStock += order.Quantity;
            }

            await _context.SaveChangesAsync();

            // Send automated notifications
            await _automationService.SendNotificationAsync(
                order.FarmerId,
                "Order Cancelled",
                $"Order #{order.OrderNumber} has been cancelled. Inventory was restored.",
                "Order",
                $"/Order/Details/{order.Id}"
            );

            await _automationService.SendNotificationAsync(
                order.BuyerId,
                "Order Cancelled",
                $"Your order #{order.OrderNumber} has been successfully cancelled.",
                "Order",
                $"/Order/Details/{order.Id}"
            );

            TempData["Success"] = $"Order #{order.OrderNumber} has been cancelled.";
            return RedirectToAction(nameof(Details), new { id = orderId });
        }

        // POST: /Order/ConfirmPayment (Farmer, Buyer or Admin payment confirmation)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int orderId, string paymentStatus = "Paid")
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            bool isAdmin = User.IsInRole("Admin");
            bool isFarmer = (order.FarmerId == currentUserId);
            bool isBuyer = (order.BuyerId == currentUserId);

            if (!isAdmin && !isFarmer && !isBuyer)
            {
                return Forbid();
            }

            await _automationService.ProcessPaymentStatusAsync(orderId, paymentStatus);

            TempData["Success"] = $"Payment for order #{order.OrderNumber} has been recorded as {paymentStatus}.";
            return RedirectToAction(nameof(Details), new { id = orderId });
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : 0;
        }
    }
}
