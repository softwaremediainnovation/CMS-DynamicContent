using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CmsBillboard.DynamicContent.Models;
using CmsBillboard.DynamicContent.Services;

namespace CmsBillboard.DynamicContent.Pages.Screen;

public class ResultModel : PageModel
{
    private readonly IDynamicContentService _contentService;
    private readonly ISecurityTokenService _tokenService;

    public ResultModel(IDynamicContentService contentService, ISecurityTokenService tokenService)
    {
        _contentService = contentService;
        _tokenService = tokenService;
    }

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = "samsung-zfold5";

    [BindProperty(SupportsGet = true)]
    public string Site { get; set; } = "Surabaya";

    public bool IsTokenValid { get; set; } = true;
    public string? TokenErrorMessage { get; set; }

    public DynamicContentResultDto? SurveyResult { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        // 1. If Token is provided, decode and strictly validate signature & expiry
        if (!string.IsNullOrWhiteSpace(Token))
        {
            var (isValid, payload, errorMessage) = _tokenService.ValidateVoteToken(Token);
            if (!isValid || payload == null)
            {
                IsTokenValid = false;
                TokenErrorMessage = errorMessage ?? "Token layar tidak valid atau telah dimodifikasi!";
                return Page();
            }

            Id = payload.ContentId;
            Site = payload.SiteId;
        }

        SurveyResult = await _contentService.GetDynamicContentResultsAsync(Id, Site);

        if (SurveyResult == null)
        {
            return NotFound($"Dynamic Content with ID '{Id}' was not found.");
        }

        return Page();
    }
}
