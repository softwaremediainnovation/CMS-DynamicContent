using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CmsBillboard.DynamicContent.Services;

public class PetModerationService : IPetModerationService
{
    private readonly ILogger<PetModerationService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecurityTokenService _tokenService;
    private readonly PetModerationConfig _config;

    // Master storage pet by Id
    private readonly ConcurrentDictionary<string, PetSpawnItem> _petsById = new();

    // Pet lists per room (room -> list of IDs in order)
    private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _pendingPerRoom = new();
    private readonly ConcurrentDictionary<string, List<PetSpawnItem>> _approvedPerRoom = new();
    private readonly ConcurrentDictionary<string, List<PetSpawnItem>> _rejectedPerRoom = new();
    private readonly object _lock = new();

    public PetModerationService(
        ILogger<PetModerationService> logger,
        IHttpClientFactory httpClientFactory,
        ISecurityTokenService tokenService,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _tokenService = tokenService;

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
            AiRejectUnsafeDrawings = false
        };

        _logger.LogInformation("PetModerationService initialized. Default Mode: {Mode}, AI Key set: {HasKey}", 
            _config.Mode, !string.IsNullOrWhiteSpace(_config.GeminiApiKey));
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
                AiRejectUnsafeDrawings = _config.AiRejectUnsafeDrawings
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
        }

        _logger.LogInformation("PetModerationService config updated. Mode: {Mode}", _config.Mode);
    }

    public async Task<PetSubmissionResult> SubmitPetAsync(PetSpawnRequest request)
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

        var pet = new PetSpawnItem
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Room = room,
            PlayerName = request.PlayerName.Trim().Length > 20 ? request.PlayerName.Trim()[..20] : request.PlayerName.Trim(),
            PetType = string.IsNullOrWhiteSpace(request.PetType) ? "template" : request.PetType.ToLowerInvariant(),
            TemplateKey = request.TemplateKey ?? "nemo",
            ImageData = request.ImageData,
            ColorHex = request.ColorHex ?? "#F97316",
            CreatedAt = DateTime.UtcNow
        };

        // 1. Opsi A: Template karakter bawaan langsung Auto-Approve
        if (pet.PetType == "template" && _config.AutoApproveTemplates)
        {
            pet.Status = "approved";
            pet.ModerationMode = "auto_template";
            pet.ApprovedAt = DateTime.UtcNow;
            pet.AiReviewReason = "Template karakter standar langsung disetujui.";

            _petsById[pet.Id] = pet;
            AddApprovedPet(room, pet);

            return new PetSubmissionResult
            {
                Success = true,
                Status = "approved",
                Message = "Pet karakter berhasil dicemplungkan ke akuarium!",
                Pet = pet
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

                _logger.LogInformation("Pet drawing {PetId} Auto-Approved by AI. Reason: {Reason}", pet.Id, aiResult.Reason);

                return new PetSubmissionResult
                {
                    Success = true,
                    Status = "approved",
                    Message = "Gambar kamu lolos verifikasi AI dan langsung berenang di videotron!",
                    Pet = pet
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

                    _logger.LogWarning("Pet drawing {PetId} Auto-Rejected by AI. Reason: {Reason}", pet.Id, aiResult.Reason);

                    return new PetSubmissionResult
                    {
                        Success = false,
                        Status = "rejected",
                        Message = $"Gambar tidak dapat ditampilkan: {aiResult.Reason}",
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
                .Where(p => (p.ApprovedAt ?? p.CreatedAt) > sinceTime)
                .OrderBy(p => p.ApprovedAt ?? p.CreatedAt)
                .Take(40)
                .ToList();
        }
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
            _logger.LogInformation("Pet {PetId} manually approved by admin.", id);
            return true;
        }
        return false;
    }

    public bool RejectPet(string id, string? reason = null)
    {
        if (_petsById.TryGetValue(id, out var pet))
        {
            pet.Status = "rejected";
            if (!string.IsNullOrWhiteSpace(reason))
            {
                pet.AiReviewReason = reason;
            }
            AddRejectedPet(pet.Room, pet);
            _logger.LogInformation("Pet {PetId} manually rejected by admin. Reason: {Reason}", id, reason ?? "N/A");
            return true;
        }
        return false;
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
