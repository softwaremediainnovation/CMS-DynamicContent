using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using CmsBillboard.DynamicContent.Data;
using CmsBillboard.DynamicContent.Hubs;
using CmsBillboard.DynamicContent.Models;
using CmsBillboard.DynamicContent.Services;

namespace CmsBillboard.DynamicContent.Controllers;

[ApiController]
[Route("api/photo-comment")]
public class PhotoCommentApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ISecurityTokenService _tokenService;
    private readonly IContentModerationService _moderationService;
    private readonly IHubContext<DynamicContentHub> _hubContext;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PhotoCommentApiController> _logger;

    public PhotoCommentApiController(
        AppDbContext context,
        ISecurityTokenService tokenService,
        IContentModerationService moderationService,
        IHubContext<DynamicContentHub> hubContext,
        IWebHostEnvironment env,
        ILogger<PhotoCommentApiController> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _moderationService = moderationService;
        _hubContext = hubContext;
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// Returns server LAN IP and base URL for multi-device Wi-Fi connections.
    /// </summary>
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

    /// <summary>
    /// Check whether a device can submit or how many minutes/seconds it must wait (15-min split time).
    /// </summary>
    [HttpGet("check-wait-time")]
    public async Task<IActionResult> CheckWaitTime([FromQuery] string id, [FromQuery] string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(fingerprint))
        {
            return Ok(new PhotoCommentWaitTimeResponse { CanPost = true, RemainingMinutes = 0, RemainingSeconds = 0 });
        }

        var master = await _context.DynamicContentMasters
            .FirstOrDefaultAsync(m => m.Id == id);

        int gapMinutes = master?.GapDurationMinutes ?? 15;
        if (gapMinutes <= 0) gapMinutes = 15;

        var lastLog = await _context.DynamicContentVoteLogs
            .Where(v => v.DynamicContentMasterId == id && v.DeviceFingerprint == fingerprint)
            .OrderByDescending(v => v.VotedAt)
            .FirstOrDefaultAsync();

        if (lastLog == null)
        {
            return Ok(new PhotoCommentWaitTimeResponse
            {
                CanPost = true,
                RemainingMinutes = 0,
                RemainingSeconds = 0,
                Message = "Perangkat Anda siap mengirim foto dan ucapan."
            });
        }

        var timeSinceLast = DateTime.UtcNow - lastLog.VotedAt;
        var requiredSpan = TimeSpan.FromMinutes(gapMinutes);

        if (timeSinceLast < requiredSpan)
        {
            var remaining = requiredSpan - timeSinceLast;
            int remSec = (int)remaining.TotalSeconds;
            int remMin = (int)Math.Ceiling(remaining.TotalMinutes);

            return Ok(new PhotoCommentWaitTimeResponse
            {
                CanPost = false,
                RemainingMinutes = remMin,
                RemainingSeconds = remSec,
                Message = $"Mode multi-partisipasi memerlukan jeda {gapMinutes} menit. Silakan tunggu {remMin} menit lagi."
            });
        }

        return Ok(new PhotoCommentWaitTimeResponse
        {
            CanPost = true,
            RemainingMinutes = 0,
            RemainingSeconds = 0,
            Message = "Perangkat Anda siap mengirim foto dan ucapan."
        });
    }

    /// <summary>
    /// Submit photo and comment with Regex Moderation and 15-minute rate limit.
    /// </summary>
    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] PhotoCommentSubmitRequest request)
    {
        if (!ModelState.IsValid)
        {
            var error = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Data input tidak valid.";
            return BadRequest(new { success = false, message = error });
        }

        // 1. Token Verification (if provided)
        if (!string.IsNullOrWhiteSpace(request.Token))
        {
            var (isValid, payload, errorMessage) = _tokenService.ValidateVoteToken(request.Token);
            if (!isValid || payload == null)
            {
                return BadRequest(new { success = false, message = errorMessage ?? "Token keamanan tidak sah atau sudah kedaluwarsa!" });
            }

            request.ContentId = payload.ContentId;
            request.SiteId = payload.SiteId;
        }

        var master = await _context.DynamicContentMasters
            .Include(m => m.Questions)
            .FirstOrDefaultAsync(m => m.Id == request.ContentId && m.IsActive);

        if (master == null)
        {
            return NotFound(new { success = false, message = "Konten kampanye tidak ditemukan atau sudah tidak aktif." });
        }

        // 2. Rate Limit Verification (15 Menit Split Time)
        int gapMinutes = master.GapDurationMinutes > 0 ? master.GapDurationMinutes : 15;
        var lastVote = await _context.DynamicContentVoteLogs
            .Where(v => v.DynamicContentMasterId == master.Id && v.DeviceFingerprint == request.DeviceFingerprint)
            .OrderByDescending(v => v.VotedAt)
            .FirstOrDefaultAsync();

        if (lastVote != null)
        {
            var elapsed = DateTime.UtcNow - lastVote.VotedAt;
            var gapSpan = TimeSpan.FromMinutes(gapMinutes);
            if (elapsed < gapSpan)
            {
                var remaining = gapSpan - elapsed;
                int remSec = (int)remaining.TotalSeconds;
                int remMin = (int)Math.Ceiling(remaining.TotalMinutes);

                return StatusCode(429, new
                {
                    success = false,
                    code = "RATE_LIMITED",
                    remainingSeconds = remSec,
                    remainingMinutes = remMin,
                    message = $"Mohon tunggu {remMin} menit lagi ({remSec} detik) sebelum mengirim foto berikutnya."
                });
            }
        }

        // 3. Regex Moderation (SARA, Profanity, Spam Promo)
        var moderationResult = _moderationService.CheckComment(request.CommentText);
        if (!moderationResult.IsApproved)
        {
            _logger.LogWarning("Photo comment rejected by moderation: Category {Category}, Reason {Reason}",
                moderationResult.Category, moderationResult.Reason);

            return BadRequest(new
            {
                success = false,
                code = "MODERATION_FAILED",
                category = moderationResult.Category,
                message = moderationResult.Reason
            });
        }

        // 4. Save Base64 Photo to Server
        string photoUrl;
        try
        {
            photoUrl = await SavePhotoAsync(master.Id, request.PhotoBase64);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decode/save photo for Content {ContentId}", master.Id);
            return BadRequest(new { success = false, message = "Format foto tidak valid atau gagal diproses server." });
        }

        // 5. Ensure question exists to link foreign key
        var question = master.Questions.FirstOrDefault();
        if (question == null)
        {
            question = new DynamicContentQuestion
            {
                DynamicContentMasterId = master.Id,
                QuestionText = "Live Photo & Greetings",
                QuestionType = "photo",
                SortOrder = 1
            };
            _context.DynamicContentQuestions.Add(question);
            await _context.SaveChangesAsync();
        }

        // 6. Record vote log
        string city = !string.IsNullOrWhiteSpace(request.City) ? request.City.Trim() : "Bandung";
        if (!string.IsNullOrWhiteSpace(request.SiteId))
        {
            var sLower = request.SiteId.ToLowerInvariant();
            if (sLower.Contains("bandung") || sLower.Contains("bdg") || sLower.Contains("sate")) city = "Gedung Sate Bandung";
            else if (sLower.Contains("surabaya") || sLower.Contains("sby")) city = "Surabaya";
            else if (sLower.Contains("jakarta") || sLower.Contains("jkt")) city = "Jakarta";
            else city = request.SiteId;
        }

        var voteLog = new DynamicContentVoteLog
        {
            DynamicContentMasterId = master.Id,
            QuestionId = question.Id,
            OptionKey = "PHOTO",
            TextResponse = moderationResult.SanitizedText,
            PhotoUrl = photoUrl,
            UserName = string.IsNullOrWhiteSpace(request.UserName) ? "Anonim" : request.UserName.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            SiteId = string.IsNullOrWhiteSpace(request.SiteId) ? "Default" : request.SiteId.Trim(),
            City = city,
            DeviceFingerprint = request.DeviceFingerprint.Trim(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            DeviceType = "Mobile",
            DeviceBrand = request.DeviceBrand,
            DeviceModel = request.DeviceModel,
            OperatingSystem = request.OperatingSystem,
            BrowserName = request.BrowserName,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            LocationDetected = request.Latitude.HasValue && request.Longitude.HasValue,
            VotedAt = DateTime.UtcNow
        };

        _context.DynamicContentVoteLogs.Add(voteLog);
        await _context.SaveChangesAsync();

        // 7. Prepare DTO and Broadcast via SignalR
        var itemDto = new PhotoCommentItemDto
        {
            Id = voteLog.Id,
            UserName = voteLog.UserName,
            CommentText = voteLog.TextResponse ?? string.Empty,
            PhotoUrl = voteLog.PhotoUrl ?? string.Empty,
            SiteId = voteLog.SiteId,
            TimeAgo = "Baru saja",
            PostedAt = voteLog.VotedAt
        };

        var totalPhotos = await _context.DynamicContentVoteLogs
            .CountAsync(v => v.DynamicContentMasterId == master.Id && !string.IsNullOrEmpty(v.PhotoUrl));

        var totalParticipants = await _context.DynamicContentVoteLogs
            .Where(v => v.DynamicContentMasterId == master.Id)
            .Select(v => v.DeviceFingerprint)
            .Distinct()
            .CountAsync();

        var broadcastPayload = new
        {
            item = itemDto,
            totalPhotos = totalPhotos,
            totalParticipants = totalParticipants,
            siteId = voteLog.SiteId
        };

        await _hubContext.Clients.Group($"content_{master.Id}_{voteLog.SiteId}")
            .SendAsync("NewPhotoComment", broadcastPayload);

        await _hubContext.Clients.Group($"content_{master.Id}")
            .SendAsync("NewPhotoComment", broadcastPayload);

        _logger.LogInformation("New photo comment #{Id} posted by '{User}' on '{ContentId}'",
            voteLog.Id, voteLog.UserName, master.Id);

        return Ok(new
        {
            success = true,
            message = "Foto & ucapan Anda berhasil dikirim dan akan segera tampil di layar videotron!",
            item = itemDto,
            remainingSeconds = gapMinutes * 60
        });
    }

    /// <summary>
    /// Retrieve feed of photos for screen billboard or web viewer.
    /// </summary>
    [HttpGet("feed")]
    public async Task<IActionResult> GetFeed([FromQuery] string id, [FromQuery] string? site = null)
    {
        var master = await _context.DynamicContentMasters
            .FirstOrDefaultAsync(m => m.Id == id);

        if (master == null)
        {
            return NotFound(new { message = $"Kampanye dengan ID '{id}' tidak ditemukan." });
        }

        var query = _context.DynamicContentVoteLogs
            .Where(v => v.DynamicContentMasterId == id && !string.IsNullOrEmpty(v.PhotoUrl));

        if (!string.IsNullOrWhiteSpace(site) && !site.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            // Prefer site match if enough data, else national
            var siteLogs = await query.Where(v => v.SiteId == site).OrderByDescending(v => v.VotedAt).Take(30).ToListAsync();
            if (siteLogs.Count < 5)
            {
                // Fallback to all sites
                siteLogs = await query.OrderByDescending(v => v.VotedAt).Take(30).ToListAsync();
            }
            query = siteLogs.AsQueryable();
        }
        else
        {
            query = query.OrderByDescending(v => v.VotedAt).Take(30);
        }

        var logs = await (query is IAsyncEnumerable<DynamicContentVoteLog> ? query.ToListAsync() : Task.FromResult(query.ToList()));

        var totalPhotos = await _context.DynamicContentVoteLogs
            .CountAsync(v => v.DynamicContentMasterId == id && !string.IsNullOrEmpty(v.PhotoUrl));

        var totalParticipants = await _context.DynamicContentVoteLogs
            .Where(v => v.DynamicContentMasterId == id)
            .Select(v => v.DeviceFingerprint)
            .Distinct()
            .CountAsync();

        var now = DateTime.UtcNow;
        var items = logs.Select(l => new PhotoCommentItemDto
        {
            Id = l.Id,
            UserName = l.UserName,
            CommentText = l.TextResponse ?? string.Empty,
            PhotoUrl = l.PhotoUrl ?? string.Empty,
            SiteId = l.SiteId,
            PostedAt = l.VotedAt,
            TimeAgo = FormatTimeAgo(now - l.VotedAt)
        }).ToList();

        var feed = new PhotoCommentFeedDto
        {
            ContentId = master.Id,
            CampaignTitle = master.CampaignTitle,
            ClientName = master.ClientName,
            BannerImageUrl = master.BannerImageUrl,
            BrandLogoUrl = master.BrandLogoUrl,
            WelcomeMessage = master.WelcomeMessage,
            InstructionComment = master.InstructionComment,
            SiteId = site ?? "Nasional",
            TotalParticipants = totalParticipants,
            TotalPhotos = totalPhotos,
            RecentPhotos = items
        };

        return Ok(feed);
    }

    private async Task<string> SavePhotoAsync(string contentId, string base64Data)
    {
        // Strip data:image/...;base64, header if present
        var commaIdx = base64Data.IndexOf(',');
        var cleanBase64 = commaIdx >= 0 ? base64Data[(commaIdx + 1)..] : base64Data;
        var bytes = Convert.FromBase64String(cleanBase64.Trim());

        // Basic size guard: max 10MB
        if (bytes.Length > 10 * 1024 * 1024)
        {
            throw new InvalidOperationException("Ukuran foto melebihi batas 10MB.");
        }

        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "photos");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var fileName = $"photo_{contentId}_{Guid.NewGuid():N}.jpg";
        var filePath = Path.Combine(uploadsFolder, fileName);

        await System.IO.File.WriteAllBytesAsync(filePath, bytes);

        return $"/uploads/photos/{fileName}";
    }

    private static string FormatTimeAgo(TimeSpan span)
    {
        if (span.TotalSeconds < 60) return "Baru saja";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} menit lalu";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} jam lalu";
        return $"{(int)span.TotalDays} hari lalu";
    }
}
