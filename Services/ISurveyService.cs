using CmsBillboard.DynamicContent.Models;

namespace CmsBillboard.DynamicContent.Services;

public interface ISurveyService
{
    Task<SurveyMaster?> GetSurveyMasterAsync(string surveyId);
    Task<SurveyResultDto?> GetSurveyResultsAsync(string surveyId, string? siteId = null, int? questionId = null);
    Task<SurveyVoteResponse> SubmitVoteAsync(SurveyVoteRequest request, string? clientIp);
    Task<bool> HasUserVotedAsync(string surveyId, string deviceFingerprint, int? questionId = null);
    string GenerateQrCodeSvg(string targetUrl);
}
