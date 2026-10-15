using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using CmsBillboard.DynamicContent.Data;
using CmsBillboard.DynamicContent.Models;
using CmsBillboard.DynamicContent.Services;

namespace CmsBillboard.DynamicContent.Pages.Screen;

public class PhotoCommentModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly IDynamicContentService _contentService;
    private readonly ISecurityTokenService _tokenService;

    public PhotoCommentModel(
        AppDbContext context,
        IDynamicContentService contentService,
        ISecurityTokenService tokenService)
    {
        _context = context;
        _contentService = contentService;
        _tokenService = tokenService;
    }

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = "tolak-angin-live";

    [BindProperty(SupportsGet = true)]
    public string Site { get; set; } = "Gedung Sate Bandung";

    [BindProperty(SupportsGet = true)]
    public string Mode { get; set; } = "onpremise";

    [BindProperty(SupportsGet = true)]
    public string? Ip { get; set; }

    public DynamicContentMaster? DynamicContent { get; set; }
    public string QrCodeSvg { get; set; } = string.Empty;
    public string VoteUrl { get; set; } = string.Empty;
    public int TotalPhotos { get; set; } = 0;
    public int TotalParticipants { get; set; } = 0;
    public List<PhotoCommentItemDto> InitialPhotos { get; set; } = new();
    public PhotoCommentSlotConfig SlotConfig { get; set; } = new();

    public bool IsTokenValid { get; set; } = true;
    public string? TokenErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        // 1. If Token is provided, decode and strictly validate signature & expiry
        if (!string.IsNullOrWhiteSpace(Token))
        {
            var (isValid, payload, errorMessage) = _tokenService.ValidateVoteToken(Token);
            if (!isValid || payload == null)
            {
                IsTokenValid = false;
                TokenErrorMessage = errorMessage ?? "Token layar tidak valid atau telah dimodifikasi!";
                return Page();
            }

            Id = payload.ContentId;
            Site = payload.SiteId;
            if (!string.IsNullOrWhiteSpace(payload.Mode))
            {
                Mode = payload.Mode;
            }
        }

        if (string.IsNullOrWhiteSpace(Id))
        {
            Id = "tolak-angin-live";
        }

        DynamicContent = await _context.DynamicContentMasters
            .FirstOrDefaultAsync(m => m.Id == Id);

        if (DynamicContent == null && Id != "tolak-angin-live")
        {
            DynamicContent = await _context.DynamicContentMasters
                .FirstOrDefaultAsync(m => m.ContentType == "PhotoComment" && m.IsActive);
            if (DynamicContent != null)
            {
                Id = DynamicContent.Id;
            }
        }

        if (DynamicContent == null)
        {
            return NotFound($"Dynamic Content Photo Comment '{Id}' tidak ditemukan.");
        }

        // Deserialize Slot Configuration
        if (!string.IsNullOrWhiteSpace(DynamicContent.SlotConfigJson))
        {
            try
            {
                SlotConfig = System.Text.Json.JsonSerializer.Deserialize<PhotoCommentSlotConfig>(
                    DynamicContent.SlotConfigJson,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            catch
            {
                SlotConfig = new PhotoCommentSlotConfig();
            }
        }
        else
        {
            SlotConfig = new PhotoCommentSlotConfig();
        }

        // Build mobile voting URL with tamper-proof encrypted token
        var baseUrl = NetworkHelper.ResolveAccessibleBaseUrl(Request, Ip);
        var mobileToken = _tokenService.GenerateVoteToken(Id, Site, Mode, expiryMinutes: 180);
        VoteUrl = $"{baseUrl}/PhotoComment?token={mobileToken}";

        // Generate QR Code SVG (fallback)
        QrCodeSvg = _contentService.GenerateQrCodeSvg(VoteUrl);

        // Load initial photos
        var logs = await _context.DynamicContentVoteLogs
            .Where(v => v.DynamicContentMasterId == Id && !string.IsNullOrEmpty(v.PhotoUrl))
            .OrderByDescending(v => v.VotedAt)
            .Take(20)
            .ToListAsync();

        TotalPhotos = await _context.DynamicContentVoteLogs
            .CountAsync(v => v.DynamicContentMasterId == Id && !string.IsNullOrEmpty(v.PhotoUrl));

        TotalParticipants = await _context.DynamicContentVoteLogs
            .Where(v => v.DynamicContentMasterId == Id)
            .Select(v => v.DeviceFingerprint)
            .Distinct()
            .CountAsync();

        var now = DateTime.UtcNow;
        InitialPhotos = logs.Select(l => new PhotoCommentItemDto
        {
            Id = l.Id,
            UserName = l.UserName,
            CommentText = l.TextResponse ?? string.Empty,
            PhotoUrl = l.PhotoUrl ?? string.Empty,
            SiteId = l.SiteId,
            PostedAt = l.VotedAt,
            TimeAgo = FormatTimeAgo(now - l.VotedAt)
        }).ToList();

        return Page();
    }

    private static string FormatTimeAgo(TimeSpan span)
    {
        if (span.TotalSeconds < 60) return "Baru saja";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} menit lalu";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} jam lalu";
        return $"{(int)span.TotalDays} hari lalu";
    }
}
