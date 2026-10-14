using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CmsBillboard.DynamicContent.Models;
using CmsBillboard.DynamicContent.Services;

namespace CmsBillboard.DynamicContent.Pages.Vote;

public class IndexModel : PageModel
{
    private readonly ISurveyService _surveyService;

    public IndexModel(ISurveyService surveyService)
    {
        _surveyService = surveyService;
    }

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = "samsung-zfold5";

    [BindProperty(SupportsGet = true)]
    public string Site { get; set; } = "Surabaya";

    [BindProperty(SupportsGet = true)]
    public string Mode { get; set; } = "onpremise"; // "onpremise" or "full"

    public SurveyItem? Survey { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Survey = await _surveyService.GetSurveyAsync(Id);

        if (Survey == null)
        {
            return NotFound($"Survey '{Id}' tidak ditemukan.");
        }

        return Page();
    }
}
