using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ST10442835_CLDV6212_POE.Models
{

    [Table("Users")] 

    public class User
    {

        [Key]

        public int Id { get; set; }

        [Required(ErrorMessage = "Username is required")]
        [MaxLength(100)]
        [Display(Name = "Username")]

        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MaxLength(256)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]

        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        [Display(Name = "Role")]

        public string Role { get; set; } = "customer"; 
    }

}
