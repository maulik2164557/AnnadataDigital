using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace AgriDirect.Models.ViewModels
{
    public class ProductFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Product name is required")]
        [StringLength(150)]
        [Display(Name = "Product Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required")]
        public string Category { get; set; } = "Vegetables";

        [Required(ErrorMessage = "Price per unit is required")]
        [Range(0.01, 1000000, ErrorMessage = "Price must be greater than 0")]
        [Display(Name = "Price (₹)")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Quantity in stock is required")]
        [Range(0.01, 1000000, ErrorMessage = "Quantity must be greater than 0")]
        [Display(Name = "Quantity Available")]
        public decimal QuantityInStock { get; set; }

        [Required(ErrorMessage = "Unit is required")]
        [Display(Name = "Unit of Measure")]
        public string Unit { get; set; } = "kg"; // kg, quintal, crate, box, liter, dozen

        [Required(ErrorMessage = "Harvest date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Harvest Date")]
        public DateTime HarvestDate { get; set; } = DateTime.UtcNow.Date;

        [StringLength(2000)]
        public string? Description { get; set; } = string.Empty;

        [Display(Name = "Primary Product Image")]
        public IFormFile? ImageFile { get; set; }

        [Display(Name = "Additional Product Images")]
        public List<IFormFile>? AdditionalImageFiles { get; set; }

        public string? ExistingImageUrl { get; set; }
        public string? ExistingAdditionalImageUrls { get; set; }
    }

    public class MarketplaceFilterViewModel
    {
        public string? Keyword { get; set; }
        public string? Category { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? Location { get; set; }
        public bool OnlyAvailable { get; set; } = true;
        public string? SortBy { get; set; } = "newest"; // newest, price_low, price_high, rating

        public List<Product> Products { get; set; } = new List<Product>();
        public List<string> AvailableCategories { get; set; } = new List<string>();
        public List<string> AvailableLocations { get; set; } = new List<string>();
    }
}
