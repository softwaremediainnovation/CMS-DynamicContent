using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CmsBillboard.DynamicContent.Services;

namespace CmsBillboard.DynamicContent.Pages.Admin;

[Authorize]
public class PhotoModerationModel : PageModel
{
    private readonly IPhotoModerationService _moderationService;

    public PhotoModerationModel(IPhotoModerationService moderationService)
    {
        _moderationService = moderationService;
    }

    public string AdminName { get; set; } = "Administrator";
    public string AdminRole { get; set; } = "Moderator";

    public PhotoModerationConfig Config { get; set; } = new();
    public PhotoModerationStats Stats { get; set; } = new();

    public void OnGet([FromQuery] string? site)
    {
        AdminName = User.FindFirstValue(ClaimTypes.GivenName) ?? User.Identity?.Name ?? "Administrator";
        AdminRole = User.FindFirstValue(ClaimTypes.Role) ?? "Moderator";

        Config = _moderationService.GetConfig();
        Stats = _moderationService.GetStats(site);
    }
}
