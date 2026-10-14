using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using CmsBillboard.DynamicContent.Data;
using CmsBillboard.DynamicContent.Models;

namespace CmsBillboard.DynamicContent.Pages.Admin.Surveys;

[Authorize]
public class EditModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly ILogger<EditModel> _logger;

    public EditModel(AppDbContext context, ILogger<EditModel> logger)
    {
        _context = context;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;

    [BindProperty]
    public SurveyEditForm Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public int TotalVotes { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var master = await _context.SurveyMasters
            .Include(m => m.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(m => m.Id == Id);

        if (master == null)
        {
            return NotFound($"Survey dengan ID '{Id}' tidak ditemukan.");
        }

        var primaryQuestion = master.Questions.OrderBy(q => q.SortOrder).FirstOrDefault();

        Input = new SurveyEditForm
        {
            ClientName = master.ClientName,
            CampaignTitle = master.CampaignTitle,
            AdHeadline = master.AdHeadline,
            AdSubheadline = master.AdSubheadline,
            BannerImageUrl = master.BannerImageUrl,
            BrandLogoUrl = master.BrandLogoUrl,
            ThemeColorHex = master.ThemeColorHex,
            ContentType = master.ContentType,
            ParticipationMode = master.ParticipationMode,
            GapDurationMinutes = master.GapDurationMinutes,
            LocationTagDetect = master.LocationTagDetect,
            WelcomeMessage = master.WelcomeMessage,
            InstructionComment = master.InstructionComment,
            IsActive = master.IsActive,
            QuestionText = primaryQuestion?.QuestionText ?? string.Empty,
            Options = primaryQuestion?.Options.OrderBy(o => o.SortOrder).Select(o => new EditOptionItem
            {
                Id = o.Id,
                OptionKey = o.OptionKey,
                OptionText = o.OptionText,
                OptionImageUrl = o.OptionImageUrl,
                ColorHex = o.ColorHex,
                VoteCount = o.VoteCount
            }).ToList() ?? new()
        };

        TotalVotes = Input.Options.Sum(o => o.VoteCount);

        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        var master = await _context.SurveyMasters
            .Include(m => m.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(m => m.Id == Id);

        if (master == null)
            return NotFound();

        if (!ModelState.IsValid)
            return Page();

        // Update Master
        master.ClientName = Input.ClientName.Trim();
        master.CampaignTitle = Input.CampaignTitle.Trim();
        master.AdHeadline = Input.AdHeadline?.Trim();
        master.AdSubheadline = Input.AdSubheadline?.Trim();
        master.BannerImageUrl = Input.BannerImageUrl?.Trim();
        master.BrandLogoUrl = Input.BrandLogoUrl?.Trim();
        master.ThemeColorHex = Input.ThemeColorHex?.Trim() ?? "#0072FF";
        master.ContentType = string.IsNullOrWhiteSpace(Input.ContentType) ? "Survey" : Input.ContentType.Trim();
        master.ParticipationMode = string.IsNullOrWhiteSpace(Input.ParticipationMode) ? "onetime" : Input.ParticipationMode.Trim();
        master.GapDurationMinutes = Input.GapDurationMinutes > 0 ? Input.GapDurationMinutes : 10;
        master.LocationTagDetect = Input.LocationTagDetect;
        master.WelcomeMessage = Input.WelcomeMessage?.Trim();
        master.InstructionComment = Input.InstructionComment?.Trim();
        master.IsActive = Input.IsActive;

        // Update Question
        var question = master.Questions.OrderBy(q => q.SortOrder).FirstOrDefault();
        if (question != null)
        {
            question.QuestionText = Input.QuestionText.Trim();

            // Update Options
            foreach (var optInput in Input.Options)
            {
                var existingOpt = question.Options.FirstOrDefault(o => o.Id == optInput.Id);
                if (existingOpt != null)
                {
                    existingOpt.OptionText = optInput.OptionText.Trim();
                    existingOpt.OptionImageUrl = optInput.OptionImageUrl?.Trim();
                    existingOpt.ColorHex = optInput.ColorHex.Trim();
                }
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Survey '{Id}' updated by admin", master.Id);
        TempData["SuccessMessage"] = $"Survey '{master.CampaignTitle}' berhasil diperbarui!";

        return RedirectToPage("/Admin/Index");
    }

    public async Task<IActionResult> OnPostResetVotesAsync()
    {
        var master = await _context.SurveyMasters
            .Include(m => m.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(m => m.Id == Id);

        if (master == null)
            return NotFound();

        // Reset all option vote counts to 0
        foreach (var q in master.Questions)
        {
            foreach (var opt in q.Options)
            {
                opt.VoteCount = 0;
            }
        }

        // Delete all vote logs for this survey
        var logs = await _context.SurveyVoteLogs
            .Where(v => v.SurveyMasterId == Id)
            .ToListAsync();

        _context.SurveyVoteLogs.RemoveRange(logs);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Votes reset for Survey '{Id}'", Id);
        TempData["SuccessMessage"] = $"Seluruh skor suara untuk survey '{master.CampaignTitle}' berhasil di-reset ke 0!";

        return RedirectToPage("/Admin/Surveys/Edit", new { id = Id });
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var master = await _context.SurveyMasters
            .Include(m => m.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(m => m.Id == Id);

        if (master == null)
            return NotFound();

        // Delete related logs
        var logs = await _context.SurveyVoteLogs
            .Where(v => v.SurveyMasterId == Id)
            .ToListAsync();
        _context.SurveyVoteLogs.RemoveRange(logs);

        _context.SurveyMasters.Remove(master);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Survey '{Id}' deleted by admin", Id);
        TempData["SuccessMessage"] = $"Survey '{master.CampaignTitle}' berhasil dihapus.";

        return RedirectToPage("/Admin/Index");
    }

    public class SurveyEditForm
    {
        [Required(ErrorMessage = "Nama klien / brand wajib diisi.")]
        [MaxLength(150)]
        public string ClientName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Judul kampanye survey wajib diisi.")]
        [MaxLength(200)]
        public string CampaignTitle { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? AdHeadline { get; set; }

        [MaxLength(200)]
        public string? AdSubheadline { get; set; }

        public string? BannerImageUrl { get; set; }

        public string? BrandLogoUrl { get; set; }

        public string ThemeColorHex { get; set; } = "#0072FF";

        // Parameter Revisi Bos
        public string ContentType { get; set; } = "Survey";
        public string ParticipationMode { get; set; } = "onetime";
        public int GapDurationMinutes { get; set; } = 10;
        public bool LocationTagDetect { get; set; } = false;
        public string? WelcomeMessage { get; set; }
        public string? InstructionComment { get; set; }

        public bool IsActive { get; set; } = true;

        [Required(ErrorMessage = "Teks pertanyaan wajib diisi.")]
        [MaxLength(500)]
        public string QuestionText { get; set; } = string.Empty;

        public List<EditOptionItem> Options { get; set; } = new();
    }

    public class EditOptionItem
    {
        public int Id { get; set; }
        public string OptionKey { get; set; } = "A";
        [Required]
        public string OptionText { get; set; } = string.Empty;
        public string? OptionImageUrl { get; set; }
        public string ColorHex { get; set; } = "#3B82F6";
        public int VoteCount { get; set; }
    }
}
