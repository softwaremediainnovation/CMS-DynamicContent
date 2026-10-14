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
        if (await _context.DynamicContentMasters.AnyAsync(s => s.Id == slug))
        {
            ModelState.AddModelError("Input.Id", $"ID/Slug '{slug}' sudah digunakan oleh konten lain. Gunakan ID yang berbeda.");
            return Page();
        }

        // 2. Validasi minimal 2 opsi untuk pilihan ganda
        var validOptions = Input.Options
            .Where(o => !string.IsNullOrWhiteSpace(o.OptionText))
            .ToList();

        if (validOptions.Count < 2)
        {
            ErrorMessage = "Konten pilihan ganda wajib memiliki minimal 2 opsi jawaban.";
            return Page();
        }

        // 3. Simpan DynamicContentMaster
        var master = new DynamicContentMaster
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
            ContentType = string.IsNullOrWhiteSpace(Input.ContentType) ? "Survey" : Input.ContentType.Trim(),
            ParticipationMode = string.IsNullOrWhiteSpace(Input.ParticipationMode) ? "onetime" : Input.ParticipationMode.Trim(),
            GapDurationMinutes = Input.GapDurationMinutes > 0 ? Input.GapDurationMinutes : 10,
            LocationTagDetect = Input.LocationTagDetect,
            WelcomeMessage = Input.WelcomeMessage?.Trim(),
            InstructionComment = Input.InstructionComment?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.DynamicContentMasters.Add(master);

        // 4. Simpan DynamicContentQuestion
        var isQuiz = master.ContentType.Equals("BrandIQ", StringComparison.OrdinalIgnoreCase);
        var question = new DynamicContentQuestion
        {
            DynamicContentMasterId = master.Id,
            QuestionText = Input.QuestionText.Trim(),
            QuestionType = "multiple",
            CorrectOptionKey = isQuiz ? Input.CorrectOptionKey : null,
            SortOrder = 1
        };

        _context.DynamicContentQuestions.Add(question);
        await _context.SaveChangesAsync(); // generate question.Id

        // 5. Simpan DynamicContentOptions
        var alphabet = new[] { "A", "B", "C", "D", "E", "F" };
        for (int i = 0; i < validOptions.Count; i++)
        {
            var opt = validOptions[i];
            var optionKey = i < alphabet.Length ? alphabet[i] : (i + 1).ToString();
            var isCorrect = isQuiz && optionKey.Equals(Input.CorrectOptionKey, StringComparison.OrdinalIgnoreCase);

            _context.DynamicContentOptions.Add(new DynamicContentOption
            {
                QuestionId = question.Id,
                OptionKey = optionKey,
                OptionText = opt.OptionText.Trim(),
                OptionImageUrl = opt.OptionImageUrl?.Trim(),
                ColorHex = string.IsNullOrWhiteSpace(opt.ColorHex) ? "#3B82F6" : opt.ColorHex.Trim(),
                IsCorrect = isCorrect,
                VoteCount = 0,
                SortOrder = i + 1
            });
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("New dynamic content created: '{Id}' for client '{ClientName}'", master.Id, master.ClientName);
        TempData["SuccessMessage"] = $"Konten '{master.CampaignTitle}' untuk klien '{master.ClientName}' berhasil dibuat!";

        return RedirectToPage("/Admin/Index");
    }

    public class SurveyCreateForm
    {
        [Required(ErrorMessage = "ID unik konten wajib diisi.")]
        [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "ID hanya boleh berisi huruf, angka, tanda minus (-), atau underscore (_).")]
        [MaxLength(100)]
        public string Id { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nama klien / brand wajib diisi.")]
        [MaxLength(150)]
        public string ClientName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Judul kampanye wajib diisi.")]
        [MaxLength(200)]
        public string CampaignTitle { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? AdHeadline { get; set; }

        [MaxLength(200)]
        public string? AdSubheadline { get; set; }

        public string? BannerImageUrl { get; set; }

        public string? BrandLogoUrl { get; set; }

        public string ThemeColorHex { get; set; } = "#0072FF";

        // Tipe Konten: "Survey" | "BrandIQ"
        public string ContentType { get; set; } = "Survey";

        public string ParticipationMode { get; set; } = "onetime";

        public int GapDurationMinutes { get; set; } = 10;

        public bool LocationTagDetect { get; set; } = false;

        public string? WelcomeMessage { get; set; } = "Selamat Datang di Interaktif DOOH Warna Warni";

        public string? InstructionComment { get; set; } = "Silakan isi nama dan nomor telepon untuk berpartisipasi";

        [Required(ErrorMessage = "Teks pertanyaan wajib diisi.")]
        [MaxLength(500)]
        public string QuestionText { get; set; } = string.Empty;

        // Kunci jawaban jika ContentType == "BrandIQ"
        public string? CorrectOptionKey { get; set; } = "A";

        public List<OptionFormItem> Options { get; set; } = new();
    }

    public class OptionFormItem
    {
        public string OptionKey { get; set; } = "A";

        [Required(ErrorMessage = "Teks opsi wajib diisi.")]
        public string OptionText { get; set; } = string.Empty;

        public string? OptionImageUrl { get; set; }

        public string ColorHex { get; set; } = "#3B82F6";
    }
}
