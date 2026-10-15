using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using CmsBillboard.DynamicContent.Data;
using CmsBillboard.DynamicContent.Models;

namespace CmsBillboard.DynamicContent.Pages.Admin.DynamicContents;

[Authorize]
public class ReportModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly CmsBillboard.DynamicContent.Services.ISecurityTokenService _tokenService;

    public ReportModel(AppDbContext context, CmsBillboard.DynamicContent.Services.ISecurityTokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? City { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SiteId { get; set; }

    public DynamicContentMaster? Survey { get; set; }
    public DynamicContentQuestion? Question { get; set; }

    // Metrics
    public int TotalVotes { get; set; }
    public double AvgResponseTime { get; set; }
    public string WinningOptionKey { get; set; } = "-";
    public string WinningOptionText { get; set; } = "-";
    public double WinningOptionPct { get; set; }
    public string TopPerformingSite { get; set; } = "-";

    // BrandIQ Quiz Metrics
    public bool IsQuizMode { get; set; }
    public int CorrectVotesCount { get; set; }
    public int IncorrectVotesCount { get; set; }
    public double AccuracyRatePct { get; set; }
    public string? CorrectAnswerKey { get; set; }
    public string? CorrectAnswerText { get; set; }

    // Vote distribution by option
    public List<OptionStatDto> OptionStats { get; set; } = new();

    // Breakdown per City
    public List<CityStatDto> CityStats { get; set; } = new();

    // Breakdown per Titik LED (Site)
    public List<SiteStatDto> SiteStats { get; set; } = new();

    // Device & Audience Intelligence
    public List<BrandStatDto> DeviceBrandStats { get; set; } = new();
    public List<OsStatDto> OsStats { get; set; } = new();
    public List<BrowserStatDto> BrowserStats { get; set; } = new();

    // Available filters
    public List<string> AvailableCities { get; set; } = new();
    public List<string> AvailableSites { get; set; } = new();

    // Recent activity log
    public List<DynamicContentVoteLog> RecentLogs { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        // Decode token if provided
        if (!string.IsNullOrWhiteSpace(Token))
        {
            var (isValid, payload, _) = _tokenService.ValidateVoteToken(Token);
            if (isValid && payload != null)
            {
                Id = payload.ContentId;
                if (!string.IsNullOrWhiteSpace(payload.SiteId))
                {
                    SiteId = payload.SiteId;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(Id))
        {
            return RedirectToPage("/Admin/Index");
        }

        Survey = await _context.DynamicContentMasters
            .Include(m => m.Questions.OrderBy(q => q.SortOrder))
                .ThenInclude(q => q.Options.OrderBy(o => o.SortOrder))
            .FirstOrDefaultAsync(m => m.Id == Id);

        if (Survey == null)
        {
            return NotFound($"Konten dengan ID '{Id}' tidak ditemukan.");
        }

        Question = Survey.Questions.FirstOrDefault();
        if (Question == null)
        {
            return Page();
        }

        // Available filter choices from actual logs
        AvailableCities = await _context.DynamicContentVoteLogs
            .Where(v => v.DynamicContentMasterId == Id && !string.IsNullOrWhiteSpace(v.City))
            .Select(v => v.City!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        AvailableSites = await _context.DynamicContentVoteLogs
            .Where(v => v.DynamicContentMasterId == Id && !string.IsNullOrWhiteSpace(v.SiteId))
            .Select(v => v.SiteId)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync();

        // Base Query with Filters
        var query = _context.DynamicContentVoteLogs.Where(v => v.DynamicContentMasterId == Id);

        if (!string.IsNullOrWhiteSpace(City) && City != "All")
        {
            query = query.Where(v => v.City == City);
        }

        if (!string.IsNullOrWhiteSpace(SiteId) && SiteId != "All")
        {
            query = query.Where(v => v.SiteId == SiteId);
        }

        var allFilteredLogs = await query.ToListAsync();
        TotalVotes = allFilteredLogs.Count;

        IsQuizMode = string.Equals(Survey.ContentType, "BrandIQ", StringComparison.OrdinalIgnoreCase);
        var correctOpt = Question.Options.FirstOrDefault(o => o.IsCorrect || o.OptionKey.Equals(Question.CorrectOptionKey, StringComparison.OrdinalIgnoreCase));
        CorrectAnswerKey = correctOpt?.OptionKey ?? Question.CorrectOptionKey;
        CorrectAnswerText = correctOpt?.OptionText;

        // If no logs yet, calculate based on seeded options for initial demo
        if (TotalVotes == 0)
        {
            var seedTotal = Question.Options.Sum(o => o.VoteCount);
            TotalVotes = seedTotal;
            OptionStats = Question.Options.Select(o => new OptionStatDto
            {
                Key = o.OptionKey,
                Text = o.OptionText,
                Color = o.ColorHex,
                IsCorrect = IsQuizMode && (o.IsCorrect || o.OptionKey.Equals(CorrectAnswerKey, StringComparison.OrdinalIgnoreCase)),
                Count = o.VoteCount,
                Percentage = seedTotal > 0 ? Math.Round((double)o.VoteCount / seedTotal * 100, 1) : 0
            }).ToList();

            var top = OptionStats.OrderByDescending(o => o.Count).FirstOrDefault();
            if (top != null)
            {
                WinningOptionKey = top.Key;
                WinningOptionText = top.Text;
                WinningOptionPct = top.Percentage;
            }

            if (IsQuizMode && TotalVotes > 0)
            {
                var correctOptStat = OptionStats.FirstOrDefault(o => o.IsCorrect);
                CorrectVotesCount = correctOptStat?.Count ?? 0;
                IncorrectVotesCount = TotalVotes - CorrectVotesCount;
                AccuracyRatePct = Math.Round((double)CorrectVotesCount / TotalVotes * 100, 1);
            }

            return Page();
        }

        // 1. Option Stats
        var optionCounts = allFilteredLogs
            .GroupBy(v => v.OptionKey)
            .ToDictionary(g => g.Key, g => g.Count());

        OptionStats = Question.Options.Select(o =>
        {
            int count = optionCounts.ContainsKey(o.OptionKey) ? optionCounts[o.OptionKey] : 0;
            return new OptionStatDto
            {
                Key = o.OptionKey,
                Text = o.OptionText,
                Color = o.ColorHex,
                IsCorrect = IsQuizMode && (o.IsCorrect || o.OptionKey.Equals(CorrectAnswerKey, StringComparison.OrdinalIgnoreCase)),
                Count = count,
                Percentage = TotalVotes > 0 ? Math.Round((double)count / TotalVotes * 100, 1) : 0
            };
        }).ToList();

        var winner = OptionStats.OrderByDescending(o => o.Count).FirstOrDefault();
        if (winner != null && winner.Count > 0)
        {
            WinningOptionKey = winner.Key;
            WinningOptionText = winner.Text;
            WinningOptionPct = winner.Percentage;
        }

        // Quiz Accuracy Calculation
        if (IsQuizMode && TotalVotes > 0)
        {
            CorrectVotesCount = allFilteredLogs.Count(v => v.OptionKey.Equals(CorrectAnswerKey, StringComparison.OrdinalIgnoreCase));
            IncorrectVotesCount = TotalVotes - CorrectVotesCount;
            AccuracyRatePct = Math.Round((double)CorrectVotesCount / TotalVotes * 100, 1);
        }

        // 2. Average Response Time
        var times = allFilteredLogs.Where(v => v.TimeToVoteSeconds.HasValue && v.TimeToVoteSeconds > 0).Select(v => v.TimeToVoteSeconds!.Value).ToList();
        AvgResponseTime = times.Any() ? Math.Round(times.Average(), 1) : 6.2;

        // 3. Breakdown per City
        CityStats = allFilteredLogs
            .GroupBy(v => string.IsNullOrWhiteSpace(v.City) ? "Nasional" : v.City)
            .Select(g => new CityStatDto
            {
                CityName = g.Key,
                TotalVotes = g.Count(),
                Percentage = Math.Round((double)g.Count() / TotalVotes * 100, 1),
                TopOption = g.GroupBy(v => v.OptionKey).OrderByDescending(og => og.Count()).Select(og => og.Key).FirstOrDefault() ?? "-"
            })
            .OrderByDescending(c => c.TotalVotes)
            .ToList();

        // 4. Breakdown per Titik LED (Site)
        SiteStats = allFilteredLogs
            .GroupBy(v => string.IsNullOrWhiteSpace(v.SiteId) ? "Default LED" : v.SiteId)
            .Select(g => new SiteStatDto
            {
                SiteName = g.Key,
                City = g.Select(v => v.City).FirstOrDefault() ?? "Nasional",
                TotalVotes = g.Count(),
                Percentage = Math.Round((double)g.Count() / TotalVotes * 100, 1),
                TopOption = g.GroupBy(v => v.OptionKey).OrderByDescending(og => og.Count()).Select(og => og.Key).FirstOrDefault() ?? "-"
            })
            .OrderByDescending(s => s.TotalVotes)
            .ToList();

        TopPerformingSite = SiteStats.FirstOrDefault()?.SiteName ?? "-";

        // 5. Device Brand Intelligence
        DeviceBrandStats = allFilteredLogs
            .GroupBy(v => string.IsNullOrWhiteSpace(v.DeviceBrand) ? "Other" : v.DeviceBrand)
            .Select(g => new BrandStatDto
            {
                BrandName = g.Key,
                Count = g.Count(),
                Percentage = Math.Round((double)g.Count() / TotalVotes * 100, 1)
            })
            .OrderByDescending(b => b.Count)
            .ToList();

        // 6. OS Intelligence
        OsStats = allFilteredLogs
            .GroupBy(v => string.IsNullOrWhiteSpace(v.OperatingSystem) ? "Unknown" : v.OperatingSystem)
            .Select(g => new OsStatDto
            {
                OsName = g.Key,
                Count = g.Count(),
                Percentage = Math.Round((double)g.Count() / TotalVotes * 100, 1)
            })
            .OrderByDescending(o => o.Count)
            .ToList();

        // 7. Browser Intelligence
        BrowserStats = allFilteredLogs
            .GroupBy(v => string.IsNullOrWhiteSpace(v.BrowserName) ? "Unknown" : v.BrowserName)
            .Select(g => new BrowserStatDto
            {
                BrowserName = g.Key,
                Count = g.Count(),
                Percentage = Math.Round((double)g.Count() / TotalVotes * 100, 1)
            })
            .OrderByDescending(b => b.Count)
            .ToList();

        // 8. UserQueue & ActivityReport: Daily Scan & Play Per SiteID / LEDID
        DailyActivityStats = allFilteredLogs
            .GroupBy(v => new { Date = v.VotedAt.Date, Site = v.SiteId, City = string.IsNullOrWhiteSpace(v.City) ? "Nasional" : v.City })
            .Select(g => new DailyActivityStatDto
            {
                Date = g.Key.Date,
                SiteId = g.Key.Site,
                City = g.Key.City,
                ScanPlayCount = g.Count()
            })
            .OrderByDescending(d => d.Date)
            .ThenByDescending(d => d.ScanPlayCount)
            .ToList();

        // 9. Recent 50 Logs
        RecentLogs = allFilteredLogs
            .OrderByDescending(v => v.VotedAt)
            .Take(50)
            .ToList();

        return Page();
    }

    public List<DailyActivityStatDto> DailyActivityStats { get; set; } = new();
}

public class DailyActivityStatDto
{
    public DateTime Date { get; set; }
    public string DateFormatted => Date.ToString("dd MMM yyyy");
    public string SiteId { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public int ScanPlayCount { get; set; }
}

public class OptionStatDto
{
    public string Key { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Color { get; set; } = "#3B82F6";
    public bool IsCorrect { get; set; } = false;
    public int Count { get; set; }
    public double Percentage { get; set; }
    public string PercentageCss => Percentage.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}

public class CityStatDto
{
    public string CityName { get; set; } = string.Empty;
    public int TotalVotes { get; set; }
    public double Percentage { get; set; }
    public string TopOption { get; set; } = string.Empty;
    public string PercentageCss => Percentage.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}

public class SiteStatDto
{
    public string SiteName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public int TotalVotes { get; set; }
    public double Percentage { get; set; }
    public string TopOption { get; set; } = string.Empty;
    public string PercentageCss => Percentage.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}

public class BrandStatDto
{
    public string BrandName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
    public string PercentageCss => Percentage.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}

public class OsStatDto
{
    public string OsName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
    public string PercentageCss => Percentage.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}

public class BrowserStatDto
{
    public string BrowserName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
    public string PercentageCss => Percentage.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
