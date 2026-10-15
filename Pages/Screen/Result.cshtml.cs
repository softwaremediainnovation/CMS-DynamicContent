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

    public DynamicContentResultDto? SurveyResult { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!string.IsNullOrWhiteSpace(Token))
        {
            var (isValid, payload, _) = _tokenService.ValidateVoteToken(Token);
            if (isValid && payload != null)
            {
                Id = payload.ContentId;
                Site = payload.SiteId;
            }
        }

        SurveyResult = await _contentService.GetDynamicContentResultsAsync(Id, Site);

        if (SurveyResult == null)
        {
            return NotFound($"Dynamic Content with ID '{Id}' was not found.");
        }

        return Page();
    }
}
