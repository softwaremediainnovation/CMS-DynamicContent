namespace CmsBillboard.DynamicContent.Services;

public class ModerationCheckResult
{
    public bool IsApproved { get; set; } = true;
    public string? Reason { get; set; }
    public string? Category { get; set; } // "SARA", "PROFANITY", "SPAM_PROMO"
    public string? MatchedTerm { get; set; }
    public string SanitizedText { get; set; } = string.Empty;
}

public interface IContentModerationService
{
    ModerationCheckResult CheckComment(string? comment);
}
