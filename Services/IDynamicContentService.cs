using CmsBillboard.DynamicContent.Models;

namespace CmsBillboard.DynamicContent.Services;

public interface IDynamicContentService
{
    Task<DynamicContentMaster?> GetDynamicContentMasterAsync(string id);
    Task<DynamicContentResultDto?> GetDynamicContentResultsAsync(string id, string? siteId = null, int? questionId = null);
    Task<DynamicContentVoteResponse> SubmitVoteAsync(DynamicContentVoteRequest request, string? clientIp);
    Task<bool> HasUserVotedAsync(string id, string deviceFingerprint, int? questionId = null);
    Task<DynamicContentWaitTimeResponse> CheckWaitTimeAsync(string id, string deviceFingerprint);
    string GenerateQrCodeSvg(string targetUrl);

    // Backward-compatibility aliases
    Task<DynamicContentMaster?> GetSurveyMasterAsync(string surveyId) => GetDynamicContentMasterAsync(surveyId);
    Task<DynamicContentResultDto?> GetSurveyResultsAsync(string surveyId, string? siteId = null, int? questionId = null) => GetDynamicContentResultsAsync(surveyId, siteId, questionId);
}

// Backward-compatibility interface alias
public interface ISurveyService : IDynamicContentService { }
