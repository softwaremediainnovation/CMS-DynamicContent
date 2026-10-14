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

    public int TotalSurveys { get; set; }
    public int ActiveSurveys { get; set; }
    public int TotalVotes { get; set; }

    public List<SurveyMaster> Surveys { get; set; } = new();

    public async Task OnGetAsync()
    {
        AdminName = User.FindFirstValue(ClaimTypes.GivenName) ?? User.Identity?.Name ?? "Administrator";
        AdminRole = User.FindFirstValue(ClaimTypes.Role) ?? "Admin";

        Surveys = await _context.SurveyMasters
            .Include(s => s.Questions)
                .ThenInclude(q => q.Options)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        TotalSurveys = Surveys.Count;
        ActiveSurveys = Surveys.Count(s => s.IsActive);
        TotalVotes = await _context.SurveyVoteLogs.CountAsync();
    }
}
