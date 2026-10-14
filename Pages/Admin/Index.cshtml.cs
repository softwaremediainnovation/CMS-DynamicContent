using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using CmsBillboard.DynamicContent.Data;
using CmsBillboard.DynamicContent.Models;

namespace CmsBillboard.DynamicContent.Pages.Admin;

[Authorize]
public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public string AdminName { get; set; } = "Administrator";
    public string AdminRole { get; set; } = "Admin";

    public int TotalDynamicContents { get; set; }
    public int ActiveDynamicContents { get; set; }
    public int TotalVotes { get; set; }

    public List<DynamicContentMaster> DynamicContents { get; set; } = new();

    // Backward-compatibility properties
    public int TotalSurveys => TotalDynamicContents;
    public int ActiveSurveys => ActiveDynamicContents;
    public List<DynamicContentMaster> Surveys => DynamicContents;

    public async Task OnGetAsync()
    {
        AdminName = User.FindFirstValue(ClaimTypes.GivenName) ?? User.Identity?.Name ?? "Administrator";
        AdminRole = User.FindFirstValue(ClaimTypes.Role) ?? "Admin";

        DynamicContents = await _context.DynamicContentMasters
            .Include(s => s.Questions)
                .ThenInclude(q => q.Options)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        TotalDynamicContents = DynamicContents.Count;
        ActiveDynamicContents = DynamicContents.Count(s => s.IsActive);
        TotalVotes = await _context.DynamicContentVoteLogs.CountAsync();
    }
}
