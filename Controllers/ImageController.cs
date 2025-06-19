using DotNetEnv;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using WebGenerateImage.Models;

namespace WebGenerateImage.Controllers
{
    [Authorize]
    public class ImageController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppDbContext _context;

        private readonly string _huggingFaceUrl;
        private readonly string _huggingFaceKey;
        private readonly string _stabilityUrl;
        private readonly string _stabilityKey;
        private readonly string _stabilityEngine;
        private readonly string _googleTranslateApi;

        public ImageController(
            IHttpClientFactory httpClientFactory,
            AppDbContext context)
        {
            DotNetEnv.Env.Load();
            _httpClientFactory = httpClientFactory;
            _context = context;

            _huggingFaceUrl = Environment.GetEnvironmentVariable("API_URL_HUG")!;
            _huggingFaceKey = Environment.GetEnvironmentVariable("API_KEY_HUG")!;
            _googleTranslateApi = Environment.GetEnvironmentVariable("GOOGLE_TRANSLATE_API")!;
            _stabilityUrl = Environment.GetEnvironmentVariable("API_URL_STABILITY")!;
            _stabilityKey = Environment.GetEnvironmentVariable("API_KEY_STABILITY")!;
            _stabilityEngine = Environment.GetEnvironmentVariable("ENGINE_ID_STABLE")!;
        }

        // ==== TEXT → IMAGE ====
        [HttpGet]
        public IActionResult Index(string? prompt)
        {
            return View(new ImagePrompt { Prompt = prompt });
        }

        [HttpPost]
        public async Task<IActionResult> Generate(ImagePrompt model)
        {
            if (string.IsNullOrWhiteSpace(model.Prompt))
            {
                ModelState.AddModelError(nameof(model.Prompt), "Vui lòng nhập prompt.");
                return View("Index", model);
            }

            try
            {
                // 1) Translate prompt (Vietnamese → English)
                var client = _httpClientFactory.CreateClient();
                var q = Uri.EscapeDataString(model.Prompt);
                var translateUrl = $"{_googleTranslateApi}?client=gtx&sl=vi&tl=en&dt=t&q={q}";
                var translateJson = await client.GetStringAsync(translateUrl);
                using var doc = JsonDocument.Parse(translateJson);
                var translation = doc.RootElement[0][0][0].GetString() ?? model.Prompt;

                // 2) Call HuggingFace API
                var hfClient = _httpClientFactory.CreateClient();
                hfClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _huggingFaceKey);

                var payload = new { inputs = translation };
                var hfResponse = await hfClient.PostAsJsonAsync(_huggingFaceUrl, payload);
                hfResponse.EnsureSuccessStatusCode();

                var bytes = await hfResponse.Content.ReadAsByteArrayAsync();
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
                var fileName = $"{Guid.NewGuid():N}.png";
                var saveDir = Path.Combine("wwwroot", "images", "Users", userId, "ImagePrompt");
                Directory.CreateDirectory(saveDir);
                var savePath = Path.Combine(saveDir, fileName);
                await System.IO.File.WriteAllBytesAsync(savePath, bytes);

                // 3) Save record
                model.UserId = userId;
                model.ImagePath = $"/images/Users/{userId}/ImagePrompt/{fileName}";
                model.CreatedAt = DateTime.UtcNow;
                _context.ImagePrompts.Add(model);
                await _context.SaveChangesAsync();

                return View("Result", model);
            }
            catch (HttpRequestException httpEx)
            {
                ModelState.AddModelError("", $"API lỗi: {httpEx.Message}");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Lỗi nội bộ: {ex.Message}");
            }

            return View("Index", model);
        }

        // ==== IMAGE → IMAGE ====
        [HttpGet]
        public IActionResult Upload() => View();

        [HttpPost]
        [RequestSizeLimit(10_000_000)] // ví dụ giới hạn 10MB
        public async Task<IActionResult> Transformation(IFormFile initImage, string imageStrength)
        {
            if (initImage is null || initImage.Length == 0)
                return BadRequest("Không có ảnh tải lên.");

            try
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
                var now = DateTime.UtcNow;

                // 1) Lưu gốc
                var origName = $"original_{Guid.NewGuid():N8}.png";
                var origDir = Path.Combine("wwwroot", "images", "Users", userId, "ImageToImage");
                Directory.CreateDirectory(origDir);
                var origPath = Path.Combine(origDir, origName);
                await using (var fs = System.IO.File.Create(origPath))
                    await initImage.CopyToAsync(fs);

                // 2) Resize (1024×1024)
                using var resizedStream = ResizeImage(initImage.OpenReadStream(), 1024, 1024);

                // 3) Gửi tới Stability API
                var client = _httpClientFactory.CreateClient();
                var multipart = new MultipartFormDataContent
                {
                    { new StreamContent(resizedStream)
                        { Headers = { ContentType = new MediaTypeHeaderValue("image/png") } },
                        "init_image", "init.png" },
                    { new StringContent("original style"), "text_prompts[0][text]" },
                    { new StringContent("2"), "cfg_scale" },
                    { new StringContent("FAST_BLUE"), "clip_guidance_preset" },
                    { new StringContent(imageStrength), "image_strength" },
                    { new StringContent("1"), "samples" },
                    { new StringContent("30"), "steps" }
                };

                var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{_stabilityUrl}/{_stabilityEngine}/image-to-image")
                {
                    Headers = { Authorization = new AuthenticationHeaderValue("Bearer", _stabilityKey) },
                    Content = multipart
                };

                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
                var b64 = root.GetProperty("artifacts")[0].GetProperty("base64").GetString()!;
                var genBytes = Convert.FromBase64String(b64);

                // 4) Lưu ảnh tạo
                var genName = $"generated_{Guid.NewGuid():N8}.png";
                var genPath = Path.Combine(origDir, genName);
                await System.IO.File.WriteAllBytesAsync(genPath, genBytes);

                // 5) Lưu record
                var record = new ImageToImage
                {
                    UserId = userId,
                    imagePathOrigin = $"/images/Users/{userId}/ImageToImage/{origName}",
                    imagePathGenerate = $"/images/Users/{userId}/ImageToImage/{genName}",
                    strength = float.Parse(imageStrength, CultureInfo.InvariantCulture),
                    CreatedAt = now
                };
                _context.ImageToImages.Add(record);
                await _context.SaveChangesAsync();

                // 6) Truyền dữ liệu ra view
                ViewBag.OriginalImage = record.imagePathOrigin;
                ViewBag.GeneratedImage = record.imagePathGenerate;
                ViewBag.Strength = imageStrength;

                return View("Transformation");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Lỗi xử lý: {ex.Message}");
                return View("Upload");
            }
        }

        // ==== THƯ VIỆN ẢNH & XÓA ====
        [HttpGet]
        public IActionResult MyLibrary()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
            var promptDir = Path.Combine("wwwroot", "images", "Users", userId, "ImagePrompt");
            var i2iDir = Path.Combine("wwwroot", "images", "Users", userId, "ImageToImage");

            var urls = new List<string>();
            if (Directory.Exists(promptDir))
                urls.AddRange(Directory.GetFiles(promptDir)
                    .Select(f => $"/images/Users/{userId}/ImagePrompt/{Path.GetFileName(f)}"));
            if (Directory.Exists(i2iDir))
                urls.AddRange(Directory.GetFiles(i2iDir)
                    .Select(f => $"/images/Users/{userId}/ImageToImage/{Path.GetFileName(f)}"));

            return View(urls);
        }

        [HttpGet]
        public IActionResult Delete(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return BadRequest();

            return View(model: imageUrl);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(string imageUrl)
        {
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                var www = Directory.GetCurrentDirectory();
                var physical = Path.Combine(www, "wwwroot", imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(physical))
                    System.IO.File.Delete(physical);

                // Nếu bạn lưu metadata trong DB, có thể xóa record tại đây
                // _context.SaveChanges();
            }

            return RedirectToAction(nameof(MyLibrary));
        }

        // ==== GIÚP VIỆN resize hình ====
        private static Stream ResizeImage(Stream input, int width, int height)
        {
            using var original = Image.FromStream(input);
            var resized = new Bitmap(width, height);
            using var g = Graphics.FromImage(resized);
            g.DrawImage(original, 0, 0, width, height);

            var output = new MemoryStream();
            resized.Save(output, ImageFormat.Png);
            output.Position = 0;
            return output;
        }
    }
}
