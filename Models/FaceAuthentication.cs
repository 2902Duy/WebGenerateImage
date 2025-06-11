using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity; // cần để dùng IdentityUser

namespace WebGenerateImage.Models
{
    public class FaceAuthentication
    {
        [Key]
        public int Id { get; set; }

        // Id user là string, vì IdentityUser.Id là string
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public IdentityUser? User { get; set; }
        public bool IsFaceAuth { get; set; } = false;
        public bool IsFaceVerified { get; set; } = false;
    }
}
