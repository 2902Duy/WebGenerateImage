using DotNetEnv;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using WebGenerateImage.Data;
using WebGenerateImage.Models;


namespace WebGenerateImage.Areas.Identity.Pages.Account
{
    [IgnoreAntiforgeryToken]
    [Authorize]
    public class RegisterFaceModel : PageModel
    {

        private readonly UserManager<IdentityUser> _userManager;
        private readonly AppDbContext _context;
        public RegisterFaceModel(UserManager<IdentityUser> userManager, AppDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync(IFormFile image)
        {
            if (image == null || image.Length == 0)
            {
                return new JsonResult(new { success = false, message = "Không có ảnh." });
            }

            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id;

            if (string.IsNullOrEmpty(userId))
            {
                return new JsonResult(new { success = false, message = "Không xác định được UserId." });
            }
            using var httpClient = new HttpClient();
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(userId), "name");
            content.Add(new StreamContent(image.OpenReadStream()), "image", "face.jpg");

            var response = await httpClient.PostAsync("http://localhost:5001/register", content);
            var result = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<RegisterResult>(result, new JsonSerializerOptions
            {   PropertyNameCaseInsensitive = true
            });
            if (data != null && data.success)
            {
                var faceAuth = new FaceAuthentication
                {
                    UserId = userId,
                    IsFaceAuth = true,
                    IsFaceVerified = true 
                };
                _context.FaceAuthentications.Add(faceAuth);
                await _context.SaveChangesAsync();
            }
            else
            {
               return new JsonResult(new { success = false, message = data?.message ?? "Lỗi không xác định khi đăng ký khuôn mặt." });
            }
            return Content(result, "application/json");
            

        }

        
    }

    public class RegisterResult
    {
        public bool success { get; set; }
        public string message { get; set; }
    }
}
