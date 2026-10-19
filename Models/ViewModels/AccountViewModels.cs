using System.ComponentModel.DataAnnotations;

namespace AgriDirect.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Address is required")]
        [StringLength(250)]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "Role selection is required")]
        [Display(Name = "Account Type")]
        public string Role { get; set; } = "Consumer"; // "Consumer" or "Farmer"

        [Display(Name = "Farm / Business Name (Optional for Farmers)")]
        public string? FarmName { get; set; }

        [Display(Name = "Farm Location / District")]
        public string? FarmLocation { get; set; }

        [Display(Name = "UPI ID (e.g. name@okhdfcbank / mobile@upi)")]
        [StringLength(100)]
        public string? UpiId { get; set; }
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email or username is required")]
        [Display(Name = "Email or Username")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember Me")]
        public bool RememberMe { get; set; } = false;

        public string? ReturnUrl { get; set; }
    }

    public class ProfileViewModel
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required, Phone]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required, StringLength(250)]
        [Display(Name = "Delivery / Business Address")]
        public string Address { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public string ProfilePictureUrl { get; set; } = "/images/default-avatar.png";

        [Display(Name = "Farm / Business Name")]
        public string? FarmName { get; set; }

        [Display(Name = "Farm Location")]
        public string? FarmLocation { get; set; }

        [Display(Name = "UPI ID (for payments)")]
        [StringLength(100)]
        public string? UpiId { get; set; }

        public double AverageRating { get; set; }
        public int TotalRatingsCount { get; set; }
        public string Status { get; set; } = "Active";
    }
}
