using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using CmsBillboard.DynamicContent.Data;
using CmsBillboard.DynamicContent.Hubs;
using CmsBillboard.DynamicContent.Models;

namespace CmsBillboard.DynamicContent.Services;

public class DynamicContentService : IDynamicContentService, ISurveyService
{
    private readonly AppDbContext _context;
    private readonly IHubContext<DynamicContentHub> _hubContext;
    private readonly ISecurityTokenService _tokenService;
    private readonly ILogger<DynamicContentService> _logger;

    public DynamicContentService(
        AppDbContext context,
        IHubContext<DynamicContentHub> hubContext,
        ISecurityTokenService tokenService,
        ILogger<DynamicContentService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<DynamicContentMaster?> GetDynamicContentMasterAsync(string id)
    {
        return await _context.DynamicContentMasters
            .Include(m => m.Questions.OrderBy(q => q.SortOrder))
                .ThenInclude(q => q.Options.OrderBy(o => o.SortOrder))
            .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);
    }

    public async Task<DynamicContentResultDto?> GetDynamicContentResultsAsync(string id, string? siteId = null, int? questionId = null)
    {
        var master = await _context.DynamicContentMasters
            .Include(m => m.Questions.OrderBy(q => q.SortOrder))
                .ThenInclude(q => q.Options.OrderBy(o => o.SortOrder))
            .FirstOrDefaultAsync(m => m.Id == id);

        if (master == null)
            return null;

        var question = questionId.HasValue
            ? master.Questions.FirstOrDefault(q => q.Id == questionId.Value)
            : master.Questions.FirstOrDefault();

        if (question == null && master.ContentType != "Game")
            return null;

        var options = question?.Options.OrderBy(o => o.SortOrder).ToList() ?? new List<DynamicContentOption>();
        int totalVotes = options.Sum(o => o.VoteCount);

        var resultDto = new DynamicContentResultDto
        {
            DynamicContentId = master.Id,
            ClientName = master.ClientName,
            CampaignTitle = master.CampaignTitle,
            AdHeadline = master.AdHeadline,
            AdSubheadline = master.AdSubheadline,
            BannerImageUrl = master.BannerImageUrl,
            BrandLogoUrl = master.BrandLogoUrl,
            ThemeColorHex = master.ThemeColorHex,
            SiteId = siteId ?? "Nasional",
            TotalVotes = totalVotes,
            ContentType = master.ContentType,
            ParticipationMode = master.ParticipationMode,
            GapDurationMinutes = master.GapDurationMinutes,
            LocationTagDetect = master.LocationTagDetect,
            WelcomeMessage = master.WelcomeMessage,
            InstructionComment = master.InstructionComment,
            GameFolder = master.GameFolder,
            GameEntryFile = master.GameEntryFile ?? "index.html",
            GameCustomParams = master.GameCustomParams,
            QuestionId = question?.Id ?? 0,
            QuestionText = question?.QuestionText ?? string.Empty,
            QuestionType = question?.QuestionType ?? "game",
            QuestionImageUrl = question?.QuestionImageUrl,
            CorrectOptionKey = question?.CorrectOptionKey,
            Options = options.Select(o => new DynamicContentOptionResultDto
            {
                OptionId = o.Id,
                OptionKey = o.OptionKey,
                OptionText = o.OptionText,
                OptionImageUrl = o.OptionImageUrl,
                ColorHex = o.ColorHex,
                IsCorrect = o.IsCorrect || (!string.IsNullOrWhiteSpace(question?.CorrectOptionKey) && o.OptionKey.Equals(question.CorrectOptionKey, StringComparison.OrdinalIgnoreCase)),
                VoteCount = o.VoteCount,
                Percentage = totalVotes > 0 ? Math.Round((double)o.VoteCount / totalVotes * 100.0, 1) : 0
            }).ToList()
        };

        // Calculate Right vs Wrong statistics for BrandGenius / BrandIQ Quiz modes
        var correctOptItem = resultDto.Options.FirstOrDefault(o => o.IsCorrect);
        resultDto.CorrectVotesCount = correctOptItem?.VoteCount ?? 0;
        resultDto.IncorrectVotesCount = Math.Max(0, totalVotes - resultDto.CorrectVotesCount);
        resultDto.AccuracyRatePct = totalVotes > 0 ? Math.Round((double)resultDto.CorrectVotesCount / totalVotes * 100.0, 1) : 0;

        return resultDto;
    }

    public async Task<bool> HasUserVotedAsync(string id, string deviceFingerprint, int? questionId = null)
    {
        if (string.IsNullOrWhiteSpace(deviceFingerprint))
            return false;

        var query = _context.DynamicContentVoteLogs
            .Where(v => v.DynamicContentMasterId == id && v.DeviceFingerprint == deviceFingerprint);

        if (questionId.HasValue && questionId.Value > 0)
        {
            query = query.Where(v => v.QuestionId == questionId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task<DynamicContentVoteResponse> SubmitVoteAsync(DynamicContentVoteRequest request, string? clientIp)
    {
        // 1. If Token is provided, validate cryptographic signature and expiry
        if (!string.IsNullOrWhiteSpace(request.Token))
        {
            var (isValid, payload, errorMessage) = _tokenService.ValidateVoteToken(request.Token);
            if (!isValid || payload == null)
            {
                return new DynamicContentVoteResponse
                {
                    Success = false,
                    Message = errorMessage ?? "Token keamanan tidak sah atau sudah kedaluwarsa!"
                };
            }

            // Enforce genuine parameters from cryptographically signed token
            request.DynamicContentId = payload.ContentId;
            request.SiteId = payload.SiteId;
        }

        var contentId = !string.IsNullOrWhiteSpace(request.DynamicContentId) ? request.DynamicContentId : request.SurveyId;

        // 2. Load Content Master & Questions
        var master = await _context.DynamicContentMasters
            .Include(m => m.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(m => m.Id == contentId && m.IsActive);

        if (master == null)
        {
            return new DynamicContentVoteResponse
            {
                Success = false,
                Message = "Konten tidak ditemukan atau sudah tidak aktif."
            };
        }

        var question = request.QuestionId > 0
            ? master.Questions.FirstOrDefault(q => q.Id == request.QuestionId)
            : master.Questions.FirstOrDefault();

        if (question == null)
        {
            return new DynamicContentVoteResponse
            {
                Success = false,
                Message = "Pertanyaan tidak ditemukan."
            };
        }

        request.QuestionId = question.Id;

        // 3. Rule: Check Participation Mode (onetime vs multipletime with gap duration)
        if (string.Equals(master.ParticipationMode, "multipletime", StringComparison.OrdinalIgnoreCase))
        {
            var lastVote = await _context.DynamicContentVoteLogs
                .Where(v => v.DynamicContentMasterId == master.Id && v.DeviceFingerprint == request.DeviceFingerprint)
                .OrderByDescending(v => v.VotedAt)
                .FirstOrDefaultAsync();

            if (lastVote != null)
            {
                var minutesSinceLast = (DateTime.UtcNow - lastVote.VotedAt).TotalMinutes;
                if (minutesSinceLast < master.GapDurationMinutes)
                {
                    int remainingMin = (int)Math.Ceiling(master.GapDurationMinutes - minutesSinceLast);
                    return new DynamicContentVoteResponse
                    {
                        Success = false,
                        Message = $"Mode multi-partisipasi memerlukan jeda {master.GapDurationMinutes} menit. Silakan coba lagi dalam {remainingMin} menit.",
                        CurrentResults = await GetDynamicContentResultsAsync(contentId, request.SiteId, question.Id)
                    };
                }
            }
        }
        else
        {
            // Default: Strict 1-Time Participation
            if (await HasUserVotedAsync(contentId, request.DeviceFingerprint, question.Id))
            {
                return new DynamicContentVoteResponse
                {
                    Success = false,
                    Message = "Anda sudah memberikan suara dalam konten ini. Partisipasi dibatasi 1 kali per perangkat.",
                    CurrentResults = await GetDynamicContentResultsAsync(contentId, request.SiteId, question.Id)
                };
            }
        }

        var option = question.Options.FirstOrDefault(o => o.OptionKey.Equals(request.OptionKey, StringComparison.OrdinalIgnoreCase));
        if (option == null)
        {
            return new DynamicContentVoteResponse
            {
                Success = false,
                Message = $"Pilihan {request.OptionKey} tidak valid untuk pertanyaan ini."
            };
        }

        // Increment vote count
        option.VoteCount += 1;

        // Determine City from request or deduce from SiteId
        string city = !string.IsNullOrWhiteSpace(request.City) ? request.City.Trim() : "Nasional";
        if ((city == "Nasional" || string.IsNullOrWhiteSpace(city)) && !string.IsNullOrWhiteSpace(request.SiteId))
        {
            var siteLower = request.SiteId.ToLowerInvariant();
            if (siteLower.Contains("surabaya") || siteLower.Contains("sby")) city = "Surabaya";
            else if (siteLower.Contains("jakarta") || siteLower.Contains("jkt")) city = "Jakarta";
            else if (siteLower.Contains("medan") || siteLower.Contains("mdn")) city = "Medan";
            else if (siteLower.Contains("bandung") || siteLower.Contains("bdg")) city = "Bandung";
            else if (siteLower.Contains("bali") || siteLower.Contains("dps")) city = "Bali";
            else city = request.SiteId;
        }

        // Record vote log with audience device telemetry & boss parameters
        var voteLog = new DynamicContentVoteLog
        {
            DynamicContentMasterId = master.Id,
            QuestionId = question.Id,
            OptionKey = option.OptionKey,
            TextResponse = request.TextResponse,
            UserName = string.IsNullOrWhiteSpace(request.UserName) ? "Anonim" : request.UserName.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            SiteId = string.IsNullOrWhiteSpace(request.SiteId) ? "Default" : request.SiteId.Trim(),
            City = city,
            DeviceFingerprint = request.DeviceFingerprint.Trim(),
            IpAddress = clientIp,
            DeviceType = string.IsNullOrWhiteSpace(request.DeviceType) ? "Mobile" : request.DeviceType.Trim(),
            DeviceBrand = request.DeviceBrand?.Trim(),
            DeviceModel = request.DeviceModel?.Trim(),
            OperatingSystem = request.OperatingSystem?.Trim(),
            BrowserName = request.BrowserName?.Trim(),
            ScreenResolution = request.ScreenResolution?.Trim(),
            UserLanguage = request.UserLanguage?.Trim(),
            TimeToVoteSeconds = request.TimeToVoteSeconds,
            UserAgent = request.UserAgent?.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            LocationDetected = request.Latitude.HasValue && request.Longitude.HasValue,
            VotedAt = DateTime.UtcNow
        };

        _context.DynamicContentVoteLogs.Add(voteLog);
        await _context.SaveChangesAsync();

        _logger.LogInformation("New vote recorded for Content {ContentId}, Question {QuestionId}, Option {OptionKey} by {UserName} at Site {SiteId}",
            master.Id, question.Id, option.OptionKey, voteLog.UserName, voteLog.SiteId);

        // Recalculate results
        var currentResults = await GetDynamicContentResultsAsync(master.Id, request.SiteId, question.Id);

        // Broadcast to SignalR groups
        if (currentResults != null)
        {
            // Group names for new system
            await _hubContext.Clients.Group($"content_{master.Id}_{request.SiteId}")
                .SendAsync("ReceiveVoteUpdate", currentResults);

            await _hubContext.Clients.Group($"content_{master.Id}")
                .SendAsync("ReceiveVoteUpdate", currentResults);

            // Group names for legacy screen listeners
            await _hubContext.Clients.Group($"survey_{master.Id}_{request.SiteId}")
                .SendAsync("ReceiveVoteUpdate", currentResults);

            await _hubContext.Clients.Group($"survey_{master.Id}")
                .SendAsync("ReceiveVoteUpdate", currentResults);

            // Broadcast to all connected screens as global fallback
            await _hubContext.Clients.All
                .SendAsync("ReceiveVoteUpdate", currentResults);
        }

        bool isBrandIq = string.Equals(master.ContentType, "BrandIQ", StringComparison.OrdinalIgnoreCase);
        bool isBrandGenius = string.Equals(master.ContentType, "BrandGenius", StringComparison.OrdinalIgnoreCase);
        bool isQuizLike = isBrandIq || isBrandGenius;

        var correctOpt = question.Options.FirstOrDefault(o => o.IsCorrect || o.OptionKey.Equals(question.CorrectOptionKey, StringComparison.OrdinalIgnoreCase));
        bool? isCorrectChoice = isQuizLike ? (option.IsCorrect || option.OptionKey.Equals(question.CorrectOptionKey, StringComparison.OrdinalIgnoreCase)) : null;

        string returnMsg;
        if (isBrandGenius)
        {
            returnMsg = isCorrectChoice == true 
                ? "Luar Biasa! Tebakan Brand Anda TEPAT! 🎉 Selamat, Anda seorang Brand Genius!" 
                : "Tebakan Anda belum tepat! Jangan menyerah, perhatikan baik-baik logo di layar Videotron dan coba lagi! 🎯";
        }
        else if (isBrandIq)
        {
            returnMsg = isCorrectChoice == true 
                ? "Selamat! Jawaban kuis Anda BENAR! 🎉" 
                : $"Jawaban Anda belum tepat. Kunci jawaban: Opsi {correctOpt?.OptionKey} - {correctOpt?.OptionText}";
        }
        else
        {
            returnMsg = "Terima kasih! Suara Anda telah berhasil dicatat.";
        }

        return new DynamicContentVoteResponse
        {
            Success = true,
            Message = returnMsg,
            IsCorrect = isCorrectChoice,
            CorrectOptionKey = isQuizLike ? (correctOpt?.OptionKey ?? question.CorrectOptionKey) : null,
            CorrectOptionText = isQuizLike ? correctOpt?.OptionText : null,
            CurrentResults = currentResults
        };
    }

    public string GenerateQrCodeSvg(string targetUrl)
    {
        try
        {
            using var qrGenerator = new QRCodeGenerator();
            // ECCLevel.L (7%) minimizes module density — makes blocks much larger and easier to scan on screens/videotrons
            using var qrCodeData = qrGenerator.CreateQrCode(targetUrl, QRCodeGenerator.ECCLevel.L);
            var qrCode = new SvgQRCode(qrCodeData);
            var svg = qrCode.GetGraphic(10, "#000000", "#FFFFFF", true);
            if (!string.IsNullOrEmpty(svg) && svg.Contains(">"))
            {
                var insertIndex = svg.IndexOf('>') + 1;
                svg = svg.Insert(insertIndex, "<style>rect{fill:#FFFFFF !important;forced-color-adjust:none !important;}path{fill:#000000 !important;forced-color-adjust:none !important;}</style>");
            }
            return svg;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate QR Code SVG for {Url}", targetUrl);
            return string.Empty;
        }
    }
}

// Backward-compatibility class alias
public class SurveyService : DynamicContentService
{
    public SurveyService(
        AppDbContext context,
        IHubContext<DynamicContentHub> hubContext,
        ISecurityTokenService tokenService,
        ILogger<DynamicContentService> logger)
        : base(context, hubContext, tokenService, logger)
    {
    }
}
