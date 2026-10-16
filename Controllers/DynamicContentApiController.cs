using Microsoft.AspNetCore.Mvc;
using CmsBillboard.DynamicContent.Models;
using CmsBillboard.DynamicContent.Services;

namespace CmsBillboard.DynamicContent.Controllers;

[ApiController]
[Route("api/dynamic-content")]
[Route("api/survey")]
public class DynamicContentApiController : ControllerBase
{
    private readonly IDynamicContentService _contentService;
    private readonly ISecurityTokenService _tokenService;

    public DynamicContentApiController(IDynamicContentService contentService, ISecurityTokenService tokenService)
    {
        _contentService = contentService;
        _tokenService = tokenService;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetContent(string id)
    {
        var content = await _contentService.GetDynamicContentMasterAsync(id);
        if (content == null)
            return NotFound(new { message = $"Dynamic Content with ID '{id}' was not found." });

        return Ok(content);
    }

    [HttpGet("{id}/results")]
    public async Task<IActionResult> GetResults(string id, [FromQuery] string? site = null, [FromQuery] int? questionId = null)
    {
        var results = await _contentService.GetDynamicContentResultsAsync(id, site, questionId);
        if (results == null)
            return NotFound(new { message = $"Dynamic Content results for '{id}' were not found." });

        return Ok(results);
    }

    [HttpGet("check-voted")]
    public async Task<IActionResult> CheckVoted([FromQuery] string surveyId, [FromQuery] string? contentId, [FromQuery] string fingerprint, [FromQuery] int? questionId = null)
    {
        var targetId = !string.IsNullOrWhiteSpace(contentId) ? contentId : surveyId;
        var hasVoted = await _contentService.HasUserVotedAsync(targetId, fingerprint, questionId);
        return Ok(new { hasVoted });
    }

    [HttpGet("check-wait-time")]
    public async Task<IActionResult> CheckWaitTime([FromQuery] string? id, [FromQuery] string? surveyId, [FromQuery] string? contentId, [FromQuery] string fingerprint)
    {
        var targetId = !string.IsNullOrWhiteSpace(id) ? id : (!string.IsNullOrWhiteSpace(contentId) ? contentId : surveyId);
        if (string.IsNullOrWhiteSpace(targetId))
            return BadRequest(new { message = "Content ID / Survey ID wajib disertakan." });

        var response = await _contentService.CheckWaitTimeAsync(targetId, fingerprint ?? string.Empty);
        return Ok(response);
    }

    [HttpPost("vote")]
    public async Task<IActionResult> SubmitVote([FromBody] DynamicContentVoteRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var response = await _contentService.SubmitVoteAsync(request, ip);

        if (!response.Success)
        {
            if (response.RemainingSeconds > 0)
            {
                return StatusCode(429, response);
            }
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpGet("token/validate")]
    public IActionResult ValidateToken([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { isValid = false, message = "Parameter token wajib diisi." });

        var (isValid, payload, errorMessage) = _tokenService.ValidateVoteToken(token);
        if (!isValid || payload == null)
        {
            return BadRequest(new { isValid = false, message = errorMessage ?? "Token tidak valid atau kedaluwarsa." });
        }

        return Ok(new
        {
            isValid = true,
            contentId = payload.ContentId,
            siteId = payload.SiteId,
            mode = payload.Mode,
            createdAtUnix = payload.CreatedAtUnix,
            expiresAtUnix = payload.ExpiresAtUnix,
            customParams = payload.CustomParams
        });
    }

    [HttpPost("token/generate")]
    public IActionResult GenerateToken([FromBody] TokenGenerateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ContentId) || string.IsNullOrWhiteSpace(request.SiteId))
            return BadRequest(new { message = "ContentId dan SiteId wajib diisi." });

        var token = _tokenService.GenerateToken(
            contentId: request.ContentId,
            siteId: request.SiteId,
            mode: request.Mode ?? "interactive",
            customParams: request.CustomParams,
            expiryMinutes: request.ExpiryMinutes ?? 60
        );

        return Ok(new { token });
    }

    [HttpGet("qr")]
    public IActionResult GetQr([FromQuery] string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest(new { message = "URL is required." });

        var svg = _contentService.GenerateQrCodeSvg(url);
        return Content(svg, "image/svg+xml");
    }
}

public class TokenGenerateRequest
{
    public string ContentId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string? Mode { get; set; } = "interactive";
    public int? ExpiryMinutes { get; set; } = 60;
    public Dictionary<string, object>? CustomParams { get; set; }
}
