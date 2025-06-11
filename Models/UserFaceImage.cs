using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.AspNetCore.Identity; 


namespace WebGenerateImage.Models
{
    public class UserFaceImage
    {
        [Key]
        public int Id { get; set; }

        // Id user là string, vì IdentityUser.Id là string
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public IdentityUser? User { get; set; }

        public string ImagePath { get; set; } = string.Empty;

        public bool IsFaceAuth { get; set; } = false;
    }
}
