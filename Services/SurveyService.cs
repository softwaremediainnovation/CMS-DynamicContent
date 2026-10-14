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

    public async Task<SurveyItem?> GetSurveyAsync(string surveyId)
    {
        return await _context.Surveys
            .Include(s => s.Options.OrderBy(o => o.SortOrder))
            .FirstOrDefaultAsync(s => s.Id == surveyId && s.IsActive);
    }

    public async Task<SurveyResultDto?> GetSurveyResultsAsync(string surveyId, string? siteId = null)
    {
        var survey = await _context.Surveys
            .Include(s => s.Options.OrderBy(o => o.SortOrder))
            .FirstOrDefaultAsync(s => s.Id == surveyId);

        if (survey == null)
            return null;

        var options = survey.Options.OrderBy(o => o.SortOrder).ToList();
        int totalVotes = options.Sum(o => o.VoteCount);

        var resultDto = new SurveyResultDto
        {
            SurveyId = survey.Id,
            Title = survey.Title,
            Question = survey.Question,
            BrandName = survey.BrandName,
            AdHeadline = survey.AdHeadline,
            AdSubheadline = survey.AdSubheadline,
            AdImageUrl = survey.AdImageUrl,
            SiteId = siteId ?? "Nasional",
            TotalVotes = totalVotes,
            Options = options.Select(o => new SurveyOptionResultDto
            {
                OptionKey = o.OptionKey,
                OptionText = o.OptionText,
                ColorHex = o.ColorHex,
                VoteCount = o.VoteCount,
                Percentage = totalVotes > 0 ? Math.Round((double)o.VoteCount / totalVotes * 100.0, 1) : 0
            }).ToList()
        };

        return resultDto;
    }

    public async Task<bool> HasUserVotedAsync(string surveyId, string deviceFingerprint)
    {
        if (string.IsNullOrWhiteSpace(deviceFingerprint))
            return false;

        return await _context.SurveyVoteLogs
            .AnyAsync(v => v.SurveyId == surveyId && v.DeviceFingerprint == deviceFingerprint);
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

        var survey = await _context.Surveys
            .Include(s => s.Options)
            .FirstOrDefaultAsync(s => s.Id == request.SurveyId && s.IsActive);

        if (survey == null)
        {
            return new SurveyVoteResponse
            {
                Success = false,
                Message = "Survey tidak ditemukan atau sudah tidak aktif."
            };
        }

        // Rule: Many user can participate ONLY ONE TIME per device/session
        if (await HasUserVotedAsync(request.SurveyId, request.DeviceFingerprint))
        {
            return new SurveyVoteResponse
            {
                Success = false,
                Message = "Anda sudah memberikan suara dalam survey ini. Partisipasi dibatasi 1 kali per perangkat.",
                CurrentResults = await GetSurveyResultsAsync(request.SurveyId, request.SiteId)
            };
        }

        var option = survey.Options.FirstOrDefault(o => o.OptionKey.Equals(request.OptionKey, StringComparison.OrdinalIgnoreCase));
        if (option == null)
        {
            return new SurveyVoteResponse
            {
                Success = false,
                Message = $"Pilihan {request.OptionKey} tidak valid."
            };
        }

        // Increment vote count
        option.VoteCount += 1;

        // Record vote log
        var voteLog = new SurveyVoteLog
        {
            SurveyId = request.SurveyId,
            OptionKey = option.OptionKey,
            UserName = string.IsNullOrWhiteSpace(request.UserName) ? "Anonim" : request.UserName.Trim(),
            SiteId = string.IsNullOrWhiteSpace(request.SiteId) ? "Default" : request.SiteId.Trim(),
            DeviceFingerprint = request.DeviceFingerprint.Trim(),
            IpAddress = clientIp,
            VotedAt = DateTime.UtcNow
        };

        _context.SurveyVoteLogs.Add(voteLog);
        await _context.SaveChangesAsync();

        _logger.LogInformation("New vote recorded for Survey {SurveyId}, Option {OptionKey} by {UserName} at Site {SiteId}",
            request.SurveyId, option.OptionKey, voteLog.UserName, voteLog.SiteId);

        // Recalculate results
        var currentResults = await GetSurveyResultsAsync(request.SurveyId, request.SiteId);

        // Broadcast to SignalR groups
        if (currentResults != null)
        {
            // Broadcast to site-specific screen
            await _hubContext.Clients.Group($"survey_{request.SurveyId}_{request.SiteId}")
                .SendAsync("ReceiveVoteUpdate", currentResults);

            // Broadcast to general survey screen
            await _hubContext.Clients.Group($"survey_{request.SurveyId}")
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
