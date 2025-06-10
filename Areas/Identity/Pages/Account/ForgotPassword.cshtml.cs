#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using WebGenerateImage.Services; // Thêm namespace chứa EmailService

namespace WebGenerateImage.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly EmailService _emailService;

        public ForgotPasswordModel(UserManager<IdentityUser> userManager, EmailService emailService)
        {
            _userManager = userManager;
            _emailService = emailService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                return RedirectToPage("./ForgotPasswordConfirmation");
            }
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            var resetLink = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code = encodedToken, email = Input.Email },
                protocol: Request.Scheme);

            string subject = "Đặt lại mật khẩu";
            string htmlMessage = $"Click vào đây để đặt lại mật khẩu: <a href='{HtmlEncoder.Default.Encode(resetLink)}'>Đặt lại mật khẩu</a>";

            try
            {
                await _emailService.SendEmailAsync(Input.Email, subject, htmlMessage);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Gửi email thất bại: {ex.Message}");
                return Page();
            }

            return RedirectToPage("./ForgotPasswordConfirmation");
        }

    }
}
