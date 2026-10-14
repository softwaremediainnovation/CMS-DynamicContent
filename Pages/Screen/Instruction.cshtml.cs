using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CmsBillboard.DynamicContent.Models;
using CmsBillboard.DynamicContent.Services;

namespace CmsBillboard.DynamicContent.Pages.Screen;

public class InstructionModel : PageModel
{
    private readonly IDynamicContentService _contentService;
    private readonly ISecurityTokenService _tokenService;

    public InstructionModel(IDynamicContentService contentService, ISecurityTokenService tokenService)
    {
        _contentService = contentService;
        _tokenService = tokenService;
    }

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = "samsung-zfold5";

    [BindProperty(SupportsGet = true)]
    public string Site { get; set; } = "Surabaya";

    [BindProperty(SupportsGet = true)]
    public string Mode { get; set; } = "onpremise"; // "onpremise" or "full"

    public DynamicContentResultDto? SurveyResult { get; set; }
    public string QrCodeSvg { get; set; } = string.Empty;
    public string VoteUrl { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        SurveyResult = await _contentService.GetDynamicContentResultsAsync(Id, Site);

        if (SurveyResult == null)
        {
            return NotFound($"Dynamic Content with ID '{Id}' was not found.");
        }

        // Generate tamper-proof encrypted token containing (Id, Site, Mode, Timestamp, Expiry)
        var token = _tokenService.GenerateVoteToken(Id, Site, Mode, expiryMinutes: 60);

        // Build mobile voting URL with encrypted token only (no plain parameters)
        var baseUrl = NetworkHelper.ResolveAccessibleBaseUrl(Request);
        VoteUrl = $"{baseUrl}/Vote?token={token}";

        // Generate QR code SVG
        QrCodeSvg = _contentService.GenerateQrCodeSvg(VoteUrl);

        return Page();
    }
}
