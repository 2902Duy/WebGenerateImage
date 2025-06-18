using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using WebGenerateImage.Data;
using WebGenerateImage.Models;

namespace WebGenerateImage.Areas.Identity.Pages.Account
{
    [IgnoreAntiforgeryToken]
    public class FaceLoginVerifyModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly AppDbContext _context;

        public FaceLoginVerifyModel(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, AppDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync(IFormFile image)
        {
            var userId = TempData["FaceLoginUserId"]?.ToString();
            var returnUrl = TempData["ReturnUrl"]?.ToString() ?? "~/";

            if (string.IsNullOrEmpty(userId) || image == null || image.Length == 0)
            {
                return new JsonResult(new { success = false, message = "Thiếu dữ liệu xác thực." });
            }

            try
            {
                using var httpClient = new HttpClient();
                using var form = new MultipartFormDataContent();

                // Gửi userId dưới dạng chuỗi
                form.Add(new StringContent(userId), "userId");

                // Gửi ảnh
                form.Add(new StreamContent(image.OpenReadStream()), "image", "face.jpg");

                var response = await httpClient.PostAsync("http://localhost:5001/recognize", form);
                var resultStr = await response.Content.ReadAsStringAsync();

                var result = JsonSerializer.Deserialize<VerifyResult>(resultStr, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result != null && result.success)
                {
                    var user = await _userManager.FindByIdAsync(userId);
                    if (user == null)
                    {
                        return new JsonResult(new { success = false, message = "Không tìm thấy tài khoản." });
                    }

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return new JsonResult(new { success = true, redirect = returnUrl });
                }

                return new JsonResult(new
                {
                    success = false,
                    message = result?.message ?? "Xác thực khuôn mặt thất bại."
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }


        public class VerifyResult
        {
            public bool success { get; set; }
            public string message { get; set; }
        }
    }
}
