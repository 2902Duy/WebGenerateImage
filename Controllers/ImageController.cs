using DotNetEnv;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;
using WebGenerateImage.Models;
public class ImageController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;

private string API_URL = Environment.GetEnvironmentVariable("API_URL_HUG");

private string API_KEY = Environment.GetEnvironmentVariable("API_KEY_HUG");

private string GOOGLE_TRANSLATE_API = Environment.GetEnvironmentVariable("GOOGLE_TRANSLATE_API");
    public ImageController(IHttpClientFactory httpClientFactory)
    {
        DotNetEnv.Env.Load();
        _httpClientFactory = httpClientFactory;
    }

    public IActionResult Index()
    {
        return View();
    }
    

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
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var fileName = $"{Guid.NewGuid()}.png";
                var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", fileName);

                await System.IO.File.WriteAllBytesAsync(savePath, bytes);

                model.ImagePath = "/images/" + fileName;
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
}
