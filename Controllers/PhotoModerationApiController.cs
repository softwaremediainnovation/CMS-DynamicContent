using Microsoft.AspNetCore.Mvc;
using CmsBillboard.DynamicContent.Services;

namespace CmsBillboard.DynamicContent.Controllers;

[ApiController]
[Route("api/photo-moderation")]
public class PhotoModerationApiController : ControllerBase
{
    private readonly IPhotoModerationService _moderationService;

    public PhotoModerationApiController(IPhotoModerationService moderationService)
    {
        _moderationService = moderationService;
    }

    [HttpGet("pending")]
    public IActionResult GetPendingPhotos([FromQuery] string? site = null, [FromQuery] string? contentId = null)
    {
        var photos = _moderationService.GetPendingPhotos(site, contentId);
        return Ok(new
        {
            success = true,
            photos = photos,
            totalPending = photos.Count()
        });
    }

    [HttpGet("approved")]
    public IActionResult GetApprovedPhotos([FromQuery] string? site = null, [FromQuery] string? contentId = null, [FromQuery] int limit = 30)
    {
        var photos = _moderationService.GetApprovedPhotos(site, contentId, limit);
        return Ok(new
        {
            success = true,
            photos = photos,
            totalApproved = photos.Count()
        });
    }

    [HttpGet("history")]
    public IActionResult GetHistoryPhotos([FromQuery] string? site = null, [FromQuery] string? contentId = null, [FromQuery] int limit = 50)
    {
        var photos = _moderationService.GetHistoryPhotos(site, contentId, limit);
        return Ok(new
        {
            success = true,
            photos = photos,
            total = photos.Count()
        });
    }

    [HttpPost("approve/{id:int}")]
    public async Task<IActionResult> ApprovePhoto([FromRoute] int id)
    {
        var ok = await _moderationService.ApprovePhotoAsync(id);
        if (!ok) return NotFound(new { success = false, message = "Foto tidak ditemukan." });

        return Ok(new { success = true, message = $"Foto #{id} berhasil disetujui & tayang di videotron!" });
    }

    [HttpPost("reject/{id:int}")]
    public async Task<IActionResult> RejectPhoto([FromRoute] int id, [FromBody] RejectPhotoRequest? req)
    {
        var ok = await _moderationService.RejectPhotoAsync(id, req?.Reason ?? "Ditolak oleh admin.");
        if (!ok) return NotFound(new { success = false, message = "Foto tidak ditemukan." });

        return Ok(new { success = true, message = $"Foto #{id} berhasil ditolak." });
    }

    [HttpPost("takedown/{id:int}")]
    public async Task<IActionResult> TakeDownPhoto([FromRoute] int id)
    {
        var ok = await _moderationService.TakeDownPhotoAsync(id);
        if (!ok) return NotFound(new { success = false, message = "Foto tidak ditemukan atau sudah dicopot." });

        return Ok(new { success = true, message = $"Foto #{id} berhasil diturunkan (take down) dari layar videotron!" });
    }

    [HttpPost("approve-all")]
    public async Task<IActionResult> ApproveAll([FromQuery] string? site = null, [FromQuery] string? contentId = null)
    {
        var count = await _moderationService.ApproveAllAsync(site, contentId);
        return Ok(new { success = true, count = count, message = $"{count} foto berhasil disetujui bersamaan." });
    }

    [HttpPost("reject-all")]
    public async Task<IActionResult> RejectAll([FromQuery] string? site = null, [FromQuery] string? contentId = null)
    {
        var count = await _moderationService.RejectAllAsync(site, contentId);
        return Ok(new { success = true, count = count, message = $"{count} foto berhasil ditolak bersamaan." });
    }

    [HttpGet("stats")]
    public IActionResult GetStats([FromQuery] string? site = null, [FromQuery] string? contentId = null)
    {
        var stats = _moderationService.GetStats(site, contentId);
        return Ok(stats);
    }

    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var config = _moderationService.GetConfig();
        var maskedKey = string.IsNullOrWhiteSpace(config.GeminiApiKey)
            ? ""
            : (config.GeminiApiKey.Length > 8
                ? $"{config.GeminiApiKey[..4]}...{config.GeminiApiKey[^4..]}"
                : "********");

        return Ok(new
        {
            mode = config.Mode,
            hasApiKey = !string.IsNullOrWhiteSpace(config.GeminiApiKey),
            maskedApiKey = maskedKey,
            model = config.GeminiModel,
            aiAutoApproveSafePhotos = config.AiAutoApproveSafePhotos,
            aiRejectUnsafePhotos = config.AiRejectUnsafePhotos,
            cooldownMinutes = config.CooldownMinutes
        });
    }

    [HttpPost("config")]
    public IActionResult UpdateConfig([FromBody] PhotoModerationConfig request)
    {
        _moderationService.UpdateConfig(request);
        return Ok(new { success = true, message = "Konfigurasi moderasi foto berhasil diperbarui!" });
    }
}

public class RejectPhotoRequest
{
    public string? Reason { get; set; }
}
