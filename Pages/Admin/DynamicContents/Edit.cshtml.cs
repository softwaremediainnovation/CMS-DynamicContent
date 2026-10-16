using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using CmsBillboard.DynamicContent.Data;
using CmsBillboard.DynamicContent.Models;

namespace CmsBillboard.DynamicContent.Pages.Admin.DynamicContents;

[Authorize]
public class EditModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<EditModel> _logger;

    public EditModel(AppDbContext context, IWebHostEnvironment env, ILogger<EditModel> logger)
    {
        _context = context;
        _env = env;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;

    [BindProperty]
    public DynamicContentEditForm Input { get; set; } = new();

    public List<string> AvailableGames { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public int TotalVotes { get; set; }

    public async Task<IActionResult> OnGetAsync(string? id)
    {
        LoadAvailableGames();

        if (string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(id))
        {
            Id = id;
        }

        if (string.IsNullOrWhiteSpace(Id) && Request.Query.ContainsKey("id"))
        {
            Id = Request.Query["id"].ToString();
        }

        if (string.IsNullOrWhiteSpace(Id))
        {
            Id = "tolak-angin-live";
        }

        var master = await _context.DynamicContentMasters
            .Include(m => m.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(m => m.Id == Id);

        if (master == null)
        {
            return NotFound($"Konten dengan ID '{Id}' tidak ditemukan.");
        }

        Id = master.Id;

        var primaryQuestion = master.Questions.OrderBy(q => q.SortOrder).FirstOrDefault();

        Input = new DynamicContentEditForm
        {
            Id = master.Id,
            ClientName = master.ClientName,
            CampaignTitle = master.CampaignTitle,
            AdHeadline = master.AdHeadline,
            AdSubheadline = master.AdSubheadline,
            BannerImageUrl = master.BannerImageUrl,
            BrandLogoUrl = master.BrandLogoUrl,
            ThemeColorHex = master.ThemeColorHex,
            ContentType = master.ContentType,
            GameFolder = master.GameFolder,
            GameEntryFile = master.GameEntryFile ?? "index.html",
            GameCustomParams = master.GameCustomParams,
            ParticipationMode = master.ParticipationMode,
            GapDurationMinutes = master.GapDurationMinutes,
            LocationTagDetect = master.LocationTagDetect,
            WelcomeMessage = master.WelcomeMessage,
            InstructionComment = master.InstructionComment,
            IsActive = master.IsActive,
            SlotConfigJson = master.SlotConfigJson,
            QuestionText = primaryQuestion?.QuestionText ?? string.Empty,
            CorrectOptionKey = primaryQuestion?.CorrectOptionKey,
            Options = primaryQuestion?.Options.OrderBy(o => o.SortOrder).Select(o => new EditOptionItem
            {
                Id = o.Id,
                OptionKey = o.OptionKey,
                OptionText = o.OptionText,
                OptionImageUrl = o.OptionImageUrl,
                ColorHex = o.ColorHex,
                IsCorrect = o.IsCorrect,
                VoteCount = o.VoteCount
            }).ToList() ?? new()
        };

        TotalVotes = Input.Options.Sum(o => o.VoteCount);

        return Page();
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

    public async Task<IActionResult> OnPostUpdateAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(id))
        {
            Id = id;
        }

        if (string.IsNullOrWhiteSpace(Id) && Request.Query.ContainsKey("id"))
        {
            Id = Request.Query["id"].ToString();
        }

        if (string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(Input.Id))
        {
            Id = Input.Id;
        }

        if (string.IsNullOrWhiteSpace(Id) && Request.HasFormContentType && Request.Form.ContainsKey("Id"))
        {
            Id = Request.Form["Id"].ToString();
        }

        if (string.IsNullOrWhiteSpace(Id))
        {
            Id = "tolak-angin-live";
        }

        var isGame = Input.ContentType.Equals("Game", StringComparison.OrdinalIgnoreCase);
        var isPhotoComment = Input.ContentType.Equals("PhotoComment", StringComparison.OrdinalIgnoreCase);

        if (isGame || isPhotoComment)
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

        var master = await _context.DynamicContentMasters
            .Include(m => m.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(m => m.Id == Id);

        if (master == null)
            return NotFound();

        // Update Master
        master.ClientName = Input.ClientName.Trim();
        master.CampaignTitle = Input.CampaignTitle.Trim();
        master.AdHeadline = Input.AdHeadline?.Trim();
        master.AdSubheadline = Input.AdSubheadline?.Trim();
        master.BannerImageUrl = string.IsNullOrWhiteSpace(Input.BannerImageUrl) ? null : Input.BannerImageUrl.Trim();
        master.BrandLogoUrl = string.IsNullOrWhiteSpace(Input.BrandLogoUrl) ? null : Input.BrandLogoUrl.Trim();
        master.ThemeColorHex = Input.ThemeColorHex?.Trim() ?? (isPhotoComment ? "#D97706" : "#0072FF");
        master.ContentType = string.IsNullOrWhiteSpace(Input.ContentType) ? "Survey" : Input.ContentType.Trim();
        master.GameFolder = isGame ? Input.GameFolder?.Trim() : null;
        master.GameEntryFile = isGame ? (string.IsNullOrWhiteSpace(Input.GameEntryFile) ? "index.html" : Input.GameEntryFile.Trim()) : null;
        master.GameCustomParams = isGame ? Input.GameCustomParams?.Trim() : null;
        master.ParticipationMode = isPhotoComment ? "multipletime" : (string.IsNullOrWhiteSpace(Input.ParticipationMode) ? "onetime" : Input.ParticipationMode.Trim());
        master.GapDurationMinutes = isPhotoComment ? (Input.GapDurationMinutes > 0 ? Input.GapDurationMinutes : 15) : (Input.GapDurationMinutes > 0 ? Input.GapDurationMinutes : 10);
        master.LocationTagDetect = Input.LocationTagDetect;
        master.WelcomeMessage = Input.WelcomeMessage?.Trim();
        master.InstructionComment = Input.InstructionComment?.Trim();
        master.IsActive = Input.IsActive;
        master.SlotConfigJson = isPhotoComment ? Input.SlotConfigJson : master.SlotConfigJson;

        // Update Question
        if (isPhotoComment)
        {
            var question = master.Questions.FirstOrDefault();
            if (question == null)
            {
                question = new DynamicContentQuestion
                {
                    DynamicContentMasterId = master.Id,
                    QuestionText = !string.IsNullOrWhiteSpace(Input.QuestionText) ? Input.QuestionText.Trim() : "Live Photo & Greetings",
                    QuestionType = "photo",
                    SortOrder = 1
                };
                _context.DynamicContentQuestions.Add(question);
                _context.DynamicContentOptions.Add(new DynamicContentOption
                {
                    Question = question,
                    OptionKey = "PHOTO",
                    OptionText = "Foto Partisipan",
                    ColorHex = master.ThemeColorHex,
                    IsCorrect = false,
                    SortOrder = 1
                });
            }
        }
        else if (!isGame)
        {
            var question = master.Questions.OrderBy(q => q.SortOrder).FirstOrDefault();
            if (question != null)
            {
                question.QuestionText = Input.QuestionText?.Trim() ?? string.Empty;
                question.CorrectOptionKey = master.ContentType == "BrandIQ" ? Input.CorrectOptionKey : null;

                // Update Options
                foreach (var optInput in Input.Options)
                {
                    var existingOpt = question.Options.FirstOrDefault(o => o.Id == optInput.Id);
                    if (existingOpt != null)
                    {
                        existingOpt.OptionText = optInput.OptionText.Trim();
                        existingOpt.OptionImageUrl = optInput.OptionImageUrl?.Trim();
                        existingOpt.ColorHex = optInput.ColorHex.Trim();
                        existingOpt.IsCorrect = master.ContentType == "BrandIQ" && existingOpt.OptionKey == Input.CorrectOptionKey;
                    }
                }
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Content '{Id}' updated by admin", master.Id);
        TempData["SuccessMessage"] = $"Konten '{master.CampaignTitle}' berhasil diperbarui!";

        return RedirectToPage("/Admin/Index");
    }

    public async Task<IActionResult> OnPostResetVotesAsync()
    {
        var master = await _context.DynamicContentMasters
            .Include(m => m.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(m => m.Id == Id);

        if (master == null)
            return NotFound();

        foreach (var q in master.Questions)
        {
            foreach (var opt in q.Options)
            {
                opt.VoteCount = 0;
            }
        }

        var voteLogs = _context.DynamicContentVoteLogs.Where(v => v.DynamicContentMasterId == Id);
        _context.DynamicContentVoteLogs.RemoveRange(voteLogs);

        await _context.SaveChangesAsync();

        _logger.LogInformation("All votes and logs for content '{Id}' reset by admin", Id);
        TempData["SuccessMessage"] = $"Seluruh data suara dan analitik log untuk '{master.CampaignTitle}' telah di-reset ke 0.";

        return RedirectToPage("/Admin/Surveys/Edit", new { id = Id });
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var master = await _context.DynamicContentMasters
            .Include(m => m.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(m => m.Id == Id);

        if (master == null)
            return NotFound();

        var voteLogs = _context.DynamicContentVoteLogs.Where(v => v.DynamicContentMasterId == Id);
        _context.DynamicContentVoteLogs.RemoveRange(voteLogs);

        foreach (var q in master.Questions)
        {
            _context.DynamicContentOptions.RemoveRange(q.Options);
        }
        _context.DynamicContentQuestions.RemoveRange(master.Questions);

        _context.DynamicContentMasters.Remove(master);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Content '{Id}' deleted by admin", Id);
        TempData["SuccessMessage"] = $"Konten '{master.CampaignTitle}' berhasil dihapus.";

        return RedirectToPage("/Admin/Index");
    }

    public class DynamicContentEditForm
    {
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

        public string ContentType { get; set; } = "Survey";

        public string? GameFolder { get; set; }
        public string? GameEntryFile { get; set; } = "index.html";
        public string? GameCustomParams { get; set; }

        public string ParticipationMode { get; set; } = "onetime";
        public int GapDurationMinutes { get; set; } = 10;
        public bool LocationTagDetect { get; set; } = false;
        public string? WelcomeMessage { get; set; }
        public string? InstructionComment { get; set; }

        public bool IsActive { get; set; } = true;

        public string? SlotConfigJson { get; set; }

        [Required(ErrorMessage = "Teks pertanyaan wajib diisi.")]
        [MaxLength(500)]
        public string? QuestionText { get; set; } = string.Empty;

        public string? CorrectOptionKey { get; set; }

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
        public bool IsCorrect { get; set; } = false;
        public int VoteCount { get; set; }
    }
}
