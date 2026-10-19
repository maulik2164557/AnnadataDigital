using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriDirect.Models
{
    public class Order
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string OrderNumber { get; set; } = string.Empty;

        [Required]
        public int BuyerId { get; set; }

        [ForeignKey("BuyerId")]
        public virtual User? Buyer { get; set; }

        [Required]
        public int FarmerId { get; set; }

        [ForeignKey("FarmerId")]
        public virtual User? Farmer { get; set; }

        [Required]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }

        [Required]
        [Range(0.01, 1000000)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        // OrderStatus: "Pending", "Accepted", "Packed", "Shipped", "Delivered", "Cancelled"
        [Required, MaxLength(30)]
        public string OrderStatus { get; set; } = "Pending";

        // PaymentStatus: "Pending", "Paid", "Failed"
        [Required, MaxLength(30)]
        public string PaymentStatus { get; set; } = "Pending";

        [Required, MaxLength(50)]
        public string PaymentMethod { get; set; } = "CashOnDelivery"; // Card, UPI, CashOnDelivery

        [Required, MaxLength(300)]
        public string DeliveryAddress { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? TrackingNotes { get; set; }

        // Transporter Details
        [MaxLength(150)]
        public string? TransporterName { get; set; }

        [MaxLength(50)]
        public string? TransporterContact { get; set; }

        // Lifecycle Timestamps
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? DispatchedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}
