namespace CmsBillboard.DynamicContent.Services;

public record VoteTokenPayload(
    string SurveyId,
    string SiteId,
    string Mode,
    long CreatedAtUnix,
    long ExpiresAtUnix
);

public interface ISecurityTokenService
{
    string GenerateVoteToken(string surveyId, string siteId, string mode, int? expiryMinutes = null);
    (bool IsValid, VoteTokenPayload? Payload, string? ErrorMessage) ValidateVoteToken(string token);
}
