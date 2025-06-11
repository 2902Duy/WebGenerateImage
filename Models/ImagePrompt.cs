namespace WebGenerateImage.Models
{
    public class ImagePrompt
    {
        public int id { get; set; }
        public string Prompt { get; set; }
        public string ImagePath { get; set; }
        public string UserId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
