using CmsBillboard.DynamicContent.Models;

namespace CmsBillboard.DynamicContent.Services;

public interface ISurveyService
{
    Task<SurveyItem?> GetSurveyAsync(string surveyId);
    Task<SurveyResultDto?> GetSurveyResultsAsync(string surveyId, string? siteId = null);
    Task<SurveyVoteResponse> SubmitVoteAsync(SurveyVoteRequest request, string? clientIp);
    Task<bool> HasUserVotedAsync(string surveyId, string deviceFingerprint);
    string GenerateQrCodeSvg(string targetUrl);
}
