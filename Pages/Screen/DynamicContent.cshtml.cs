using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CmsBillboard.DynamicContent.Models;
using CmsBillboard.DynamicContent.Services;

namespace CmsBillboard.DynamicContent.Pages.Screen;

public class DynamicContentModel : PageModel
{
    private readonly IDynamicContentService _contentService;
    private readonly ISecurityTokenService _tokenService;

    public DynamicContentModel(IDynamicContentService contentService, ISecurityTokenService tokenService)
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

    [BindProperty(SupportsGet = true)]
    public string Mode { get; set; } = "onpremise"; // onpremise or full

    public bool IsTokenValid { get; set; } = true;
    public string? TokenErrorMessage { get; set; }

    public DynamicContentResultDto? DynamicContentResult { get; set; }
    public DynamicContentResultDto? SurveyResult => DynamicContentResult;
    public string QrCodeSvg { get; set; } = string.Empty;
    public string VoteUrl { get; set; } = string.Empty;

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
            if (!string.IsNullOrWhiteSpace(payload.Mode))
            {
                Mode = payload.Mode;
            }
        }

        DynamicContentResult = await _contentService.GetDynamicContentResultsAsync(Id, Site);

        if (DynamicContentResult == null)
        {
            return NotFound($"Dynamic Content with ID '{Id}' was not found.");
        }

        // Build mobile voting URL with tamper-proof encrypted token
        var baseUrl = NetworkHelper.ResolveAccessibleBaseUrl(Request);
        var mobileToken = _tokenService.GenerateVoteToken(Id, Site, Mode, expiryMinutes: 120);
        VoteUrl = $"{baseUrl}/Vote?token={mobileToken}";

        // Generate QR code SVG (fallback)
        QrCodeSvg = _contentService.GenerateQrCodeSvg(VoteUrl);

        return Page();
    }
}
