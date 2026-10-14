using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CmsBillboard.DynamicContent.Models;
using CmsBillboard.DynamicContent.Services;

namespace CmsBillboard.DynamicContent.Pages.Vote;

public class IndexModel : PageModel
{
    private readonly IDynamicContentService _contentService;
    private readonly ISecurityTokenService _tokenService;

    public IndexModel(IDynamicContentService contentService, ISecurityTokenService tokenService)
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
    public string Mode { get; set; } = "onpremise"; // "onpremise" or "full"

    public bool IsTokenValid { get; set; } = true;
    public string? TokenErrorMessage { get; set; }

    public DynamicContentMaster? DynamicContent { get; set; }
    public DynamicContentMaster? Survey => DynamicContent;
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
            Id = payload.ContentId;
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

        DynamicContent = await _contentService.GetDynamicContentMasterAsync(Id);

        if (DynamicContent == null)
        {
            IsTokenValid = false;
            TokenErrorMessage = $"Konten '{Id}' tidak ditemukan atau sudah tidak aktif.";
            return Page();
        }

        Question = DynamicContent.Questions.OrderBy(q => q.SortOrder).FirstOrDefault();

        return Page();
    }
}
