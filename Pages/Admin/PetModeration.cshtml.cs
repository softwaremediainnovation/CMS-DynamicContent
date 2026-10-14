using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CmsBillboard.DynamicContent.Services;

namespace CmsBillboard.DynamicContent.Pages.Admin;

[Authorize]
public class PetModerationModel : PageModel
{
    private readonly IPetModerationService _moderationService;

    public PetModerationModel(IPetModerationService moderationService)
    {
        _moderationService = moderationService;
    }

    public string AdminName { get; set; } = "Administrator";
    public string AdminRole { get; set; } = "Moderator";

    public PetModerationConfig Config { get; set; } = new();
    public PetModerationStats Stats { get; set; } = new();

    public void OnGet([FromQuery] string? room)
    {
        AdminName = User.FindFirstValue(ClaimTypes.GivenName) ?? User.Identity?.Name ?? "Administrator";
        AdminRole = User.FindFirstValue(ClaimTypes.Role) ?? "Moderator";

        Config = _moderationService.GetConfig();
        Stats = _moderationService.GetStats(room);
    }
}
