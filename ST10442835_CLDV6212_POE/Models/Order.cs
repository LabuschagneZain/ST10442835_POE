using Azure;
using Azure.Data.Tables;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ST10442835_CLDV6212_POE.Models
{
    public class Order : ITableEntity
    {
        [Key]
        public int Id { get; set; }

        // ===============================
        // Azure Table Storage properties
        // ===============================
        [NotMapped] // EF will ignore this
        public ETag ETag { get; set; }

        [NotMapped] // EF will ignore this
        public DateTimeOffset? Timestamp { get; set; }

        [NotMapped] // optional for EF
        public string PartitionKey { get; set; } = "Order";

        [NotMapped] // optional for EF
        public string RowKey { get; set; } = Guid.NewGuid().ToString();

        // ===============================
        // Local properties for EF / MVC
        // ===============================
        [Display(Name = "Order ID")]
        public string OrderId => RowKey;

        [Required]
        [Display(Name = "Customer")]
        public string CustomerId { get; set; } = string.Empty;

        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Product")]
        public string ProductId { get; set; } = string.Empty;

        [Display(Name = "Product Name")]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Order Date")]
        [DataType(DataType.Date)]
        public DateTime OrderDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "Quantity")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        [Display(Name = "Unit Price")]
        [DataType(DataType.Currency)]
        public double UnitPrice { get; set; }

        [Display(Name = "Total Price")]
        [DataType(DataType.Currency)]
        public double TotalPrice { get; set; }

        [Required]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Submitted";

        public enum OrderStatus
        {
            Submitted,
            Processing,
            Completed,
            Cancelled
        }
    }
}
