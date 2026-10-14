using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CmsBillboard.DynamicContent.Models;
using CmsBillboard.DynamicContent.Services;

namespace CmsBillboard.DynamicContent.Pages.Screen;

public class SurveyModel : PageModel
{
    private readonly ISurveyService _surveyService;

    public SurveyModel(ISurveyService surveyService)
    {
        _surveyService = surveyService;
    }

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = "samsung-zfold5";

    [BindProperty(SupportsGet = true)]
    public string Site { get; set; } = "Surabaya";

    [BindProperty(SupportsGet = true)]
    public string Mode { get; set; } = "onpremise"; // onpremise or full

    public SurveyResultDto? SurveyResult { get; set; }
    public string QrCodeSvg { get; set; } = string.Empty;
    public string VoteUrl { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        SurveyResult = await _surveyService.GetSurveyResultsAsync(Id, Site);

        if (SurveyResult == null)
        {
            return NotFound($"Survey with ID '{Id}' was not found.");
        }

        // Build mobile voting URL for QR code
        var host = Request.Host.Value;
        var scheme = Request.Scheme;
        VoteUrl = $"{scheme}://{host}/Vote?id={Id}&site={Site}&mode={Mode}";

        // Generate QR code SVG
        QrCodeSvg = _surveyService.GenerateQrCodeSvg(VoteUrl);

        return Page();
    }
}
