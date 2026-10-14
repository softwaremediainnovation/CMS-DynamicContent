using Microsoft.AspNetCore.Mvc;
using CmsBillboard.DynamicContent.Services;

namespace CmsBillboard.DynamicContent.Controllers;

[ApiController]
[Route("api/pet")]
public class PetApiController : ControllerBase
{
    private readonly IPetModerationService _moderationService;

    public PetApiController(IPetModerationService moderationService)
    {
        _moderationService = moderationService;
    }

    [HttpGet("server-info")]
    public IActionResult GetServerInfo()
    {
        var localIp = NetworkHelper.GetLocalLanIp();
        var port = HttpContext.Request.Host.Port ?? 5215;
        var scheme = HttpContext.Request.Scheme;

        return Ok(new
        {
            localIp = localIp,
            port = port,
            httpBaseUrl = $"{scheme}://{localIp}:{port}",
            currentBaseUrl = $"{scheme}://{HttpContext.Request.Host}"
        });
    }

    [HttpPost("spawn")]
    public async Task<IActionResult> SpawnPet([FromBody] PetSpawnRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerName))
        {
            return BadRequest(new { success = false, message = "Nama pemain wajib diisi." });
        }

        var result = await _moderationService.SubmitPetAsync(request);

        return Ok(new
        {
            success = result.Success,
            status = result.Status,
            message = result.Message,
            pet = result.Pet
        });
    }

    // Dipanggil oleh Layar Videotron untuk mengambil pet yang SUDAH DISETUJUI (APPROVED)
    [HttpGet("events")]
    public IActionResult GetApprovedPets([FromQuery] string? room, [FromQuery] long sinceUnixMs = 0)
    {
        var targetRoom = string.IsNullOrWhiteSpace(room) ? "DEFAULT" : room.Trim().ToUpper();
        var pets = _moderationService.GetApprovedPets(targetRoom, sinceUnixMs);
        var stats = _moderationService.GetStats(targetRoom);

        return Ok(new
        {
            room = targetRoom,
            totalSpawns = stats.ApprovedCount,
            pets = pets,
            serverTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    // Dipanggil oleh Panel Moderasi Admin untuk mengambil daftar pet yang MENUNGGU PERSETUJUAN
    [HttpGet("pending")]
    public IActionResult GetPendingPets([FromQuery] string? room)
    {
        var pets = _moderationService.GetPendingPets(room);
        return Ok(new
        {
            success = true,
            pets = pets,
            totalPending = pets.Count()
        });
    }

    [HttpPost("approve/{id}")]
    public IActionResult ApprovePet([FromRoute] string id)
    {
        var ok = _moderationService.ApprovePet(id);
        if (!ok) return NotFound(new { success = false, message = "Pet tidak ditemukan." });

        return Ok(new { success = true, message = $"Pet {id} berhasil disetujui untuk tayang!" });
    }

    [HttpPost("reject/{id}")]
    public IActionResult RejectPet([FromRoute] string id, [FromBody] RejectPetRequest? req)
    {
        var ok = _moderationService.RejectPet(id, req?.Reason ?? "Ditolak oleh admin.");
        if (!ok) return NotFound(new { success = false, message = "Pet tidak ditemukan." });

        return Ok(new { success = true, message = $"Pet {id} berhasil ditolak." });
    }

    [HttpPost("approve-all")]
    public IActionResult ApproveAll([FromQuery] string? room)
    {
        var count = _moderationService.ApproveAll(room);
        return Ok(new { success = true, count = count, message = $"{count} pet berhasil disetujui bersamaan." });
    }

    [HttpPost("reject-all")]
    public IActionResult RejectAll([FromQuery] string? room)
    {
        var count = _moderationService.RejectAll(room);
        return Ok(new { success = true, count = count, message = $"{count} pet berhasil ditolak bersamaan." });
    }

    [HttpGet("stats")]
    public IActionResult GetStats([FromQuery] string? room)
    {
        var stats = _moderationService.GetStats(room);
        return Ok(stats);
    }

    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var config = _moderationService.GetConfig();
        // Mask API Key for security preview
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
            autoApproveTemplates = config.AutoApproveTemplates,
            aiAutoApproveSafeDrawings = config.AiAutoApproveSafeDrawings,
            aiRejectUnsafeDrawings = config.AiRejectUnsafeDrawings
        });
    }

    [HttpPost("config")]
    public IActionResult UpdateConfig([FromBody] PetModerationConfig request)
    {
        _moderationService.UpdateConfig(request);
        return Ok(new { success = true, message = "Konfigurasi moderasi berhasil diperbarui!" });
    }

    [HttpGet("status/{id}")]
    public IActionResult CheckPetStatus([FromRoute] string id)
    {
        var pet = _moderationService.GetPetById(id);
        if (pet == null) return NotFound(new { success = false, message = "Pet tidak ditemukan." });

        return Ok(new
        {
            id = pet.Id,
            status = pet.Status,
            playerName = pet.PlayerName,
            petType = pet.PetType,
            reason = pet.AiReviewReason,
            approvedAt = pet.ApprovedAt
        });
    }
}

public class RejectPetRequest
{
    public string? Reason { get; set; }
}
