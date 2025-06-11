namespace WebGenerateImage.Models
{
    public class ImageToImage
    {
        public int id { get; set; }
        public string imagePathOrigin { get; set; }

        public float strength { get; set; }
        public string imagePathGenerate { get; set; }
        public string UserId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

