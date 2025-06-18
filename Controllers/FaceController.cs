using Microsoft.AspNetCore.Mvc;

namespace WebGenerateImage.Controllers
{
    public class FaceController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult RegisterFace()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> RegisterFacePost(IFormFile image, string userid)
        {
            if (string.IsNullOrEmpty(userid) || image == null)
            {
                return Json(new { success = false, message = "Thiếu tên hoặc ảnh" });
            }

            using var httpClient = new HttpClient();
            using var content = new MultipartFormDataContent();
            using var stream = image.OpenReadStream();

            content.Add(new StringContent(userid), "userid");
            content.Add(new StreamContent(stream), "image", "face.jpg");

            var response = await httpClient.PostAsync("http://localhost:5001/register", content);

            if (response.IsSuccessStatusCode)
            {
                return Json(new { success = false, message = "Lỗi khi gửi đến server nhận diện" });
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            return Content(jsonString, "application/json");
        }

    }
}
