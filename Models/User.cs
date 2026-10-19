using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AgriDirect.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Phone, MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [MaxLength(250)]
        public string Address { get; set; } = string.Empty;

        // Roles: "Admin", "Farmer", "Consumer"
        [Required, MaxLength(20)]
        public string Role { get; set; } = "Consumer";

        public string ProfilePictureUrl { get; set; } = "/images/default-avatar.svg";

        // Status: "Active", "PendingApproval", "Blocked", "Deactivated"
        [Required, MaxLength(20)]
        public string Status { get; set; } = "Active";

        [MaxLength(150)]
        public string? FarmName { get; set; }

        [MaxLength(200)]
        public string? FarmLocation { get; set; }

        [MaxLength(100)]
        public string? UpiId { get; set; }

        public double AverageRating { get; set; } = 5.0;
        public int TotalRatingsCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
        public virtual ICollection<Order> BuyerOrders { get; set; } = new List<Order>();
        public virtual ICollection<Order> FarmerOrders { get; set; } = new List<Order>();
        public virtual ICollection<Review> GivenReviews { get; set; } = new List<Review>();
        public virtual ICollection<Review> ReceivedReviews { get; set; } = new List<Review>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}
