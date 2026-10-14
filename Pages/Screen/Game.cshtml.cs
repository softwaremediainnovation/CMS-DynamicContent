using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CmsBillboard.DynamicContent.Models;
using CmsBillboard.DynamicContent.Services;

namespace CmsBillboard.DynamicContent.Pages.Screen;

public class GameModel : PageModel
{
    private readonly ISurveyService _surveyService;
    private readonly IWebHostEnvironment _env;

    public GameModel(ISurveyService surveyService, IWebHostEnvironment env)
    {
        _surveyService = surveyService;
        _env = env;
    }

    [BindProperty(SupportsGet = true)]
    public string? Id { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Game { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Site { get; set; } = "Surabaya";

    [BindProperty(SupportsGet = true)]
    public string Mode { get; set; } = "interactive"; // "interactive" or "auto"

    public string CampaignTitle { get; set; } = "Interactive DOOH Game";
    public string ClientName { get; set; } = "Warna Warni Advertising";
    public string? ThemeColorHex { get; set; } = "#0072FF";
    public string? BrandLogoUrl { get; set; }
    public string GameIframeSrc { get; set; } = string.Empty;
    public bool GameFound { get; set; } = true;
    public string ErrorMessage { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        string targetFolder = string.Empty;
        string entryFile = "index.html";
        string customParams = string.Empty;

        // 1. Coba cari di database jika ada ID
        if (!string.IsNullOrEmpty(Id))
        {
            var master = await _surveyService.GetSurveyMasterAsync(Id);
            if (master != null)
            {
                CampaignTitle = master.CampaignTitle;
                ClientName = master.ClientName;
                ThemeColorHex = master.ThemeColorHex;
                BrandLogoUrl = master.BrandLogoUrl;
                targetFolder = !string.IsNullOrEmpty(master.GameFolder) ? master.GameFolder : master.Id;
                entryFile = !string.IsNullOrEmpty(master.GameEntryFile) ? master.GameEntryFile : "index.html";
                customParams = master.GameCustomParams ?? string.Empty;
            }
        }

        // 2. Jika tidak ada di database, gunakan parameter ?game= atau fallback dari ?id=
        if (string.IsNullOrEmpty(targetFolder))
        {
            targetFolder = !string.IsNullOrEmpty(Game) ? Game : (Id ?? "rock-paper-scissors");
        }

        // 3. Verifikasi ketersediaan folder fisik game di wwwroot/games/
        var gamesRootPath = Path.Combine(_env.WebRootPath, "games");
        var gameDirPath = Path.Combine(gamesRootPath, targetFolder);

        if (!Directory.Exists(gameDirPath))
        {
            GameFound = false;
            ErrorMessage = $"Direktori game '{targetFolder}' tidak ditemukan di wwwroot/games/. Pastikan folder game sudah diletakkan di sana.";
            return Page();
        }

        // 4. Susun URL Iframe Game dengan parameter dinamis
        var queryParams = new List<string>
        {
            $"site={Uri.EscapeDataString(Site)}",
            $"title={Uri.EscapeDataString(CampaignTitle)}",
            $"brand={Uri.EscapeDataString(ClientName)}",
            $"mode={Uri.EscapeDataString(Mode)}"
        };

        if (!string.IsNullOrEmpty(customParams))
        {
            queryParams.Add(customParams.TrimStart('&', '?'));
        }

        GameIframeSrc = $"/games/{targetFolder}/{entryFile}?{string.Join("&", queryParams)}";

        return Page();
    }
}
