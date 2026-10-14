using Microsoft.AspNetCore.SignalR;
using CmsBillboard.DynamicContent.Models;

namespace CmsBillboard.DynamicContent.Hubs;

public class DynamicContentHub : Hub
{
    private readonly ILogger<DynamicContentHub> _logger;

    public DynamicContentHub(ILogger<DynamicContentHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinContentScreen(string contentId, string? siteId = null)
    {
        var groupName = string.IsNullOrWhiteSpace(siteId) 
            ? $"content_{contentId}" 
            : $"content_{contentId}_{siteId}";

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"content_{contentId}");
        
        // Also add to legacy survey group so existing screens receive broadcasts
        await Groups.AddToGroupAsync(Context.ConnectionId, $"survey_{contentId}");
        if (!string.IsNullOrWhiteSpace(siteId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"survey_{contentId}_{siteId}");
        }
        
        _logger.LogInformation("Connection {ConnectionId} joined group {GroupName}", Context.ConnectionId, groupName);
    }

    // Backward compatibility for existing front-end screens
    public async Task JoinSurveyScreen(string surveyId, string? siteId = null)
    {
        await JoinContentScreen(surveyId, siteId);
    }

    public async Task JoinDynamicContentGroup(string contentId)
    {
        await JoinContentScreen(contentId, null);
    }

    public async Task JoinDynamicContentSiteGroup(string contentId, string siteId)
    {
        await JoinContentScreen(contentId, siteId);
    }

    public async Task LeaveContentScreen(string contentId, string? siteId = null)
    {
        var groupName = string.IsNullOrWhiteSpace(siteId) 
            ? $"content_{contentId}" 
            : $"content_{contentId}_{siteId}";

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"survey_{contentId}");
        _logger.LogInformation("Connection {ConnectionId} left group {GroupName}", Context.ConnectionId, groupName);
    }

    public async Task LeaveSurveyScreen(string surveyId, string? siteId = null)
    {
        await LeaveContentScreen(surveyId, siteId);
    }
}

// Backward-compatibility class alias
public class SurveyHub : DynamicContentHub
{
    public SurveyHub(ILogger<DynamicContentHub> logger) : base(logger)
    {
    }
}
