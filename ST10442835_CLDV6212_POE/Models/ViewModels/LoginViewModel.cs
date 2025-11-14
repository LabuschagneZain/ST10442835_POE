using System.ComponentModel.DataAnnotations;

namespace ST10442835_CLDV6212_POE.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username is required.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select your role.")]
        public string Role { get; set; } = string.Empty; 
    }
}