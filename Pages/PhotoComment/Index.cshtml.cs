using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CmsBillboard.DynamicContent.Models;
using CmsBillboard.DynamicContent.Services;

namespace CmsBillboard.DynamicContent.Pages.PhotoComment;

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
    public string Id { get; set; } = "tolak-angin-live";

    [BindProperty(SupportsGet = true)]
    public string Site { get; set; } = "Gedung Sate Bandung";

    public bool IsTokenValid { get; set; } = true;
    public string? TokenErrorMessage { get; set; }
    public DynamicContentMaster? DynamicContent { get; set; }
    public PhotoCommentSlotConfig SlotConfig { get; set; } = new();

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

            Id = payload.ContentId;
            Site = payload.SiteId;
            IsTokenValid = true;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                Id = "tolak-angin-live";
            }
            if (string.IsNullOrWhiteSpace(Site))
            {
                Site = "Gedung Sate Bandung";
            }

            // Direct access: generate a valid signed token for API requests
            Token = _tokenService.GenerateVoteToken(Id, Site, "onpremise");
            IsTokenValid = true;
        }

        if (string.IsNullOrWhiteSpace(Id))
        {
            Id = "tolak-angin-live";
        }

        DynamicContent = await _contentService.GetDynamicContentMasterAsync(Id);

        if (DynamicContent == null)
        {
            IsTokenValid = false;
            TokenErrorMessage = $"Kampanye Live Photo '{Id}' tidak ditemukan atau sudah tidak aktif.";
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(DynamicContent.SlotConfigJson))
        {
            try
            {
                SlotConfig = System.Text.Json.JsonSerializer.Deserialize<PhotoCommentSlotConfig>(
                    DynamicContent.SlotConfigJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            catch
            {
                SlotConfig = new PhotoCommentSlotConfig();
            }
        }

        return Page();
    }
}
