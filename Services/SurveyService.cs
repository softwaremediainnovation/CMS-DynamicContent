using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using CmsBillboard.DynamicContent.Data;
using CmsBillboard.DynamicContent.Hubs;
using CmsBillboard.DynamicContent.Models;

namespace CmsBillboard.DynamicContent.Services;

public class SurveyService : ISurveyService
{
    private readonly AppDbContext _context;
    private readonly IHubContext<SurveyHub> _hubContext;
    private readonly ISecurityTokenService _tokenService;
    private readonly ILogger<SurveyService> _logger;

    public SurveyService(
        AppDbContext context,
        IHubContext<SurveyHub> hubContext,
        ISecurityTokenService tokenService,
        ILogger<SurveyService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<SurveyMaster?> GetSurveyMasterAsync(string surveyId)
    {
        return await _context.SurveyMasters
            .Include(m => m.Questions.OrderBy(q => q.SortOrder))
                .ThenInclude(q => q.Options.OrderBy(o => o.SortOrder))
            .FirstOrDefaultAsync(m => m.Id == surveyId && m.IsActive);
    }

    public async Task<SurveyResultDto?> GetSurveyResultsAsync(string surveyId, string? siteId = null, int? questionId = null)
    {
        var master = await _context.SurveyMasters
            .Include(m => m.Questions.OrderBy(q => q.SortOrder))
                .ThenInclude(q => q.Options.OrderBy(o => o.SortOrder))
            .FirstOrDefaultAsync(m => m.Id == surveyId);

        if (master == null)
            return null;

        var question = questionId.HasValue
            ? master.Questions.FirstOrDefault(q => q.Id == questionId.Value)
            : master.Questions.FirstOrDefault();

        if (question == null)
            return null;

        var options = question.Options.OrderBy(o => o.SortOrder).ToList();
        int totalVotes = options.Sum(o => o.VoteCount);

        var resultDto = new SurveyResultDto
        {
            SurveyId = master.Id,
            ClientName = master.ClientName,
            CampaignTitle = master.CampaignTitle,
            AdHeadline = master.AdHeadline,
            AdSubheadline = master.AdSubheadline,
            BannerImageUrl = master.BannerImageUrl,
            BrandLogoUrl = master.BrandLogoUrl,
            ThemeColorHex = master.ThemeColorHex,
            SiteId = siteId ?? "Nasional",
            TotalVotes = totalVotes,
            QuestionId = question.Id,
            QuestionText = question.QuestionText,
            QuestionType = question.QuestionType,
            QuestionImageUrl = question.QuestionImageUrl,
            Options = options.Select(o => new SurveyOptionResultDto
            {
                OptionId = o.Id,
                OptionKey = o.OptionKey,
                OptionText = o.OptionText,
                OptionImageUrl = o.OptionImageUrl,
                ColorHex = o.ColorHex,
                VoteCount = o.VoteCount,
                Percentage = totalVotes > 0 ? Math.Round((double)o.VoteCount / totalVotes * 100.0, 1) : 0
            }).ToList()
        };

        return resultDto;
    }

    public async Task<bool> HasUserVotedAsync(string surveyId, string deviceFingerprint, int? questionId = null)
    {
        if (string.IsNullOrWhiteSpace(deviceFingerprint))
            return false;

        var query = _context.SurveyVoteLogs
            .Where(v => v.SurveyMasterId == surveyId && v.DeviceFingerprint == deviceFingerprint);

        if (questionId.HasValue && questionId.Value > 0)
        {
            query = query.Where(v => v.QuestionId == questionId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task<SurveyVoteResponse> SubmitVoteAsync(SurveyVoteRequest request, string? clientIp)
    {
        // 1. If Token is provided, validate cryptographic signature and expiry
        if (!string.IsNullOrWhiteSpace(request.Token))
        {
            var (isValid, payload, errorMessage) = _tokenService.ValidateVoteToken(request.Token);
            if (!isValid || payload == null)
            {
                return new SurveyVoteResponse
                {
                    Success = false,
                    Message = errorMessage ?? "Token keamanan tidak sah atau sudah kedaluwarsa!"
                };
            }

            // Enforce genuine parameters from cryptographically signed token
            request.SurveyId = payload.SurveyId;
            request.SiteId = payload.SiteId;
        }

        // 2. Load Survey Master & Questions
        var master = await _context.SurveyMasters
            .Include(m => m.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(m => m.Id == request.SurveyId && m.IsActive);

        if (master == null)
        {
            return new SurveyVoteResponse
            {
                Success = false,
                Message = "Survey tidak ditemukan atau sudah tidak aktif."
            };
        }

        var question = request.QuestionId > 0
            ? master.Questions.FirstOrDefault(q => q.Id == request.QuestionId)
            : master.Questions.FirstOrDefault();

        if (question == null)
        {
            return new SurveyVoteResponse
            {
                Success = false,
                Message = "Pertanyaan survey tidak ditemukan."
            };
        }

        request.QuestionId = question.Id;

        // 3. Rule: Strict 1-Time Participation per device/session
        if (await HasUserVotedAsync(request.SurveyId, request.DeviceFingerprint, question.Id))
        {
            return new SurveyVoteResponse
            {
                Success = false,
                Message = "Anda sudah memberikan suara dalam survey ini. Partisipasi dibatasi 1 kali per perangkat.",
                CurrentResults = await GetSurveyResultsAsync(request.SurveyId, request.SiteId, question.Id)
            };
        }

        var option = question.Options.FirstOrDefault(o => o.OptionKey.Equals(request.OptionKey, StringComparison.OrdinalIgnoreCase));
        if (option == null)
        {
            return new SurveyVoteResponse
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

        // Record vote log with audience device telemetry
        var voteLog = new SurveyVoteLog
        {
            SurveyMasterId = master.Id,
            QuestionId = question.Id,
            OptionKey = option.OptionKey,
            TextResponse = request.TextResponse,
            UserName = string.IsNullOrWhiteSpace(request.UserName) ? "Anonim" : request.UserName.Trim(),
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
            VotedAt = DateTime.UtcNow
        };

        _context.SurveyVoteLogs.Add(voteLog);
        await _context.SaveChangesAsync();

        _logger.LogInformation("New vote recorded for Survey {SurveyId}, Question {QuestionId}, Option {OptionKey} by {UserName} at Site {SiteId}",
            master.Id, question.Id, option.OptionKey, voteLog.UserName, voteLog.SiteId);

        // Recalculate results
        var currentResults = await GetSurveyResultsAsync(master.Id, request.SiteId, question.Id);

        // Broadcast to SignalR groups
        if (currentResults != null)
        {
            await _hubContext.Clients.Group($"survey_{master.Id}_{request.SiteId}")
                .SendAsync("ReceiveVoteUpdate", currentResults);

            await _hubContext.Clients.Group($"survey_{master.Id}")
                .SendAsync("ReceiveVoteUpdate", currentResults);
        }

        return new SurveyVoteResponse
        {
            Success = true,
            Message = "Terima kasih! Suara Anda telah berhasil dicatat.",
            CurrentResults = currentResults
        };
    }

    public string GenerateQrCodeSvg(string targetUrl)
    {
        try
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(targetUrl, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new SvgQRCode(qrCodeData);
            return qrCode.GetGraphic(10);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate QR Code SVG for {Url}", targetUrl);
            return string.Empty;
        }
    }
}
