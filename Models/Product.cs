using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriDirect.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int FarmerId { get; set; }

        [ForeignKey("FarmerId")]
        public virtual User? Farmer { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Category { get; set; } = "Vegetables";

        [Required]
        [Range(0.01, 1000000)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Required]
        [Range(0, 1000000)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal QuantityInStock { get; set; }

        [Required, MaxLength(20)]
        public string Unit { get; set; } = "kg"; // kg, quintal, crate, box, liter, dozen

        [Required]
        public DateTime HarvestDate { get; set; } = DateTime.UtcNow.Date;

        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = "/images/default-product.svg";
        public string? AdditionalImageUrls { get; set; }

        [NotMapped]
        public List<string> AllImageUrls
        {
            get
            {
                var list = new List<string>();
                if (!string.IsNullOrWhiteSpace(ImageUrl)) list.Add(ImageUrl);
                if (!string.IsNullOrWhiteSpace(AdditionalImageUrls))
                {
                    var extras = AdditionalImageUrls.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var img in extras)
                    {
                        if (!list.Contains(img)) list.Add(img);
                    }
                }
                return list;
            }
        }

        // Status: "Approved", "Pending", "Rejected", "Removed"
        [Required, MaxLength(20)]
        public string Status { get; set; } = "Approved";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}
