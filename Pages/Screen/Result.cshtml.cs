using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CmsBillboard.DynamicContent.Models;
using CmsBillboard.DynamicContent.Services;

namespace CmsBillboard.DynamicContent.Pages.Screen;

public class ResultModel : PageModel
{
    private readonly ISurveyService _surveyService;

    public ResultModel(ISurveyService surveyService)
    {
        _surveyService = surveyService;
    }

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = "samsung-zfold5";

    [BindProperty(SupportsGet = true)]
    public string Site { get; set; } = "Surabaya";

    public SurveyResultDto? SurveyResult { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        SurveyResult = await _surveyService.GetSurveyResultsAsync(Id, Site);

        if (SurveyResult == null)
        {
            return NotFound($"Survey with ID '{Id}' was not found.");
        }

        return Page();
    }
}
