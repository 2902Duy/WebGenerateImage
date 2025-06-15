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

[Authorize]
public class ImageController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpClientFactory _clientFactory;
    private readonly AppDbContext _context;

    
    public ImageController(IHttpClientFactory httpClientFactory, IHttpClientFactory clientFactory, AppDbContext context)
    {
        DotNetEnv.Env.Load();
        _httpClientFactory = httpClientFactory;
        _clientFactory = clientFactory;
        _context = context;

    }

    private string API_URL = Environment.GetEnvironmentVariable("API_URL_HUG");

    private string API_KEY = Environment.GetEnvironmentVariable("API_KEY_HUG");

    private string GOOGLE_TRANSLATE_API = Environment.GetEnvironmentVariable("GOOGLE_TRANSLATE_API");

    public IActionResult Index()
    {
        return View();
    }

    // prompt to image
    [HttpPost]
    public async Task<IActionResult> Generate(ImagePrompt model)
    {
        if (string.IsNullOrEmpty(model.Prompt))
        {
            ModelState.AddModelError("Prompt", "Vui lòng nhập nội dung.");
            return View("Index", model);
        }

        try
        {
            var translateClient = _httpClientFactory.CreateClient();
            var encodedPrompt = Uri.EscapeDataString(model.Prompt);
            var translateUrl = $"{GOOGLE_TRANSLATE_API}?client=gtx&sl=vi&tl=en&dt=t&q={encodedPrompt}";

            var translateResponse = await translateClient.GetStringAsync(translateUrl);


            using var jsonDoc = JsonDocument.Parse(translateResponse);
            var translation = jsonDoc.RootElement[0][0][0].GetString();
            Console.WriteLine($"Translated Prompt: {translation}");
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", API_KEY);

            var payload = new { inputs = translation };
            var response = await client.PostAsJsonAsync(API_URL, payload);

            if (response.IsSuccessStatusCode)
            {
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var fileName = $"{Guid.NewGuid()}.png";
                var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", $"images/Users/{userId}/ImagePrompt", fileName);

                await System.IO.File.WriteAllBytesAsync(savePath, bytes);
                
                model.UserId = userId;
                model.ImagePath = $"/images/Users/{userId}/ImagePrompt/" + fileName;
                model.CreatedAt= DateTime.Now;
                _context.ImagePrompts.Add(model);
                await _context.SaveChangesAsync();
                return View("Result", model);
                
            }

            ModelState.AddModelError("", "API lỗi: " + response.StatusCode);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Lỗi: " + ex.Message);
        }

        return View("Index", model);
    }


    //image to image

    private string API_KEY_STABILITY = Environment.GetEnvironmentVariable("API_KEY_STABILITY");
    private string ENGINE_ID_STABLE = Environment.GetEnvironmentVariable("ENGINE_ID_STABLE");
    private string API_URL_STABILITY = Environment.GetEnvironmentVariable("API_URL_STABILITY");
    [HttpGet]
    public IActionResult Upload()
    {
        return View();
    }

    public static Stream ResizeImage(Stream inputStream, int width, int height)
    {
        using var image = Image.FromStream(inputStream);
        var resized = new Bitmap(width, height);
        using var g = Graphics.FromImage(resized);
        g.DrawImage(image, 0, 0, width, height);

        var outputStream = new MemoryStream();
        resized.Save(outputStream, ImageFormat.Png);
        outputStream.Position = 0;
        return outputStream;
    }
    [HttpPost]
    public async Task<IActionResult> Transformation(IFormFile initImage, string imageStrength)
    {


        try
        {
            if (initImage == null || initImage.Length == 0)
                return BadRequest("Không có ảnh.");
            var resizedStream = ResizeImage(initImage.OpenReadStream(), 1024, 1024);

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            // Lưu bản gốc (chưa resize) nếu cần
            var originalFileName = $"original_{Guid.NewGuid().ToString().Substring(0, 8)}.png";
            var originalPath = Path.Combine($"wwwroot/images/Users/{userId}/ImageToImage", originalFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(originalPath)!);
            using (var fileStream = new FileStream(originalPath, FileMode.Create))
            {
                await initImage.CopyToAsync(fileStream);
            }

            // Gửi ảnh resized lên API
            var client = _clientFactory.CreateClient();
            var requestContent = new MultipartFormDataContent();

            var imageContent = new StreamContent(resizedStream);
            imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
            requestContent.Add(imageContent, "init_image", "init.png");

            requestContent.Add(new StringContent("original style"), "text_prompts[0][text]");
            requestContent.Add(new StringContent("2"), "cfg_scale");
            requestContent.Add(new StringContent("FAST_BLUE"), "clip_guidance_preset");
            requestContent.Add(new StringContent(imageStrength), "image_strength");
            requestContent.Add(new StringContent("1"), "samples");
            requestContent.Add(new StringContent("30"), "steps");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{API_URL_STABILITY}/{ENGINE_ID_STABLE}/image-to-image");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", API_KEY_STABILITY);
            request.Content = requestContent;

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                return Content($"Lỗi: {response.StatusCode}\n{errorText}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonDocument.Parse(json);


            var base64 = data.RootElement.GetProperty("artifacts")[0].GetProperty("base64").GetString();
            var bytes = Convert.FromBase64String(base64);
            var outputFileName = $"generated_{Guid.NewGuid().ToString().Substring(0, 8)}.png";
            var outputPath = Path.Combine($"wwwroot/images/Users/{userId}/ImageToImage", outputFileName);
            await System.IO.File.WriteAllBytesAsync(outputPath, bytes);

            var record = new ImageToImage
            {
                imagePathOrigin = $"/images/Users/{userId}/ImageToImage/{originalFileName}",
                strength = float.Parse(imageStrength, CultureInfo.InvariantCulture),
                imagePathGenerate = $"/images/Users/{userId}/ImageToImage/{outputFileName}",
                UserId = userId,
                CreatedAt = DateTime.Now
            };
            
            _context.ImageToImages.Add(record);
            await _context.SaveChangesAsync();
            
            ViewBag.OriginalImage = $"/images/Users/{userId}/ImageToImage/{originalFileName}";
            ViewBag.GeneratedImage = $"/images/Users/{userId}/ImageToImage/{outputFileName}";
            ViewBag.Strength = imageStrength;

            return View("Transformation");
        }
        catch (Exception ex)
        {
            return Content($"Đã xảy ra lỗi: {ex.Message}");
        }
        return View("Upload");
    }

    public IActionResult MyLibrary()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var promptImageFolder = Path.Combine("wwwroot/images/Users", userId, "ImagePrompt");
        var imageToImageFolder = Path.Combine("wwwroot/images/Users", userId, "ImageToImage");

        var promptImages = new List<string>();
        var imageToImageImages = new List<string>();

        if (Directory.Exists(promptImageFolder))
        {
            promptImages = Directory.GetFiles(promptImageFolder)
                                    .Select(f => $"/images/Users/{userId}/ImagePrompt/{Path.GetFileName(f)}")
                                    .ToList();
        }

        if (Directory.Exists(imageToImageFolder))
        {
            imageToImageImages = Directory.GetFiles(imageToImageFolder)
                                          .Where(f => Path.GetFileName(f).StartsWith("generated_")) 
                                          .Select(f => $"/images/Users/{userId}/ImageToImage/{Path.GetFileName(f)}")
                                          .ToList();
        }

        var allImages = promptImages.Concat(imageToImageImages).ToList();

        return View(allImages);
    }


}
