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

public class PetModerationService : IPetModerationService
{
    private readonly ILogger<PetModerationService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecurityTokenService _tokenService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWebHostEnvironment _env;
    private readonly IHubContext<DynamicContentHub> _hubContext;
    private readonly PetModerationConfig _config;

    // Master storage pet by Id (In-Memory Cache synced with Database)
    private readonly ConcurrentDictionary<string, PetSpawnItem> _petsById = new();

    // Pet lists per room (room -> list of IDs in order)
    private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _pendingPerRoom = new();
    private readonly ConcurrentDictionary<string, List<PetSpawnItem>> _approvedPerRoom = new();
    private readonly ConcurrentDictionary<string, List<PetSpawnItem>> _rejectedPerRoom = new();
    private readonly ConcurrentDictionary<string, List<(string Id, DateTime RemovedAt)>> _removedPerRoom = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastApprovedClientTime = new();
    private readonly object _lock = new();

    public PetModerationService(
        ILogger<PetModerationService> logger,
        IHttpClientFactory httpClientFactory,
        ISecurityTokenService tokenService,
        IServiceScopeFactory scopeFactory,
        IWebHostEnvironment env,
        IHubContext<DynamicContentHub> hubContext,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _tokenService = tokenService;
        _scopeFactory = scopeFactory;
        _env = env;
        _hubContext = hubContext;

        // Initialize default config from appsettings / environment
        _config = new PetModerationConfig
        {
            Mode = configuration["AiModeration:Mode"] ?? "manual",
            GeminiApiKey = configuration["AiModeration:ApiKey"] 
                           ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY") 
                           ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY") 
                           ?? string.Empty,
            GeminiModel = configuration["AiModeration:Model"] ?? "gemini-1.5-flash",
            AutoApproveTemplates = true,
            AiAutoApproveSafeDrawings = true,
            AiRejectUnsafeDrawings = false,
            CooldownMinutes = int.TryParse(configuration["AiModeration:CooldownMinutes"], out var cm) ? cm : 15
        };

        _logger.LogInformation("PetModerationService initialized. Default Mode: {Mode}, AI Key set: {HasKey}", 
            _config.Mode, !string.IsNullOrWhiteSpace(_config.GeminiApiKey));
    }

    public async Task InitializeFromDatabaseAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // 1. Load pending / flagged
            var pendingPets = await db.PetSubmissions
                .Where(p => p.Status == "pending" || p.Status == "flagged")
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            foreach (var p in pendingPets)
            {
                var item = MapEntityToItem(p);
                _petsById[item.Id] = item;
                var queue = _pendingPerRoom.GetOrAdd(item.Room, _ => new ConcurrentQueue<string>());
                queue.Enqueue(item.Id);
            }

            // 2. Load recent approved pets (up to 200)
            var approvedPets = await db.PetSubmissions
                .Where(p => p.Status == "approved")
                .OrderByDescending(p => p.ApprovedAt ?? p.CreatedAt)
                .Take(200)
                .ToListAsync();

            foreach (var p in approvedPets.OrderBy(p => p.ApprovedAt ?? p.CreatedAt))
            {
                var item = MapEntityToItem(p);
                _petsById[item.Id] = item;
                AddApprovedPet(item.Room, item);

                // Rehydrate cooldown if recent
                if (item.ApprovedAt.HasValue && !string.IsNullOrWhiteSpace(item.DeviceFingerprint))
                {
                    var key = $"fp_{item.DeviceFingerprint}_{item.Room}";
                    _lastApprovedClientTime[key] = item.ApprovedAt.Value;
                }
            }

            _logger.LogInformation("PetModerationService initialized from DB: {PendingCount} pending, {ApprovedCount} approved pets restored.",
                pendingPets.Count, approvedPets.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing PetModerationService from database. The table might not exist yet.");
        }
    }

    public PetModerationConfig GetConfig()
    {
        lock (_lock)
        {
            return new PetModerationConfig
            {
                Mode = _config.Mode,
                GeminiApiKey = _config.GeminiApiKey,
                GeminiModel = _config.GeminiModel,
                AutoApproveTemplates = _config.AutoApproveTemplates,
                AiAutoApproveSafeDrawings = _config.AiAutoApproveSafeDrawings,
                AiRejectUnsafeDrawings = _config.AiRejectUnsafeDrawings,
                CooldownMinutes = _config.CooldownMinutes
            };
        }
    }

    public void UpdateConfig(PetModerationConfig config)
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
            _config.AutoApproveTemplates = config.AutoApproveTemplates;
            _config.AiAutoApproveSafeDrawings = config.AiAutoApproveSafeDrawings;
            _config.AiRejectUnsafeDrawings = config.AiRejectUnsafeDrawings;
            if (config.CooldownMinutes > 0)
            {
                _config.CooldownMinutes = config.CooldownMinutes;
            }
        }

        _logger.LogInformation("PetModerationService config updated. Mode: {Mode}, Cooldown: {Cooldown}m", _config.Mode, _config.CooldownMinutes);
    }

    public async Task<PetSubmissionResult> SubmitPetAsync(PetSpawnRequest request, string? clientIp = null)
    {
        var room = string.IsNullOrWhiteSpace(request.Room) ? "DEFAULT" : request.Room.Trim().ToUpper();

        // Validasi jika ada token kriptografis
        var candidateToken = !string.IsNullOrWhiteSpace(request.Token) ? request.Token : (request.Room?.Length >= 30 ? request.Room : null);
        if (!string.IsNullOrWhiteSpace(candidateToken))
        {
            var (isValid, payload, _) = _tokenService.ValidateVoteToken(candidateToken);
            if (isValid && payload != null && !string.IsNullOrWhiteSpace(payload.SiteId))
            {
                room = payload.SiteId.Trim().ToUpper();
            }
        }

        // Cek Jeda Waktu (Cooldown 15 Menit setelah disetujui sebelumnya)
        var (canSubmit, remSec) = CheckCooldown(request.DeviceFingerprint, clientIp, room);
        if (!canSubmit)
        {
            return new PetSubmissionResult
            {
                Success = false,
                Status = "cooldown",
                Message = $"Perangkat Anda sedang dalam masa jeda {_config.CooldownMinutes} menit.",
                RemainingCooldownSeconds = remSec
            };
        }

        // Cek jika masih ada pet dari perangkat yang sama yang sedang menunggu verifikasi (Pending / Flagged)
        if (!string.IsNullOrWhiteSpace(request.DeviceFingerprint))
        {
            var hasPending = _petsById.Values.Any(p =>
                (p.Status == "pending" || p.Status == "flagged") &&
                p.Room == room &&
                p.DeviceFingerprint == request.DeviceFingerprint);

            if (hasPending)
            {
                return new PetSubmissionResult
                {
                    Success = false,
                    Status = "pending_existing",
                    Message = "Anda masih memiliki karya pet yang sedang dalam antrean peninjauan. Mohon tunggu proses validasi selesai."
                };
            }
        }

        var pet = new PetSpawnItem
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Room = room,
            PlayerName = request.PlayerName.Trim().Length > 20 ? request.PlayerName.Trim()[..20] : request.PlayerName.Trim(),
            PetType = string.IsNullOrWhiteSpace(request.PetType) ? "template" : request.PetType.ToLowerInvariant(),
            TemplateKey = request.TemplateKey ?? "nemo",
            ImageData = request.ImageData,
            ColorHex = request.ColorHex ?? "#F97316",
            DeviceFingerprint = request.DeviceFingerprint,
            ClientIp = clientIp,
            CreatedAt = DateTime.UtcNow
        };

        // Simpan file gambar fisik jika tipe 'draw'
        if (pet.PetType == "draw" && !string.IsNullOrWhiteSpace(pet.ImageData))
        {
            pet.ImageUrl = await SaveDrawingImageAsync(pet.Id, pet.ImageData);
        }

        // 1. Opsi A: Template karakter bawaan langsung Auto-Approve
        if (pet.PetType == "template" && _config.AutoApproveTemplates)
        {
            pet.Status = "approved";
            pet.ModerationMode = "auto_template";
            pet.ApprovedAt = DateTime.UtcNow;
            pet.AiReviewReason = "Template karakter standar langsung disetujui.";

            _petsById[pet.Id] = pet;
            AddApprovedPet(room, pet);
            RecordClientApproval(pet.DeviceFingerprint, pet.ClientIp, pet.Room);
            _ = PersistPetAsync(pet);

            return new PetSubmissionResult
            {
                Success = true,
                Status = "approved",
                Message = "Pet karakter berhasil dicemplungkan ke akuarium!",
                Pet = pet,
                RemainingCooldownSeconds = _config.CooldownMinutes * 60
            };
        }

        // 2. Mode AI untuk Coretan Tangan (Drawing)
        if (pet.PetType == "draw" && _config.Mode == "ai")
        {
            if (string.IsNullOrWhiteSpace(_config.GeminiApiKey))
            {
                _logger.LogWarning("AI Moderation diaktifkan namun Gemini API Key belum diisi. Alihkan ke pending manual.");
                return EnqueueManual(pet, "API Key AI belum diset. Menunggu approval manual admin.");
            }

            var aiResult = await InspectDrawingWithGeminiAsync(pet.ImageData);
            pet.ModerationMode = "ai";
            pet.AiConfidence = aiResult.Confidence;
            pet.AiReviewReason = aiResult.Reason;

            if (aiResult.IsSafe)
            {
                // AI Menyatakan Safe -> Langsung Auto Approve!
                pet.Status = "approved";
                pet.ApprovedAt = DateTime.UtcNow;
                _petsById[pet.Id] = pet;
                AddApprovedPet(room, pet);
                RecordClientApproval(pet.DeviceFingerprint, pet.ClientIp, pet.Room);
                _ = PersistPetAsync(pet);

                _logger.LogInformation("Pet drawing {PetId} Auto-Approved by AI. Reason: {Reason}", pet.Id, aiResult.Reason);

                return new PetSubmissionResult
                {
                    Success = true,
                    Status = "approved",
                    Message = "Gambar kamu lolos verifikasi AI dan langsung berenang di videotron!",
                    Pet = pet,
                    RemainingCooldownSeconds = _config.CooldownMinutes * 60
                };
            }
            else
            {
                // AI Mendeteksi Konten Tidak Pantas
                if (_config.AiRejectUnsafeDrawings)
                {
                    pet.Status = "rejected";
                    _petsById[pet.Id] = pet;
                    AddRejectedPet(room, pet);
                    _ = PersistPetAsync(pet);

                    _logger.LogWarning("Pet drawing {PetId} Auto-Rejected by AI. Reason: {Reason}", pet.Id, aiResult.Reason);

                    return new PetSubmissionResult
                    {
                        Success = false,
                        Status = "rejected",
                        Message = $"Gambar belum memenuhi ketentuan: {aiResult.Reason}",
                        Pet = pet
                    };
                }
                else
                {
                    // Masukkan ke antrean dengan label 'flagged' agar admin dapat meninjau
                    pet.Status = "flagged";
                    _petsById[pet.Id] = pet;
                    var queue = _pendingPerRoom.GetOrAdd(room, _ => new ConcurrentQueue<string>());
                    queue.Enqueue(pet.Id);
                    _ = PersistPetAsync(pet);

                    _logger.LogWarning("Pet drawing {PetId} Flagged by AI. Reason: {Reason}", pet.Id, aiResult.Reason);

                    return new PetSubmissionResult
                    {
                        Success = true,
                        Status = "flagged",
                        Message = "Gambar kamu ditandai untuk peninjauan cepat moderator.",
                        Pet = pet
                    };
                }
            }
        }

        // 3. Mode Manual (Default untuk Gambar): Masuk Antrean Pending
        return EnqueueManual(pet, "Menunggu peninjauan oleh moderator.");
    }

    private PetSubmissionResult EnqueueManual(PetSpawnItem pet, string reason)
    {
        pet.Status = "pending";
        pet.ModerationMode = "manual";
        pet.AiReviewReason = reason;

        _petsById[pet.Id] = pet;
        var queue = _pendingPerRoom.GetOrAdd(pet.Room, _ => new ConcurrentQueue<string>());
        queue.Enqueue(pet.Id);
        _ = PersistPetAsync(pet);

        _logger.LogInformation("Pet drawing {PetId} queued for manual moderation in room {Room}", pet.Id, pet.Room);

        return new PetSubmissionResult
        {
            Success = true,
            Status = "pending",
            Message = "Gambar kamu berhasil dikirim dan sedang menunggu validasi admin.",
            Pet = pet
        };
    }

    private void AddApprovedPet(string room, PetSpawnItem pet)
    {
        lock (_lock)
        {
            var list = _approvedPerRoom.GetOrAdd(room, _ => new List<PetSpawnItem>());
            // Prevent duplicate
            list.RemoveAll(p => p.Id == pet.Id);
            list.Add(pet);
            // Batasi memory agar tidak menumpuk tanpa batas (keep last 200 pets)
            if (list.Count > 200)
            {
                list.RemoveRange(0, list.Count - 200);
            }
        }
    }

    private void AddRejectedPet(string room, PetSpawnItem pet)
    {
        lock (_lock)
        {
            var list = _rejectedPerRoom.GetOrAdd(room, _ => new List<PetSpawnItem>());
            list.RemoveAll(p => p.Id == pet.Id);
            list.Add(pet);
            if (list.Count > 100)
            {
                list.RemoveRange(0, list.Count - 100);
            }
        }
    }

    public IEnumerable<PetSpawnItem> GetPendingPets(string? room)
    {
        var targetRoom = string.IsNullOrWhiteSpace(room) ? null : room.Trim().ToUpper();

        var result = _petsById.Values
            .Where(p => (p.Status == "pending" || p.Status == "flagged") 
                        && (targetRoom == null || p.Room == targetRoom))
            .OrderBy(p => p.CreatedAt)
            .ToList();

        return result;
    }

    public IEnumerable<PetSpawnItem> GetApprovedPets(string? room, long sinceUnixMs = 0)
    {
        var targetRoom = string.IsNullOrWhiteSpace(room) ? "DEFAULT" : room.Trim().ToUpper();
        var sinceTime = DateTimeOffset.FromUnixTimeMilliseconds(sinceUnixMs).UtcDateTime;

        lock (_lock)
        {
            if (!_approvedPerRoom.TryGetValue(targetRoom, out var list))
            {
                return Enumerable.Empty<PetSpawnItem>();
            }

            return list
                .Where(p => p.Status == "approved" && (p.ApprovedAt ?? p.CreatedAt) > sinceTime)
                .OrderBy(p => p.ApprovedAt ?? p.CreatedAt)
                .Take(40)
                .ToList();
        }
    }

    public IEnumerable<PetSpawnItem> GetActivePets(string? room, int limit = 30)
    {
        var targetRoom = string.IsNullOrWhiteSpace(room) ? "DEFAULT" : room.Trim().ToUpper();

        lock (_lock)
        {
            if (_approvedPerRoom.TryGetValue(targetRoom, out var list))
            {
                var active = list.Where(p => p.Status == "approved")
                    .OrderByDescending(p => p.ApprovedAt ?? p.CreatedAt)
                    .Take(limit)
                    .ToList();

                if (active.Count > 0) return active;
            }
        }

        // Fallback to database
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return db.PetSubmissions
                .Where(p => p.Room == targetRoom && p.Status == "approved")
                .OrderByDescending(p => p.ApprovedAt ?? p.CreatedAt)
                .Take(limit)
                .AsEnumerable()
                .Select(MapEntityToItem)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query active pets from database for room {Room}", targetRoom);
            return Enumerable.Empty<PetSpawnItem>();
        }
    }

    public IEnumerable<string> GetRemovedPetIds(string? room, long sinceUnixMs = 0)
    {
        var targetRoom = string.IsNullOrWhiteSpace(room) ? "DEFAULT" : room.Trim().ToUpper();
        var sinceTime = DateTimeOffset.FromUnixTimeMilliseconds(sinceUnixMs).UtcDateTime;

        lock (_lock)
        {
            if (_removedPerRoom.TryGetValue(targetRoom, out var list))
            {
                return list
                    .Where(r => r.RemovedAt > sinceTime)
                    .Select(r => r.Id)
                    .ToList();
            }
        }

        return Enumerable.Empty<string>();
    }

    public IEnumerable<PetSpawnItem> GetHistoryPets(string? room, int limit = 30)
    {
        var targetRoom = string.IsNullOrWhiteSpace(room) ? null : room.Trim().ToUpper();

        return _petsById.Values
            .Where(p => targetRoom == null || p.Room == targetRoom)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToList();
    }

    public PetSpawnItem? GetPetById(string id)
    {
        _petsById.TryGetValue(id, out var pet);
        return pet;
    }

    public bool ApprovePet(string id)
    {
        if (_petsById.TryGetValue(id, out var pet))
        {
            pet.Status = "approved";
            pet.ApprovedAt = DateTime.UtcNow;
            AddApprovedPet(pet.Room, pet);
            RecordClientApproval(pet.DeviceFingerprint, pet.ClientIp, pet.Room);
            _ = PersistPetAsync(pet);

            _logger.LogInformation("Pet {PetId} manually approved by admin. Cooldown applied to client.", id);
            return true;
        }

        // Fallback check database
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = db.PetSubmissions.FirstOrDefault(p => p.Id == id);
            if (entity != null)
            {
                entity.Status = "approved";
                entity.ApprovedAt = DateTime.UtcNow;
                db.SaveChanges();

                var item = MapEntityToItem(entity);
                _petsById[item.Id] = item;
                AddApprovedPet(item.Room, item);
                RecordClientApproval(item.DeviceFingerprint, item.ClientIp, item.Room);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve pet {PetId} in database", id);
        }

        return false;
    }

    public bool RejectPet(string id, string? reason = null)
    {
        if (_petsById.TryGetValue(id, out var pet))
        {
            pet.Status = "rejected";
            pet.AiReviewReason = !string.IsNullOrWhiteSpace(reason) 
                ? reason 
                : "Gambar belum memenuhi pedoman penayangan di videotron.";
            AddRejectedPet(pet.Room, pet);
            _ = PersistPetAsync(pet);

            _logger.LogInformation("Pet {PetId} manually rejected by admin. Reason: {Reason}", id, pet.AiReviewReason);
            return true;
        }

        // Fallback check database
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = db.PetSubmissions.FirstOrDefault(p => p.Id == id);
            if (entity != null)
            {
                entity.Status = "rejected";
                entity.AiReviewReason = reason ?? "Ditolak oleh admin.";
                db.SaveChanges();

                var item = MapEntityToItem(entity);
                _petsById[item.Id] = item;
                AddRejectedPet(item.Room, item);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject pet {PetId} in database", id);
        }

        return false;
    }

    public bool TakeDownPet(string id)
    {
        if (_petsById.TryGetValue(id, out var pet))
        {
            pet.Status = "takedown";
            pet.TakenDownAt = DateTime.UtcNow;

            lock (_lock)
            {
                if (_approvedPerRoom.TryGetValue(pet.Room, out var approvedList))
                {
                    approvedList.RemoveAll(p => p.Id == id);
                }

                var removedList = _removedPerRoom.GetOrAdd(pet.Room, _ => new List<(string Id, DateTime RemovedAt)>());
                removedList.Add((pet.Id, pet.TakenDownAt.Value));
                if (removedList.Count > 100)
                {
                    removedList.RemoveRange(0, removedList.Count - 100);
                }
            }

            _ = PersistPetAsync(pet);
            _ = BroadcastPetRemovedAsync(pet.Room, pet.Id);

            _logger.LogInformation("Pet {PetId} TAKEN DOWN by admin for room {Room}", id, pet.Room);
            return true;
        }

        // Fallback check database
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = db.PetSubmissions.FirstOrDefault(p => p.Id == id);
            if (entity != null)
            {
                entity.Status = "takedown";
                entity.TakenDownAt = DateTime.UtcNow;
                db.SaveChanges();

                lock (_lock)
                {
                    if (_approvedPerRoom.TryGetValue(entity.Room, out var approvedList))
                    {
                        approvedList.RemoveAll(p => p.Id == id);
                    }

                    var removedList = _removedPerRoom.GetOrAdd(entity.Room, _ => new List<(string Id, DateTime RemovedAt)>());
                    removedList.Add((entity.Id, entity.TakenDownAt.Value));
                }

                _ = BroadcastPetRemovedAsync(entity.Room, entity.Id);
                _logger.LogInformation("Pet {PetId} TAKEN DOWN from DB for room {Room}", id, entity.Room);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to takedown pet from DB for {PetId}", id);
        }

        return false;
    }

    private async Task BroadcastPetRemovedAsync(string room, string petId)
    {
        try
        {
            await _hubContext.Clients.Group($"room_{room}").SendAsync("PetRemoved", new { room, petId });
            await _hubContext.Clients.All.SendAsync("PetRemoved", new { room, petId });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast PetRemoved signal");
        }
    }

    public int ApproveAll(string? room)
    {
        var pending = GetPendingPets(room).ToList();
        int count = 0;
        foreach (var p in pending)
        {
            if (ApprovePet(p.Id)) count++;
        }
        return count;
    }

    public int RejectAll(string? room)
    {
        var pending = GetPendingPets(room).ToList();
        int count = 0;
        foreach (var p in pending)
        {
            if (RejectPet(p.Id, "Ditolak massal oleh operator.")) count++;
        }
        return count;
    }

    public (bool CanSubmit, int RemainingSeconds) CheckCooldown(string? fingerprint, string? clientIp, string? room)
    {
        int cooldownMinutes = _config.CooldownMinutes > 0 ? _config.CooldownMinutes : 15;
        var targetRoom = string.IsNullOrWhiteSpace(room) ? "DEFAULT" : room.Trim().ToUpper();

        // 1. Cek berdasarkan device fingerprint unik
        if (!string.IsNullOrWhiteSpace(fingerprint))
        {
            var key = $"fp_{fingerprint}_{targetRoom}";
            if (_lastApprovedClientTime.TryGetValue(key, out var lastTime))
            {
                var elapsed = DateTime.UtcNow - lastTime;
                var required = TimeSpan.FromMinutes(cooldownMinutes);
                if (elapsed < required)
                {
                    var rem = (int)(required - elapsed).TotalSeconds;
                    return (false, rem);
                }
            }
        }

        // 2. Cek berdasarkan client IP address jika bukan localhost
        if (!string.IsNullOrWhiteSpace(clientIp) && clientIp != "127.0.0.1" && clientIp != "::1")
        {
            var key = $"ip_{clientIp}_{targetRoom}";
            if (_lastApprovedClientTime.TryGetValue(key, out var lastTime))
            {
                var elapsed = DateTime.UtcNow - lastTime;
                var required = TimeSpan.FromMinutes(cooldownMinutes);
                if (elapsed < required)
                {
                    var rem = (int)(required - elapsed).TotalSeconds;
                    return (false, rem);
                }
            }
        }

        return (true, 0);
    }

    public void RecordClientApproval(string? fingerprint, string? clientIp, string? room)
    {
        var targetRoom = string.IsNullOrWhiteSpace(room) ? "DEFAULT" : room.Trim().ToUpper();
        var now = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(fingerprint))
        {
            _lastApprovedClientTime[$"fp_{fingerprint}_{targetRoom}"] = now;
        }

        if (!string.IsNullOrWhiteSpace(clientIp) && clientIp != "127.0.0.1" && clientIp != "::1")
        {
            _lastApprovedClientTime[$"ip_{clientIp}_{targetRoom}"] = now;
        }
    }

    public PetModerationStats GetStats(string? room)
    {
        var targetRoom = string.IsNullOrWhiteSpace(room) ? null : room.Trim().ToUpper();
        var all = _petsById.Values.Where(p => targetRoom == null || p.Room == targetRoom).ToList();

        return new PetModerationStats
        {
            Room = targetRoom ?? "ALL",
            CurrentMode = _config.Mode,
            IsAiConfigured = !string.IsNullOrWhiteSpace(_config.GeminiApiKey),
            PendingCount = all.Count(p => p.Status == "pending" || p.Status == "flagged"),
            ApprovedCount = all.Count(p => p.Status == "approved"),
            RejectedCount = all.Count(p => p.Status == "rejected"),
            TotalSubmissions = all.Count
        };
    }

    private async Task<string?> SaveDrawingImageAsync(string petId, string? base64Data)
    {
        if (string.IsNullOrWhiteSpace(base64Data)) return null;

        try
        {
            var commaIdx = base64Data.IndexOf(',');
            var cleanBase64 = commaIdx >= 0 ? base64Data[(commaIdx + 1)..] : base64Data;
            var bytes = Convert.FromBase64String(cleanBase64.Trim());

            if (bytes.Length > 10 * 1024 * 1024)
            {
                _logger.LogWarning("Pet image size exceeds 10MB limit: {Length} bytes", bytes.Length);
                return null;
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "pets");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileName = $"pet_{petId}.png";
            var filePath = Path.Combine(uploadsFolder, fileName);
            await File.WriteAllBytesAsync(filePath, bytes);

            return $"/uploads/pets/{fileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save pet drawing image for {PetId}", petId);
            return null;
        }
    }

    private async Task PersistPetAsync(PetSpawnItem pet)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var entity = await db.PetSubmissions.FirstOrDefaultAsync(p => p.Id == pet.Id);
            if (entity == null)
            {
                entity = new PetSubmission
                {
                    Id = pet.Id,
                    Room = pet.Room,
                    PlayerName = pet.PlayerName,
                    PetType = pet.PetType,
                    TemplateKey = pet.TemplateKey,
                    ColorHex = pet.ColorHex,
                    ImageUrl = pet.ImageUrl,
                    Status = pet.Status,
                    ModerationMode = pet.ModerationMode,
                    AiReviewReason = pet.AiReviewReason,
                    AiConfidence = pet.AiConfidence,
                    DeviceFingerprint = pet.DeviceFingerprint,
                    ClientIp = pet.ClientIp,
                    CreatedAt = pet.CreatedAt,
                    ApprovedAt = pet.ApprovedAt,
                    TakenDownAt = pet.TakenDownAt
                };
                db.PetSubmissions.Add(entity);
            }
            else
            {
                entity.Status = pet.Status;
                entity.ModerationMode = pet.ModerationMode;
                entity.AiReviewReason = pet.AiReviewReason;
                entity.AiConfidence = pet.AiConfidence;
                entity.ApprovedAt = pet.ApprovedAt;
                entity.TakenDownAt = pet.TakenDownAt;
                if (!string.IsNullOrEmpty(pet.ImageUrl)) entity.ImageUrl = pet.ImageUrl;
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist pet {PetId} to database", pet.Id);
        }
    }

    private static PetSpawnItem MapEntityToItem(PetSubmission p)
    {
        return new PetSpawnItem
        {
            Id = p.Id,
            Room = p.Room,
            PlayerName = p.PlayerName,
            PetType = p.PetType,
            TemplateKey = p.TemplateKey,
            ColorHex = p.ColorHex,
            ImageUrl = p.ImageUrl,
            ImageData = p.ImageUrl, // for canvas img.src compatibility
            Status = p.Status,
            ModerationMode = p.ModerationMode,
            AiReviewReason = p.AiReviewReason,
            AiConfidence = p.AiConfidence,
            DeviceFingerprint = p.DeviceFingerprint,
            ClientIp = p.ClientIp,
            CreatedAt = p.CreatedAt,
            ApprovedAt = p.ApprovedAt,
            TakenDownAt = p.TakenDownAt
        };
    }

    // =========================================================================
    // INSPEKSI GAMBAR DENGAN GEMINI AI VISION API
    // =========================================================================
    private async Task<(bool IsSafe, string Reason, double Confidence)> InspectDrawingWithGeminiAsync(string? imageData)
    {
        if (string.IsNullOrWhiteSpace(imageData))
        {
            return (true, "Tidak ada data gambar.", 1.0);
        }

        try
        {
            // Ambil raw Base64 data
            string base64 = imageData;
            string mimeType = "image/png";
            if (imageData.Contains(","))
            {
                var parts = imageData.Split(',');
                var header = parts[0];
                base64 = parts[1];
                if (header.Contains("image/jpeg")) mimeType = "image/jpeg";
                else if (header.Contains("image/webp")) mimeType = "image/webp";
            }

            var apiKey = _config.GeminiApiKey;
            var model = string.IsNullOrWhiteSpace(_config.GeminiModel) ? "gemini-1.5-flash" : _config.GeminiModel;
            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var prompt = 
                "You are an automated content moderation AI for an interactive public DOOH (Digital Out-Of-Home) billboard aquarium game in Indonesia.\n" +
                "Audience members draw pictures on their smartphone screens to have their fish/drawing swim on a giant public LED videotron in city centers.\n" +
                "Analyze this user drawing carefully.\n" +
                "CHECK STRICTLY FOR:\n" +
                "1. Genitalia, nudity, sexual shapes, breasts, penis doodles, or vulgar anatomical drawings.\n" +
                "2. Profanity, slurs, toxic text, swear words, sexual words, hate speech (Indonesian or English).\n" +
                "3. Weapons, blood, violence, gore, suicide/self-harm symbols.\n" +
                "4. SARA, swastikas, terrorist symbols, or hate group iconography.\n\n" +
                "Harmless sea animals, fishes, cute monsters, normal squiggles, innocent text (like someone's name or 'halo'), or abstract art ARE CONSIDERED SAFE.\n" +
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
            client.Timeout = TimeSpan.FromSeconds(8);

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(endpoint, jsonContent);

            if (!response.IsSuccessStatusCode)
            {
                var errStr = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Gemini API returned error status {StatusCode}: {Error}", response.StatusCode, errStr);
                return (false, $"Gagal menghubungi AI ({response.StatusCode}). Perlu ditinjau manual.", 0.5);
            }

            var respJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(respJson);
            var root = doc.RootElement;

            // Extract candidate text
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

            // Parse clean JSON result
            text = text.Trim();
            if (text.StartsWith("```json")) text = text[7..];
            if (text.StartsWith("```")) text = text[3..];
            if (text.EndsWith("```")) text = text[..^3];
            text = text.Trim();

            var parsed = JsonNode.Parse(text);
            var isSafe = parsed?["isSafe"]?.GetValue<bool>() ?? true;
            var reason = parsed?["reason"]?.GetValue<string>() ?? (isSafe ? "Gambar aman untuk tayang." : "Gambar mengandung konten tidak pantas.");
            var conf = parsed?["confidence"]?.GetValue<double>() ?? 0.9;

            return (isSafe, reason, conf);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while analyzing drawing with Gemini AI.");
            return (false, "Error analisis AI. Dialihkan ke antrean manual.", 0.5);
        }
    }
}
