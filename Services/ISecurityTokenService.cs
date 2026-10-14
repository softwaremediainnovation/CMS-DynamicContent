namespace CmsBillboard.DynamicContent.Services;

public class VoteTokenPayload
{
    public string ContentId { get; set; } = string.Empty;

    // Backward-compatibility alias
    public string SurveyId
    {
        get => ContentId;
        set => ContentId = value;
    }

    public string SiteId { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public long CreatedAtUnix { get; set; }
    public long ExpiresAtUnix { get; set; }

    public VoteTokenPayload() { }

    public VoteTokenPayload(string contentId, string siteId, string mode, long createdAtUnix, long expiresAtUnix)
    {
        ContentId = contentId;
        SiteId = siteId;
        Mode = mode;
        CreatedAtUnix = createdAtUnix;
        ExpiresAtUnix = expiresAtUnix;
    }
}

public interface ISecurityTokenService
{
    string GenerateVoteToken(string contentId, string siteId, string mode, int? expiryMinutes = null);
    (bool IsValid, VoteTokenPayload? Payload, string? ErrorMessage) ValidateVoteToken(string token);
}
