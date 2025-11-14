using System.ComponentModel.DataAnnotations;

namespace ST10442835_CLDV6212_POE.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        public string Username { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required, DataType(DataType.Password)]
        public string Password { get; set; }

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        public string ShippingAddress { get; set; }

        [Required]
        public string Role { get; set; } = "customer";
    }
}

