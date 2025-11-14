using System.ComponentModel.DataAnnotations;

namespace ST10442835_CLDV6212_POE.Models
{
    public class FileUploadModel
    {
        [Required]
        [Display(Name = "Proof of Payment")]
        public IFormFile ProofOfPayment { get; set; }

        [Display(Name = "Order ID")]
        public string? OrderId { get; set; }

        [Display(Name = "Customer Name")]
        public string? CustomerName { get; set; }

        // New property to hold all uploaded files
        public List<UploadedFile> UploadedFiles { get; set; } = new();
    }

    public class UploadedFile
    {
        public string FileName { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public DateTime UploadDate { get; set; }
        public string Url { get; set; } = string.Empty;
    }
}
