using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using CmsBillboard.DynamicContent.Data;
using CmsBillboard.DynamicContent.Models;

namespace CmsBillboard.DynamicContent.Pages.Admin.Surveys;

[Authorize]
public class CreateModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(AppDbContext context, ILogger<CreateModel> logger)
    {
        _context = context;
        _logger = logger;
    }

    [BindProperty]
    public SurveyCreateForm Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
        // Default template dengan 4 opsi pilihan ganda
        Input.Options = new List<OptionFormItem>
        {
            new() { OptionKey = "A", OptionText = "", ColorHex = "#3B82F6" },
            new() { OptionKey = "B", OptionText = "", ColorHex = "#10B981" },
            new() { OptionKey = "C", OptionText = "", ColorHex = "#F59E0B" },
            new() { OptionKey = "D", OptionText = "", ColorHex = "#8B5CF6" }
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // 1. Sanitasi ID / Slug
        var slug = Input.Id.Trim().ToLower().Replace(" ", "-");
        if (await _context.SurveyMasters.AnyAsync(s => s.Id == slug))
        {
            ModelState.AddModelError("Input.Id", $"ID/Slug '{slug}' sudah digunakan oleh survey lain. Gunakan ID yang berbeda.");
            return Page();
        }

        // 2. Validasi minimal 2 opsi untuk pilihan ganda
        var validOptions = Input.Options
            .Where(o => !string.IsNullOrWhiteSpace(o.OptionText))
            .ToList();

        if (validOptions.Count < 2)
        {
            ErrorMessage = "Survey pilihan ganda wajib memiliki minimal 2 opsi jawaban.";
            return Page();
        }

        // 3. Simpan SurveyMaster
        var master = new SurveyMaster
        {
            Id = slug,
            ClientName = Input.ClientName.Trim(),
            CampaignTitle = Input.CampaignTitle.Trim(),
            AdHeadline = Input.AdHeadline?.Trim(),
            AdSubheadline = Input.AdSubheadline?.Trim(),
            BannerImageUrl = string.IsNullOrWhiteSpace(Input.BannerImageUrl) 
                ? "/images/samsung_galaxy_fold.svg" 
                : Input.BannerImageUrl.Trim(),
            BrandLogoUrl = Input.BrandLogoUrl?.Trim(),
            ThemeColorHex = string.IsNullOrWhiteSpace(Input.ThemeColorHex) ? "#0072FF" : Input.ThemeColorHex.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.SurveyMasters.Add(master);

        // 4. Simpan SurveyQuestion
        var question = new SurveyQuestion
        {
            SurveyMasterId = master.Id,
            QuestionText = Input.QuestionText.Trim(),
            QuestionType = "MultipleChoice",
            SortOrder = 1
        };

        _context.SurveyQuestions.Add(question);
        await _context.SaveChangesAsync(); // generate question.Id

        // 5. Simpan SurveyOptions
        var alphabet = new[] { "A", "B", "C", "D", "E", "F" };
        for (int i = 0; i < validOptions.Count; i++)
        {
            var opt = validOptions[i];
            var optionKey = i < alphabet.Length ? alphabet[i] : (i + 1).ToString();

            _context.SurveyOptions.Add(new SurveyOption
            {
                QuestionId = question.Id,
                OptionKey = optionKey,
                OptionText = opt.OptionText.Trim(),
                ColorHex = string.IsNullOrWhiteSpace(opt.ColorHex) ? "#3B82F6" : opt.ColorHex.Trim(),
                VoteCount = 0,
                SortOrder = i + 1
            });
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("New custom survey created: '{Id}' for client '{ClientName}'", master.Id, master.ClientName);
        TempData["SuccessMessage"] = $"Survey '{master.CampaignTitle}' untuk klien '{master.ClientName}' berhasil dibuat!";

        return RedirectToPage("/Admin/Index");
    }

    public class SurveyCreateForm
    {
        [Required(ErrorMessage = "ID unik survey wajib diisi.")]
        [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "ID hanya boleh berisi huruf, angka, tanda minus (-), atau underscore (_).")]
        [MaxLength(100)]
        public string Id { get; set; } = string.Empty;

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

        [Required(ErrorMessage = "Teks pertanyaan wajib diisi.")]
        [MaxLength(500)]
        public string QuestionText { get; set; } = string.Empty;

        public List<OptionFormItem> Options { get; set; } = new();
    }

    public class OptionFormItem
    {
        public string OptionKey { get; set; } = "A";

        [Required(ErrorMessage = "Teks opsi wajib diisi.")]
        public string OptionText { get; set; } = string.Empty;

        public string ColorHex { get; set; } = "#3B82F6";
    }
}
