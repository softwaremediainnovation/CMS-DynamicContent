using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using CmsBillboard.DynamicContent.Data;
using CmsBillboard.DynamicContent.Hubs;
using CmsBillboard.DynamicContent.Models;

namespace CmsBillboard.DynamicContent.Services;

public class PhotoModerationService : IPhotoModerationService
{
    private readonly ILogger<PhotoModerationService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<DynamicContentHub> _hubContext;
    private readonly PhotoModerationConfig _config;

    // Master in-memory cache
    private readonly ConcurrentDictionary<int, PhotoModerationItem> _photosById = new();

    // Queues per Site/City
    private readonly ConcurrentDictionary<string, ConcurrentQueue<int>> _pendingPerSite = new();
    private readonly ConcurrentDictionary<string, List<PhotoModerationItem>> _approvedPerSite = new();
    private readonly ConcurrentDictionary<string, List<PhotoModerationItem>> _rejectedPerSite = new();
    private readonly ConcurrentDictionary<string, List<(int Id, DateTime RemovedAt)>> _removedPerSite = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastApprovedClientTime = new();
    private readonly object _lock = new();

    public PhotoModerationService(
        ILogger<PhotoModerationService> logger,
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        IHubContext<DynamicContentHub> hubContext,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;

        _config = new PhotoModerationConfig
        {
            Mode = configuration["AiModeration:Mode"] ?? "manual",
            GeminiApiKey = configuration["AiModeration:ApiKey"]
                           ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                           ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
                           ?? string.Empty,
            GeminiModel = configuration["AiModeration:Model"] ?? "gemini-1.5-flash",
            AiAutoApproveSafePhotos = true,
            AiRejectUnsafePhotos = false,
            CooldownMinutes = int.TryParse(configuration["AiModeration:CooldownMinutes"], out var cm) ? cm : 15
        };

        _logger.LogInformation("PhotoModerationService initialized. Default Mode: {Mode}, AI Key set: {HasKey}",
            _config.Mode, !string.IsNullOrWhiteSpace(_config.GeminiApiKey));
    }

    public async Task InitializeFromDatabaseAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Backfill legacy rows with empty status to "approved"
            var unassignedLegacy = await db.DynamicContentVoteLogs
                .Where(v => string.IsNullOrEmpty(v.Status) && !string.IsNullOrEmpty(v.PhotoUrl))
                .ToListAsync();

            if (unassignedLegacy.Count > 0)
            {
                foreach (var log in unassignedLegacy)
                {
                    log.Status = "approved";
                    log.ModerationMode = "auto";
                    log.ApprovedAt = log.VotedAt;
                }
                await db.SaveChangesAsync();
                _logger.LogInformation("Migrated {Count} legacy photo vote logs to status 'approved'.", unassignedLegacy.Count);
            }

            // 1. Load pending / flagged
            var pendingLogs = await db.DynamicContentVoteLogs
                .Where(v => (v.Status == "pending" || v.Status == "flagged") && !string.IsNullOrEmpty(v.PhotoUrl))
                .OrderBy(v => v.VotedAt)
                .ToListAsync();

            foreach (var p in pendingLogs)
            {
                var item = MapEntityToItem(p);
                _photosById[item.Id] = item;
                var queue = _pendingPerSite.GetOrAdd(NormalizeSite(item.SiteId), _ => new ConcurrentQueue<int>());
                queue.Enqueue(item.Id);
            }

            // 2. Load recent approved photos (up to 200)
            var approvedLogs = await db.DynamicContentVoteLogs
                .Where(v => v.Status == "approved" && !string.IsNullOrEmpty(v.PhotoUrl))
                .OrderByDescending(v => v.ApprovedAt ?? v.VotedAt)
                .Take(200)
                .ToListAsync();

            foreach (var p in approvedLogs.OrderBy(v => v.ApprovedAt ?? v.VotedAt))
            {
                var item = MapEntityToItem(p);
                _photosById[item.Id] = item;
                AddApprovedPhoto(item.SiteId, item);

                if (item.ApprovedAt.HasValue && !string.IsNullOrWhiteSpace(item.DeviceFingerprint))
                {
                    _lastApprovedClientTime[$"fp_{item.DeviceFingerprint}_{NormalizeSite(item.SiteId)}"] = item.ApprovedAt.Value;
                }
            }

            _logger.LogInformation("PhotoModerationService initialized from DB: {Pending} pending, {Approved} approved restored.",
                pendingLogs.Count, approvedLogs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing PhotoModerationService from database.");
        }
    }

    public PhotoModerationConfig GetConfig()
    {
        lock (_lock)
        {
            return new PhotoModerationConfig
            {
                Mode = _config.Mode,
                GeminiApiKey = _config.GeminiApiKey,
                GeminiModel = _config.GeminiModel,
                AiAutoApproveSafePhotos = _config.AiAutoApproveSafePhotos,
                AiRejectUnsafePhotos = _config.AiRejectUnsafePhotos,
                CooldownMinutes = _config.CooldownMinutes
            };
        }
    }

    public void UpdateConfig(PhotoModerationConfig config)
    {
        lock (_lock)
        {
            _config.Mode = (config.Mode ?? "manual").ToLowerInvariant() == "ai" ? "ai" : "manual";
            if (!string.IsNullOrWhiteSpace(config.GeminiApiKey))
            {
                _config.GeminiApiKey = config.GeminiApiKey.Trim();
            }
            if (!string.IsNullOrWhiteSpace(config.GeminiModel))
            {
                _config.GeminiModel = config.GeminiModel.Trim();
            }
            _config.AiAutoApproveSafePhotos = config.AiAutoApproveSafePhotos;
            _config.AiRejectUnsafePhotos = config.AiRejectUnsafePhotos;
            if (config.CooldownMinutes > 0)
            {
                _config.CooldownMinutes = config.CooldownMinutes;
            }
        }

        _logger.LogInformation("PhotoModerationService config updated. Mode: {Mode}, Cooldown: {Cooldown}m", _config.Mode, _config.CooldownMinutes);
    }

    public async Task<PhotoSubmissionResult> ProcessSubmissionAsync(DynamicContentVoteLog voteLog, string? rawBase64 = null)
    {
        var item = MapEntityToItem(voteLog);
        var siteKey = NormalizeSite(item.SiteId);

        // 1. AI Auto-Check Mode
        if (_config.Mode == "ai")
        {
            if (string.IsNullOrWhiteSpace(_config.GeminiApiKey))
            {
                _logger.LogWarning("AI Moderation aktif tapi Gemini API Key belum diisi. Alihkan ke pending manual.");
                return await EnqueueManualAsync(voteLog, item, "API Key AI belum diset. Menunggu approval manual admin.");
            }

            var aiResult = await InspectPhotoWithGeminiAsync(rawBase64 ?? voteLog.PhotoUrl, voteLog.TextResponse);
            voteLog.ModerationMode = "ai";
            voteLog.AiConfidence = aiResult.Confidence;
            voteLog.AiReviewReason = aiResult.Reason;

            item.ModerationMode = "ai";
            item.AiConfidence = aiResult.Confidence;
            item.AiReviewReason = aiResult.Reason;

            if (aiResult.IsSafe)
            {
                // Safe -> AUTO APPROVE!
                voteLog.Status = "approved";
                voteLog.ApprovedAt = DateTime.UtcNow;

                item.Status = "approved";
                item.ApprovedAt = voteLog.ApprovedAt;

                _photosById[item.Id] = item;
                AddApprovedPhoto(item.SiteId, item);
                RecordClientApproval(voteLog.DeviceFingerprint, voteLog.IpAddress, item.SiteId);

                await PersistVoteLogAsync(voteLog);
                await BroadcastNewPhotoAsync(voteLog, item);

                _logger.LogInformation("Photo #{Id} Auto-Approved by AI. Reason: {Reason}", item.Id, aiResult.Reason);

                return new PhotoSubmissionResult
                {
                    Success = true,
                    Status = "approved",
                    Message = "Foto Anda lolos verifikasi AI dan langsung disiarkan di layar Videotron!",
                    Item = item,
                    RemainingCooldownSeconds = _config.CooldownMinutes * 60
                };
            }
            else
            {
                // Not Safe
                if (_config.AiRejectUnsafePhotos)
                {
                    voteLog.Status = "rejected";
                    item.Status = "rejected";

                    _photosById[item.Id] = item;
                    AddRejectedPhoto(item.SiteId, item);
                    await PersistVoteLogAsync(voteLog);

                    _logger.LogWarning("Photo #{Id} Auto-Rejected by AI. Reason: {Reason}", item.Id, aiResult.Reason);

                    return new PhotoSubmissionResult
                    {
                        Success = false,
                        Status = "rejected",
                        Message = $"Foto belum memenuhi ketentuan penayangan: {aiResult.Reason}",
                        Item = item
                    };
                }
                else
                {
                    // Flagged for manual review
                    voteLog.Status = "flagged";
                    item.Status = "flagged";

                    _photosById[item.Id] = item;
                    var queue = _pendingPerSite.GetOrAdd(siteKey, _ => new ConcurrentQueue<int>());
                    queue.Enqueue(item.Id);
                    await PersistVoteLogAsync(voteLog);

                    _logger.LogWarning("Photo #{Id} Flagged by AI for review. Reason: {Reason}", item.Id, aiResult.Reason);

                    return new PhotoSubmissionResult
                    {
                        Success = true,
                        Status = "flagged",
                        Message = "Foto Anda ditandai untuk peninjauan cepat moderator sebelum ditayangkan.",
                        Item = item
                    };
                }
            }
        }

        // 2. Manual Mode (Default)
        return await EnqueueManualAsync(voteLog, item, "Menunggu peninjauan oleh moderator.");
    }

    private async Task<PhotoSubmissionResult> EnqueueManualAsync(DynamicContentVoteLog voteLog, PhotoModerationItem item, string reason)
    {
        voteLog.Status = "pending";
        voteLog.ModerationMode = "manual";
        voteLog.AiReviewReason = reason;

        item.Status = "pending";
        item.ModerationMode = "manual";
        item.AiReviewReason = reason;

        _photosById[item.Id] = item;
        var queue = _pendingPerSite.GetOrAdd(NormalizeSite(item.SiteId), _ => new ConcurrentQueue<int>());
        queue.Enqueue(item.Id);

        await PersistVoteLogAsync(voteLog);

        _logger.LogInformation("Photo #{Id} queued for manual moderation for Site {Site}", item.Id, item.SiteId);

        return new PhotoSubmissionResult
        {
            Success = true,
            Status = "pending",
            Message = "Foto & ucapan Anda berhasil dikirim dan sedang menunggu validasi admin.",
            Item = item
        };
    }

    public async Task<bool> ApprovePhotoAsync(int id)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entity = await db.DynamicContentVoteLogs.FirstOrDefaultAsync(v => v.Id == id);
        if (entity == null) return false;

        entity.Status = "approved";
        entity.ApprovedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var item = MapEntityToItem(entity);
        _photosById[item.Id] = item;
        AddApprovedPhoto(item.SiteId, item);
        RecordClientApproval(entity.DeviceFingerprint, entity.IpAddress, item.SiteId);

        // Broadcast immediately to Videotron Screen!
        await BroadcastNewPhotoAsync(entity, item);

        _logger.LogInformation("Photo #{Id} manually approved by admin. Sent to videotron screens.", id);
        return true;
    }

    public async Task<bool> RejectPhotoAsync(int id, string? reason = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entity = await db.DynamicContentVoteLogs.FirstOrDefaultAsync(v => v.Id == id);
        if (entity == null) return false;

        entity.Status = "rejected";
        entity.AiReviewReason = !string.IsNullOrWhiteSpace(reason)
            ? reason
            : "Foto belum memenuhi pedoman penayangan di videotron.";
        await db.SaveChangesAsync();

        var item = MapEntityToItem(entity);
        _photosById[item.Id] = item;
        AddRejectedPhoto(item.SiteId, item);

        _logger.LogInformation("Photo #{Id} manually rejected by admin. Reason: {Reason}", id, entity.AiReviewReason);
        return true;
    }

    public async Task<bool> TakeDownPhotoAsync(int id)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entity = await db.DynamicContentVoteLogs.FirstOrDefaultAsync(v => v.Id == id);
        if (entity == null) return false;

        entity.Status = "takedown";
        entity.TakenDownAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var item = MapEntityToItem(entity);
        _photosById[item.Id] = item;

        var siteKey = NormalizeSite(entity.SiteId);
        lock (_lock)
        {
            if (_approvedPerSite.TryGetValue(siteKey, out var approvedList))
            {
                approvedList.RemoveAll(p => p.Id == id);
            }
            if (_approvedPerSite.TryGetValue("ALL", out var allList))
            {
                allList.RemoveAll(p => p.Id == id);
            }

            var removedList = _removedPerSite.GetOrAdd(siteKey, _ => new List<(int Id, DateTime RemovedAt)>());
            removedList.Add((id, entity.TakenDownAt.Value));
        }

        // Broadcast removal event to screen!
        await BroadcastPhotoRemovedAsync(entity.DynamicContentMasterId, entity.SiteId, id);

        _logger.LogInformation("Photo #{Id} TAKEN DOWN by admin from videotron screens.", id);
        return true;
    }

    public async Task<int> ApproveAllAsync(string? siteId = null, string? contentId = null)
    {
        var pending = GetPendingPhotos(siteId, contentId).ToList();
        int count = 0;
        foreach (var p in pending)
        {
            if (await ApprovePhotoAsync(p.Id)) count++;
        }
        return count;
    }

    public async Task<int> RejectAllAsync(string? siteId = null, string? contentId = null)
    {
        var pending = GetPendingPhotos(siteId, contentId).ToList();
        int count = 0;
        foreach (var p in pending)
        {
            if (await RejectPhotoAsync(p.Id, "Ditolak bersamaan oleh moderator.")) count++;
        }
        return count;
    }

    public IEnumerable<PhotoModerationItem> GetPendingPhotos(string? siteId = null, string? contentId = null)
    {
        var normSite = NormalizeSite(siteId);

        return _photosById.Values
            .Where(p => (p.Status == "pending" || p.Status == "flagged")
                        && (normSite == "ALL" || NormalizeSite(p.SiteId) == normSite)
                        && (string.IsNullOrWhiteSpace(contentId) || p.DynamicContentMasterId == contentId))
            .OrderBy(p => p.VotedAt)
            .ToList();
    }

    public IEnumerable<PhotoModerationItem> GetApprovedPhotos(string? siteId = null, string? contentId = null, int limit = 30)
    {
        var normSite = NormalizeSite(siteId);

        return _photosById.Values
            .Where(p => p.Status == "approved"
                        && (normSite == "ALL" || NormalizeSite(p.SiteId) == normSite)
                        && (string.IsNullOrWhiteSpace(contentId) || p.DynamicContentMasterId == contentId))
            .OrderByDescending(p => p.ApprovedAt ?? p.VotedAt)
            .Take(limit)
            .ToList();
    }

    public IEnumerable<PhotoModerationItem> GetHistoryPhotos(string? siteId = null, string? contentId = null, int limit = 50)
    {
        var normSite = NormalizeSite(siteId);

        return _photosById.Values
            .Where(p => (normSite == "ALL" || NormalizeSite(p.SiteId) == normSite)
                        && (string.IsNullOrWhiteSpace(contentId) || p.DynamicContentMasterId == contentId))
            .OrderByDescending(p => p.VotedAt)
            .Take(limit)
            .ToList();
    }

    public PhotoModerationItem? GetPhotoById(int id)
    {
        _photosById.TryGetValue(id, out var item);
        return item;
    }

    public PhotoModerationStats GetStats(string? siteId = null, string? contentId = null)
    {
        var normSite = NormalizeSite(siteId);
        var all = _photosById.Values
            .Where(p => (normSite == "ALL" || NormalizeSite(p.SiteId) == normSite)
                        && (string.IsNullOrWhiteSpace(contentId) || p.DynamicContentMasterId == contentId))
            .ToList();

        return new PhotoModerationStats
        {
            SiteId = normSite,
            CurrentMode = _config.Mode,
            IsAiConfigured = !string.IsNullOrWhiteSpace(_config.GeminiApiKey),
            PendingCount = all.Count(p => p.Status == "pending" || p.Status == "flagged"),
            ApprovedCount = all.Count(p => p.Status == "approved"),
            RejectedCount = all.Count(p => p.Status == "rejected"),
            TotalSubmissions = all.Count
        };
    }

    private void AddApprovedPhoto(string siteId, PhotoModerationItem item)
    {
        lock (_lock)
        {
            var key = NormalizeSite(siteId);
            var list = _approvedPerSite.GetOrAdd(key, _ => new List<PhotoModerationItem>());
            list.RemoveAll(p => p.Id == item.Id);
            list.Insert(0, item);
            if (list.Count > 200) list.RemoveRange(200, list.Count - 200);

            var allList = _approvedPerSite.GetOrAdd("ALL", _ => new List<PhotoModerationItem>());
            allList.RemoveAll(p => p.Id == item.Id);
            allList.Insert(0, item);
            if (allList.Count > 200) allList.RemoveRange(200, allList.Count - 200);
        }
    }

    private void AddRejectedPhoto(string siteId, PhotoModerationItem item)
    {
        lock (_lock)
        {
            var key = NormalizeSite(siteId);
            var list = _rejectedPerSite.GetOrAdd(key, _ => new List<PhotoModerationItem>());
            list.RemoveAll(p => p.Id == item.Id);
            list.Insert(0, item);
            if (list.Count > 100) list.RemoveRange(100, list.Count - 100);
        }
    }

    private void RecordClientApproval(string? fingerprint, string? clientIp, string? siteId)
    {
        var now = DateTime.UtcNow;
        var siteKey = NormalizeSite(siteId);

        if (!string.IsNullOrWhiteSpace(fingerprint))
        {
            _lastApprovedClientTime[$"fp_{fingerprint}_{siteKey}"] = now;
            _lastApprovedClientTime[$"fp_{fingerprint}_ALL"] = now;
        }

        if (!string.IsNullOrWhiteSpace(clientIp) && clientIp != "127.0.0.1" && clientIp != "::1")
        {
            _lastApprovedClientTime[$"ip_{clientIp}_{siteKey}"] = now;
            _lastApprovedClientTime[$"ip_{clientIp}_ALL"] = now;
        }
    }

    private async Task PersistVoteLogAsync(DynamicContentVoteLog voteLog)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var entity = await db.DynamicContentVoteLogs.FirstOrDefaultAsync(v => v.Id == voteLog.Id);
            if (entity != null)
            {
                entity.Status = voteLog.Status;
                entity.ModerationMode = voteLog.ModerationMode;
                entity.AiReviewReason = voteLog.AiReviewReason;
                entity.AiConfidence = voteLog.AiConfidence;
                entity.ApprovedAt = voteLog.ApprovedAt;
                entity.TakenDownAt = voteLog.TakenDownAt;
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist voteLog #{Id} moderation state to DB", voteLog.Id);
        }
    }

    private async Task BroadcastNewPhotoAsync(DynamicContentVoteLog voteLog, PhotoModerationItem item)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var totalPhotos = await db.DynamicContentVoteLogs
                .CountAsync(v => v.DynamicContentMasterId == voteLog.DynamicContentMasterId && v.Status == "approved" && !string.IsNullOrEmpty(v.PhotoUrl));

            var totalParticipants = await db.DynamicContentVoteLogs
                .Where(v => v.DynamicContentMasterId == voteLog.DynamicContentMasterId && v.Status == "approved")
                .Select(v => v.DeviceFingerprint)
                .Distinct()
                .CountAsync();

            var itemDto = new PhotoCommentItemDto
            {
                Id = item.Id,
                UserName = item.UserName,
                CommentText = item.CommentText,
                PhotoUrl = item.PhotoUrl,
                SiteId = item.SiteId,
                TimeAgo = "Baru saja",
                PostedAt = item.ApprovedAt ?? item.VotedAt
            };

            var broadcastPayload = new
            {
                item = itemDto,
                totalPhotos = totalPhotos,
                totalParticipants = totalParticipants,
                siteId = voteLog.SiteId
            };

            await _hubContext.Clients.Group($"content_{voteLog.DynamicContentMasterId}")
                .SendAsync("NewPhotoComment", broadcastPayload);

            _logger.LogInformation("Broadcasted approved photo #{Id} to SignalR group content_{MasterId}",
                item.Id, voteLog.DynamicContentMasterId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast NewPhotoComment via SignalR");
        }
    }

    private async Task BroadcastPhotoRemovedAsync(string contentId, string siteId, int photoId)
    {
        try
        {
            var payload = new { photoId, siteId, contentId };
            await _hubContext.Clients.Group($"content_{contentId}")
                .SendAsync("PhotoTakenDown", payload);
            await _hubContext.Clients.All
                .SendAsync("PhotoTakenDown", payload);

            _logger.LogInformation("Broadcasted PhotoTakenDown signal for Photo #{PhotoId}", photoId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast PhotoTakenDown signal");
        }
    }

    private static string NormalizeSite(string? siteId)
    {
        if (string.IsNullOrWhiteSpace(siteId) || siteId.Equals("all", StringComparison.OrdinalIgnoreCase))
            return "ALL";
        return siteId.Trim();
    }

    private static PhotoModerationItem MapEntityToItem(DynamicContentVoteLog v)
    {
        var now = DateTime.UtcNow;
        var dateRef = v.ApprovedAt ?? v.VotedAt;
        var span = now - dateRef;
        var timeAgo = span.TotalSeconds < 60 ? "Baru saja" :
                      span.TotalMinutes < 60 ? $"{(int)span.TotalMinutes}m lalu" :
                      span.TotalHours < 24 ? $"{(int)span.TotalHours}j lalu" : $"{(int)span.TotalDays}h lalu";

        return new PhotoModerationItem
        {
            Id = v.Id,
            DynamicContentMasterId = v.DynamicContentMasterId,
            UserName = string.IsNullOrWhiteSpace(v.UserName) ? "Anonim" : v.UserName,
            PhoneNumber = v.PhoneNumber,
            CommentText = v.TextResponse ?? string.Empty,
            PhotoUrl = v.PhotoUrl ?? string.Empty,
            SiteId = string.IsNullOrWhiteSpace(v.SiteId) ? "Default" : v.SiteId,
            City = string.IsNullOrWhiteSpace(v.City) ? "Nasional" : v.City,
            Status = string.IsNullOrWhiteSpace(v.Status) ? "approved" : v.Status,
            ModerationMode = string.IsNullOrWhiteSpace(v.ModerationMode) ? "auto" : v.ModerationMode,
            AiReviewReason = v.AiReviewReason,
            AiConfidence = v.AiConfidence,
            DeviceFingerprint = v.DeviceFingerprint,
            IpAddress = v.IpAddress,
            VotedAt = v.VotedAt,
            ApprovedAt = v.ApprovedAt,
            TakenDownAt = v.TakenDownAt,
            TimeAgo = timeAgo
        };
    }

    // =========================================================================
    // GEMINI AI VISION INSPECTOR UNTUK FOTO SELFIE & UCAPAN
    // =========================================================================
    private async Task<(bool IsSafe, string Reason, double Confidence)> InspectPhotoWithGeminiAsync(string? photoData, string? commentText)
    {
        if (string.IsNullOrWhiteSpace(photoData))
        {
            return (true, "Tidak ada data foto.", 1.0);
        }

        try
        {
            string base64 = photoData;
            string mimeType = "image/jpeg";

            if (photoData.Contains(","))
            {
                var parts = photoData.Split(',');
                var header = parts[0];
                base64 = parts[1];
                if (header.Contains("image/png")) mimeType = "image/png";
                else if (header.Contains("image/webp")) mimeType = "image/webp";
            }
            else if (photoData.StartsWith("/") || photoData.StartsWith("http"))
            {
                // If it's a file path on local disk, read bytes
                var webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var relativePath = photoData.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.Combine(webRoot, relativePath);

                if (File.Exists(fullPath))
                {
                    var fileBytes = await File.ReadAllBytesAsync(fullPath);
                    base64 = Convert.ToBase64String(fileBytes);
                }
            }

            var apiKey = _config.GeminiApiKey;
            var model = string.IsNullOrWhiteSpace(_config.GeminiModel) ? "gemini-1.5-flash" : _config.GeminiModel;
            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var prompt =
                "You are an automated content moderation AI for an interactive public DOOH (Digital Out-Of-Home) billboard display in Indonesia.\n" +
                "Audience members take a selfie and send a warm greeting message to be displayed on giant public LED videotron screens in prominent city centers.\n\n" +
                $"User Greeting Message: \"{commentText ?? ""}\"\n\n" +
                "Analyze this user photo and greeting strictly for:\n" +
                "1. Genitalia, nudity, exposed breasts/buttocks, sexually explicit poses, or vulgar clothing.\n" +
                "2. Offensive hand gestures (middle finger, obscene signs, gang signs).\n" +
                "3. Profanity, slurs, hate speech, political agitation, toxic phrases (in Indonesian, English, or slang).\n" +
                "4. Weapons (guns, knives), blood, violence, gore, alcohol, drugs, or illegal substances.\n" +
                "5. Hate group symbols, racist iconography, or harassment.\n" +
                "6. Irrelevant offensive memes or troll pictures that violate public decency.\n\n" +
                "Normal selfies, smiles, friendly greetings, cultural attire, funny innocent faces, and normal everyday photos ARE CONSIDERED SAFE.\n" +
                "Output MUST BE pure JSON (no markdown fences, just JSON):\n" +
                "{\"isSafe\": true/false, \"reason\": \"Indonesian explanation max 15 words\", \"confidence\": 0.95}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt },
                            new
                            {
                                inlineData = new
                                {
                                    mimeType = mimeType,
                                    data = base64
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json"
                }
            };

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(endpoint, jsonContent);

            if (!response.IsSuccessStatusCode)
            {
                var errStr = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Gemini API error {StatusCode}: {Error}", response.StatusCode, errStr);
                return (false, $"Gagal menghubungi AI ({response.StatusCode}). Perlu ditinjau manual.", 0.5);
            }

            var respJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(respJson);
            var root = doc.RootElement;

            var text = root
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(text))
            {
                return (true, "AI tidak memberikan respon teks, lolos default.", 0.7);
            }

            text = text.Trim();
            if (text.StartsWith("```json")) text = text[7..];
            if (text.StartsWith("```")) text = text[3..];
            if (text.EndsWith("```")) text = text[..^3];
            text = text.Trim();

            var parsed = JsonNode.Parse(text);
            var isSafe = parsed?["isSafe"]?.GetValue<bool>() ?? true;
            var reason = parsed?["reason"]?.GetValue<string>() ?? (isSafe ? "Foto dan ucapan aman untuk tayang." : "Foto/ucapan terdeteksi tidak memenuhi ketentuan.");
            var conf = parsed?["confidence"]?.GetValue<double>() ?? 0.9;

            return (isSafe, reason, conf);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while analyzing photo with Gemini AI.");
            return (false, "Error analisis AI. Dialihkan ke antrean manual.", 0.5);
        }
    }
}
