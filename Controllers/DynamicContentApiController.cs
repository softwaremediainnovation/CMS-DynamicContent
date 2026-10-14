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

    public DynamicContentApiController(IDynamicContentService contentService)
    {
        _contentService = contentService;
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

    [HttpPost("vote")]
    public async Task<IActionResult> SubmitVote([FromBody] DynamicContentVoteRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var response = await _contentService.SubmitVoteAsync(request, ip);

        if (!response.Success)
            return BadRequest(response);

        return Ok(response);
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
