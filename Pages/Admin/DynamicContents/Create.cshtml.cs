using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using CmsBillboard.DynamicContent.Data;
using CmsBillboard.DynamicContent.Models;

namespace CmsBillboard.DynamicContent.Pages.Admin.DynamicContents;

[Authorize]
public class CreateModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(AppDbContext context, IWebHostEnvironment env, ILogger<CreateModel> logger)
    {
        _context = context;
        _env = env;
        _logger = logger;
    }

    [BindProperty]
    public DynamicContentCreateForm Input { get; set; } = new();

    public List<string> AvailableGames { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
        LoadAvailableGames();

        // Default template dengan 4 opsi pilihan ganda
        Input.Options = new List<OptionFormItem>
        {
            new() { OptionKey = "A", OptionText = "", ColorHex = "#3B82F6" },
            new() { OptionKey = "B", OptionText = "", ColorHex = "#10B981" },
            new() { OptionKey = "C", OptionText = "", ColorHex = "#F59E0B" },
            new() { OptionKey = "D", OptionText = "", ColorHex = "#8B5CF6" }
        };
    }

    private void LoadAvailableGames()
    {
        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var gamesDir = Path.Combine(webRoot, "games");
        if (Directory.Exists(gamesDir))
        {
            AvailableGames = Directory.GetDirectories(gamesDir)
                .Select(Path.GetFileName)
                .Where(f => !string.IsNullOrEmpty(f))
                .ToList()!;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var isGame = Input.ContentType.Equals("Game", StringComparison.OrdinalIgnoreCase);

        // Jika tipe Game, QuestionText dan Options tidak wajib
        if (isGame)
        {
            ModelState.Remove("Input.QuestionText");
            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Input.Options")).ToList())
            {
                ModelState.Remove(key);
            }
        }

        if (!ModelState.IsValid)
        {
            LoadAvailableGames();
            return Page();
        }

        // 1. Sanitasi ID / Slug
        var slug = Input.Id.Trim().ToLower().Replace(" ", "-");
        if (await _context.DynamicContentMasters.AnyAsync(s => s.Id == slug))
        {
            ModelState.AddModelError("Input.Id", $"ID/Slug '{slug}' sudah digunakan oleh konten lain. Gunakan ID yang berbeda.");
            LoadAvailableGames();
            return Page();
        }

        List<OptionFormItem> validOptions = new();
        if (!isGame)
        {
            // 2. Validasi minimal 2 opsi untuk survey / quiz
            validOptions = Input.Options
                .Where(o => !string.IsNullOrWhiteSpace(o.OptionText))
                .ToList();

            if (validOptions.Count < 2)
            {
                ErrorMessage = "Konten survey atau quiz wajib memiliki minimal 2 opsi jawaban.";
                LoadAvailableGames();
                return Page();
            }
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
            GameFolder = isGame ? (string.IsNullOrWhiteSpace(Input.GameFolder) ? slug : Input.GameFolder.Trim()) : null,
            GameEntryFile = isGame ? (string.IsNullOrWhiteSpace(Input.GameEntryFile) ? "index.html" : Input.GameEntryFile.Trim()) : null,
            GameCustomParams = isGame ? Input.GameCustomParams?.Trim() : null,
            ParticipationMode = string.IsNullOrWhiteSpace(Input.ParticipationMode) ? "onetime" : Input.ParticipationMode.Trim(),
            GapDurationMinutes = Input.GapDurationMinutes > 0 ? Input.GapDurationMinutes : 10,
            LocationTagDetect = Input.LocationTagDetect,
            WelcomeMessage = Input.WelcomeMessage?.Trim(),
            InstructionComment = Input.InstructionComment?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.DynamicContentMasters.Add(master);

        // 4. Simpan DynamicContentQuestion (hanya jika survey / quiz)
        if (!isGame)
        {
            var isQuiz = master.ContentType.Equals("BrandIQ", StringComparison.OrdinalIgnoreCase);
            var question = new DynamicContentQuestion
            {
                DynamicContentMasterId = master.Id,
                QuestionText = Input.QuestionText?.Trim() ?? string.Empty,
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
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("New dynamic content created: '{Id}' ({ContentType}) for client '{ClientName}'", master.Id, master.ContentType, master.ClientName);
        TempData["SuccessMessage"] = $"Konten '{master.CampaignTitle}' ({master.ContentType}) untuk klien '{master.ClientName}' berhasil dibuat!";

        return RedirectToPage("/Admin/Index");
    }

    public class DynamicContentCreateForm
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

        // Tipe Konten: "Survey" | "BrandIQ" | "Game"
        public string ContentType { get; set; } = "Survey";

        // Game Properties
        public string? GameFolder { get; set; }
        public string? GameEntryFile { get; set; } = "index.html";
        public string? GameCustomParams { get; set; }

        public string ParticipationMode { get; set; } = "onetime";

        public int GapDurationMinutes { get; set; } = 10;

        public bool LocationTagDetect { get; set; } = false;

        public string? WelcomeMessage { get; set; } = "Selamat Datang di Interaktif DOOH Warna Warni";

        public string? InstructionComment { get; set; } = "Silakan isi nama dan nomor telepon untuk berpartisipasi";

        [Required(ErrorMessage = "Teks pertanyaan wajib diisi.")]
        [MaxLength(500)]
        public string? QuestionText { get; set; } = string.Empty;

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
