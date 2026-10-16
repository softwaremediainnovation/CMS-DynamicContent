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
        var accessibleUrl = NetworkHelper.ResolveAccessibleBaseUrl(HttpContext.Request);

        return Ok(new
        {
            localIp = localIp,
            port = port,
            httpBaseUrl = accessibleUrl,
            currentBaseUrl = accessibleUrl,
            accessibleBaseUrl = accessibleUrl
        });
    }

    [HttpPost("spawn")]
    public async Task<IActionResult> SpawnPet([FromBody] PetSpawnRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerName))
        {
            return BadRequest(new { success = false, message = "Nama pemain wajib diisi." });
        }

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _moderationService.SubmitPetAsync(request, clientIp);

        return Ok(new
        {
            success = result.Success,
            status = result.Status,
            message = result.Message,
            pet = result.Pet,
            remainingSeconds = result.RemainingCooldownSeconds
        });
    }

    [HttpGet("check-cooldown")]
    public IActionResult CheckCooldown([FromQuery] string? fingerprint, [FromQuery] string? room)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (canSubmit, remainingSec) = _moderationService.CheckCooldown(fingerprint, clientIp, room);

        return Ok(new
        {
            canSubmit = canSubmit,
            remainingSeconds = remainingSec,
            cooldownMinutes = _moderationService.GetConfig().CooldownMinutes
        });
    }

    // Dipanggil oleh Layar Videotron untuk mengambil pet yang SUDAH DISETUJUI (APPROVED)
    [HttpGet("events")]
    public IActionResult GetApprovedPets([FromQuery] string? room, [FromQuery] long sinceUnixMs = 0)
    {
        var targetRoom = string.IsNullOrWhiteSpace(room) ? "DEFAULT" : room.Trim().ToUpper();
        var pets = _moderationService.GetApprovedPets(targetRoom, sinceUnixMs);
        var removedPetIds = _moderationService.GetRemovedPetIds(targetRoom, sinceUnixMs);
        var stats = _moderationService.GetStats(targetRoom);

        return Ok(new
        {
            room = targetRoom,
            totalSpawns = stats.ApprovedCount,
            pets = pets,
            removedPetIds = removedPetIds,
            serverTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    // Dipanggil saat Layar Videotron pertama kali dibuka/refresh untuk memuat pet yang sedang berenang
    [HttpGet("active")]
    public IActionResult GetActivePets([FromQuery] string? room, [FromQuery] int limit = 30)
    {
        var targetRoom = string.IsNullOrWhiteSpace(room) ? "DEFAULT" : room.Trim().ToUpper();
        var pets = _moderationService.GetActivePets(targetRoom, limit);

        return Ok(new
        {
            room = targetRoom,
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

    [HttpPost("takedown/{id}")]
    public IActionResult TakeDownPet([FromRoute] string id)
    {
        var ok = _moderationService.TakeDownPet(id);
        if (!ok) return NotFound(new { success = false, message = "Pet tidak ditemukan atau sudah diturunkan." });

        return Ok(new { success = true, message = $"Pet {id} berhasil diturunkan (take down) dari layar videotron!" });
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
            aiRejectUnsafeDrawings = config.AiRejectUnsafeDrawings,
            cooldownMinutes = config.CooldownMinutes
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
            approvedAt = pet.ApprovedAt,
            cooldownMinutes = _moderationService.GetConfig().CooldownMinutes
        });
    }
}

public class RejectPetRequest
{
    public string? Reason { get; set; }
}
