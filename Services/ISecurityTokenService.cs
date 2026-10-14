namespace CmsBillboard.DynamicContent.Services;

public class VoteTokenPayload
{
    public string ContentId { get; set; } = string.Empty;

    // Backward-compatibility alias
    [System.Text.Json.Serialization.JsonIgnore]
    public string SurveyId
    {
        get => ContentId;
        set => ContentId = value;
    }

    public string SiteId { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public long CreatedAtUnix { get; set; }
    public long ExpiresAtUnix { get; set; }

    public Dictionary<string, object>? CustomParams { get; set; }

    public VoteTokenPayload() { }

    public VoteTokenPayload(string contentId, string siteId, string mode, long createdAtUnix, long expiresAtUnix, Dictionary<string, object>? customParams = null)
    {
        ContentId = contentId;
        SiteId = siteId;
        Mode = mode;
        CreatedAtUnix = createdAtUnix;
        ExpiresAtUnix = expiresAtUnix;
        CustomParams = customParams;
    }
}

public interface ISecurityTokenService
{
    string GenerateVoteToken(string contentId, string siteId, string mode, int? expiryMinutes = null);
    string GenerateToken(string contentId, string siteId, string mode, Dictionary<string, object>? customParams = null, int? expiryMinutes = null);
    (bool IsValid, VoteTokenPayload? Payload, string? ErrorMessage) ValidateVoteToken(string token);
}
