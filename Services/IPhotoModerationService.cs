namespace CmsBillboard.DynamicContent.Services;

public class PhotoModerationConfig
{
    public string Mode { get; set; } = "manual"; // "manual" | "ai"
    public string? GeminiApiKey { get; set; } = string.Empty;
    public string GeminiModel { get; set; } = "gemini-1.5-flash";
    public bool AiAutoApproveSafePhotos { get; set; } = true;
    public bool AiRejectUnsafePhotos { get; set; } = false; // Jika false, unsafe akan di-flag ke antrean manual
    public int CooldownMinutes { get; set; } = 15;
}

public class PhotoModerationItem
{
    public int Id { get; set; }
    public string DynamicContentMasterId { get; set; } = string.Empty;
    public string UserName { get; set; } = "Anonim";
    public string? PhoneNumber { get; set; }
    public string CommentText { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;
    public string SiteId { get; set; } = "Default";
    public string City { get; set; } = "Nasional";
    public string Status { get; set; } = "pending"; // "pending", "approved", "rejected", "flagged", "takedown"
    public string ModerationMode { get; set; } = "manual"; // "manual", "ai", "auto"
    public string? AiReviewReason { get; set; }
    public double? AiConfidence { get; set; }
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTime VotedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public DateTime? TakenDownAt { get; set; }
    public string TimeAgo { get; set; } = "Baru saja";
}

public class PhotoSubmissionResult
{
    public bool Success { get; set; }
    public string Status { get; set; } = "pending"; // "pending", "approved", "rejected", "flagged"
    public string Message { get; set; } = string.Empty;
    public PhotoModerationItem? Item { get; set; }
    public int RemainingCooldownSeconds { get; set; } = 0;
}

public class PhotoModerationStats
{
    public string SiteId { get; set; } = "ALL";
    public string CurrentMode { get; set; } = "manual";
    public bool IsAiConfigured { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int TotalSubmissions { get; set; }
}

public interface IPhotoModerationService
{
    PhotoModerationConfig GetConfig();
    void UpdateConfig(PhotoModerationConfig config);
    Task<PhotoSubmissionResult> ProcessSubmissionAsync(Models.DynamicContentVoteLog voteLog, string? rawBase64 = null);
    IEnumerable<PhotoModerationItem> GetPendingPhotos(string? siteId = null, string? contentId = null);
    IEnumerable<PhotoModerationItem> GetApprovedPhotos(string? siteId = null, string? contentId = null, int limit = 30);
    IEnumerable<PhotoModerationItem> GetHistoryPhotos(string? siteId = null, string? contentId = null, int limit = 50);
    PhotoModerationItem? GetPhotoById(int id);
    Task<bool> ApprovePhotoAsync(int id);
    Task<bool> RejectPhotoAsync(int id, string? reason = null);
    Task<bool> TakeDownPhotoAsync(int id);
    Task<int> ApproveAllAsync(string? siteId = null, string? contentId = null);
    Task<int> RejectAllAsync(string? siteId = null, string? contentId = null);
    PhotoModerationStats GetStats(string? siteId = null, string? contentId = null);
    Task InitializeFromDatabaseAsync();
}
