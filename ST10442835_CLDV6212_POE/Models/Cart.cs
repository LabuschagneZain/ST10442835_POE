using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ST10442835_CLDV6212_POE.Models
{
    [Table("Cart")]
    public class Cart
    {
        [Key]

        public int Id { get; set; }

        [Required]
        [MaxLength(100)]

        public string CustomerUsername { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]

        public string ProductId { get; set; } = string.Empty;

        [Required]

        public int Quantity { get; set; }
    }
}
