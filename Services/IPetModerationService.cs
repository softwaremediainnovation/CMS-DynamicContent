namespace CmsBillboard.DynamicContent.Services;

public class PetModerationConfig
{
    public string Mode { get; set; } = "manual"; // "manual" | "ai"
    public string? GeminiApiKey { get; set; } = string.Empty;
    public string GeminiModel { get; set; } = "gemini-1.5-flash";
    public bool AutoApproveTemplates { get; set; } = true;
    public bool AiAutoApproveSafeDrawings { get; set; } = true;
    public bool AiRejectUnsafeDrawings { get; set; } = false; // Jika false, unsafe akan di-flag ke antrean manual
    public int CooldownMinutes { get; set; } = 15; // Jeda waktu pengiriman ulang setelah disetujui (menit)
}

public class PetSpawnItem
{
    public string Id { get; set; } = string.Empty;
    public string Room { get; set; } = "DEFAULT";
    public string PlayerName { get; set; } = string.Empty;
    public string PetType { get; set; } = "template"; // "template" | "draw"
    public string TemplateKey { get; set; } = "nemo";
    public string? ImageData { get; set; } // Base64 data URL
    public string? ImageUrl { get; set; } // Web URL path, e.g. /uploads/pets/pet_xxx.png
    public string ColorHex { get; set; } = "#F97316";
    public string Status { get; set; } = "pending"; // "pending", "approved", "rejected", "flagged", "takedown"
    public string ModerationMode { get; set; } = "manual"; // "auto_template", "manual", "ai"
    public string? AiReviewReason { get; set; }
    public double? AiConfidence { get; set; }
    public string? DeviceFingerprint { get; set; }
    public string? ClientIp { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public DateTime? TakenDownAt { get; set; }
}

public class PetSpawnRequest
{
    public string? Room { get; set; } = "DEFAULT";
    public string? Token { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string? PetType { get; set; } = "template";
    public string? TemplateKey { get; set; } = "nemo";
    public string? ImageData { get; set; }
    public string ColorHex { get; set; } = "#F97316";
    public string? DeviceFingerprint { get; set; }
}

public class PetSubmissionResult
{
    public bool Success { get; set; }
    public string Status { get; set; } = "pending"; // "pending", "approved", "rejected", "flagged", "cooldown"
    public string Message { get; set; } = string.Empty;
    public PetSpawnItem? Pet { get; set; }
    public int RemainingCooldownSeconds { get; set; } = 0;
}

public class PetModerationStats
{
    public string Room { get; set; } = "ALL";
    public string CurrentMode { get; set; } = "manual";
    public bool IsAiConfigured { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int TotalSubmissions { get; set; }
}

public interface IPetModerationService
{
    PetModerationConfig GetConfig();
    void UpdateConfig(PetModerationConfig config);
    Task<PetSubmissionResult> SubmitPetAsync(PetSpawnRequest request, string? clientIp = null);
    IEnumerable<PetSpawnItem> GetPendingPets(string? room);
    IEnumerable<PetSpawnItem> GetApprovedPets(string? room, long sinceUnixMs = 0);
    IEnumerable<PetSpawnItem> GetHistoryPets(string? room, int limit = 30);
    PetSpawnItem? GetPetById(string id);
    bool ApprovePet(string id);
    bool RejectPet(string id, string? reason = null);
    bool TakeDownPet(string id);
    int ApproveAll(string? room);
    int RejectAll(string? room);
    PetModerationStats GetStats(string? room);
    (bool CanSubmit, int RemainingSeconds) CheckCooldown(string? fingerprint, string? clientIp, string? room);
    void RecordClientApproval(string? fingerprint, string? clientIp, string? room);
    IEnumerable<PetSpawnItem> GetActivePets(string? room, int limit = 30);
    IEnumerable<string> GetRemovedPetIds(string? room, long sinceUnixMs = 0);
    Task InitializeFromDatabaseAsync();
}
