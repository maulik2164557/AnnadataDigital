using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AgriDirect.Models.ViewModels
{
    public class OrderCheckoutViewModel
    {
        [Required]
        public int ProductId { get; set; }
        public Product? Product { get; set; }

        [Required(ErrorMessage = "Quantity is required")]
        [Range(0.01, 100000, ErrorMessage = "Please enter a valid quantity")]
        [Display(Name = "Order Quantity")]
        public decimal Quantity { get; set; } = 1;

        [Required(ErrorMessage = "Delivery address is required")]
        [StringLength(300)]
        [Display(Name = "Shipping / Delivery Address")]
        public string DeliveryAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "Contact phone number is required")]
        [Phone]
        [Display(Name = "Contact Phone")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "CashOnDelivery"; // CashOnDelivery, Card, UPI

        [Display(Name = "Order / Delivery Instructions")]
        [StringLength(500)]
        public string? Notes { get; set; }

        public decimal UnitPrice { get; set; }
        public decimal Subtotal => Quantity * UnitPrice;

        public string? BuyerUpiId { get; set; }
    }

    public class ReviewViewModel
    {
        [Required]
        public int OrderId { get; set; }

        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? FarmerName { get; set; }

        [Required(ErrorMessage = "Please select a rating")]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars")]
        [Display(Name = "Rating")]
        public int Rating { get; set; } = 5;

        [Required(ErrorMessage = "Please write a review comment")]
        [StringLength(1000, MinimumLength = 5, ErrorMessage = "Review must be between 5 and 1000 characters")]
        [Display(Name = "Review & Feedback")]
        public string Comment { get; set; } = string.Empty;
    }

    public class OrderStatusUpdateViewModel
    {
        public int OrderId { get; set; }
        public string NewStatus { get; set; } = "Accepted"; // Pending, Accepted, Dispatched, Delivered, Cancelled
        public string? Notes { get; set; }

        // Transporter Details (required when dispatching)
        [Display(Name = "Transporter / Truck Driver Name")]
        [StringLength(150)]
        public string? TransporterName { get; set; }

        [Display(Name = "Transporter Contact Number")]
        [StringLength(50)]
        public string? TransporterContact { get; set; }
    }
}
