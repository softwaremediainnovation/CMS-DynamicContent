using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CmsBillboard.DynamicContent.Models;
using CmsBillboard.DynamicContent.Services;

namespace CmsBillboard.DynamicContent.Pages.Vote;

public class IndexModel : PageModel
{
    private readonly ISurveyService _surveyService;
    private readonly ISecurityTokenService _tokenService;

    public IndexModel(ISurveyService surveyService, ISecurityTokenService tokenService)
    {
        _surveyService = surveyService;
        _tokenService = tokenService;
    }

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = "samsung-zfold5";

    [BindProperty(SupportsGet = true)]
    public string Site { get; set; } = "Surabaya";

    [BindProperty(SupportsGet = true)]
    public string Mode { get; set; } = "onpremise"; // "onpremise" or "full"

    public bool IsTokenValid { get; set; } = true;
    public string? TokenErrorMessage { get; set; }

    public DynamicContentMaster? Survey { get; set; }
    public DynamicContentQuestion? Question { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        // 1. If Token is provided, decode and strictly validate signature & expiry
        if (!string.IsNullOrWhiteSpace(Token))
        {
            var (isValid, payload, errorMessage) = _tokenService.ValidateVoteToken(Token);
            if (!isValid || payload == null)
            {
                IsTokenValid = false;
                TokenErrorMessage = errorMessage ?? "Token tidak valid atau telah dimodifikasi!";
                return Page();
            }

            // Extract securely from decrypted payload (cannot be forged)
            Id = payload.SurveyId;
            Site = payload.SiteId;
            Mode = payload.Mode;
            IsTokenValid = true;
        }
        else
        {
            // Direct developer access without token: create signed token on the fly
            Token = _tokenService.GenerateVoteToken(Id, Site, Mode);
            IsTokenValid = true;
        }

        Survey = await _surveyService.GetSurveyMasterAsync(Id);

        if (Survey == null)
        {
            IsTokenValid = false;
            TokenErrorMessage = $"Survey/Content '{Id}' tidak ditemukan atau sudah tidak aktif.";
            return Page();
        }

        Question = Survey.Questions.OrderBy(q => q.SortOrder).FirstOrDefault();

        return Page();
    }
}
