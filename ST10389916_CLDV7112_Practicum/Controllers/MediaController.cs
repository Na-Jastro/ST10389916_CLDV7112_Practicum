using Microsoft.AspNetCore.Mvc;

namespace ST10389916_CLDV7112_Practicum.Controllers;

public sealed class MediaController(BlobMediaStore mediaStore) : Controller
{
    [HttpGet]
    public IActionResult Upload() => View();

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            ViewBag.Error = "Select a file to upload.";
            return View();
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var uri = await mediaStore.UploadAsync(file.FileName, stream, cancellationToken);
            ViewBag.Message = $"Uploaded: {uri}";
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
        }

        return View();
    }
}
