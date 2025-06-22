using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebGenerateImage.Data;
using WebGenerateImage.Models;
using WebGenerateImage.Services;

namespace WebGenerateImage.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IUserStore<IdentityUser> _userStore;
        private readonly IUserEmailStore<IdentityUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly AppDbContext _context;
        private readonly EmailService _emailService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public RegisterModel(
            UserManager<IdentityUser> userManager,
            IUserStore<IdentityUser> userStore,
            SignInManager<IdentityUser> signInManager,
            ILogger<RegisterModel> logger,
            AppDbContext context,
            EmailService emailService,
            IWebHostEnvironment webHostEnvironment)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _context = context;
            _emailService = emailService;
            _webHostEnvironment = webHostEnvironment;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "Mật khẩu phải dài ít nhất {2} ký tự.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Mật khẩu")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Xác nhận mật khẩu")]
            [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
            public string ConfirmPassword { get; set; }

            [Required]
            [Display(Name = "Mã OTP")]
            public string OtpCode { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (!ModelState.IsValid)
                return Page();

            var existingUser = await _userManager.FindByEmailAsync(Input.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError(string.Empty, "Email đã được đăng ký.");
                return Page();
            }

            // Kiểm tra mã OTP
            var otpRecord = _context.OtpCodes
                .Where(o => o.Email == Input.Email && o.Code == Input.OtpCode)
                .OrderByDescending(o => o.ExpirationTime)
                .FirstOrDefault();

            if (otpRecord == null || otpRecord.ExpirationTime < DateTime.Now)
            {
                ModelState.AddModelError(string.Empty, "Mã OTP không hợp lệ hoặc đã hết hạn.");
                return Page();
            }

            var user = CreateUser();

            await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
            await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("Người dùng mới đã được tạo.");
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                await _userManager.ConfirmEmailAsync(user, token);

                // Gán role mặc định nếu cần
                await _userManager.AddToRoleAsync(user, "User");
                // Tạo thư mục cho người dùng mới
                CreateUserDirectory(user.Id);
                // Xoá mã OTP đã dùng
                _context.OtpCodes.RemoveRange(_context.OtpCodes.Where(o => o.Email == Input.Email));
                await _context.SaveChangesAsync();

                // Chuyển hướng về trang login (không đăng nhập tự động)
                return RedirectToPage("Login");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return Page();
        }

        public async Task<IActionResult> OnPostSendOtpAsync()
        {
            if (string.IsNullOrWhiteSpace(Input?.Email))
            {
                ModelState.AddModelError(string.Empty, "Email không hợp lệ.");

                return Page();
            }
            ModelState.Remove("Input.OtpCode");

            var existingUser = await _userManager.FindByEmailAsync(Input.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError(string.Empty, "Email đã được đăng ký.");
                return Page();
            }

            // Tạo mã OTP mới
            var otpCode = new Random().Next(100000, 999999).ToString();
            var otp = new OtpCode
            {
                Email = Input.Email,
                Code = otpCode,
                ExpirationTime = DateTime.Now.AddMinutes(2)
            };

            _context.OtpCodes.Add(otp);
            await _context.SaveChangesAsync();

            await _emailService.SendOtpAsync(Input.Email, otpCode);
            TempData["OtpSent"] = "Mã OTP đã được gửi đến email.";
            return Page();
        }

        private IdentityUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<IdentityUser>();
            }
            catch
            {
                throw new InvalidOperationException("Không thể tạo người dùng. Hãy đảm bảo IdentityUser có constructor mặc định.");
            }
        }

        private IUserEmailStore<IdentityUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
                throw new NotSupportedException("UserManager hiện tại không hỗ trợ email.");
            return (IUserEmailStore<IdentityUser>)_userStore;
        }
        private void CreateUserDirectory(string userId)
        {
            var userFolder = Path.Combine(_webHostEnvironment.WebRootPath, "Images", "Users", userId);
            var promptFolder = Path.Combine(userFolder, "ImagePrompt");
            var imageToImageFolder = Path.Combine(userFolder, "ImageToImage");

            if (!Directory.Exists(userFolder))
            {
                Directory.CreateDirectory(promptFolder);
                Directory.CreateDirectory(imageToImageFolder);
            }
        }
    }
}
