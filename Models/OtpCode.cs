using System;
using System.ComponentModel.DataAnnotations;

namespace WebGenerateImage.Models
{
    public class OtpCode
    {
        [Key]
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public DateTime ExpirationTime { get; set; }
    }
}
