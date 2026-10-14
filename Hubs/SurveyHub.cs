using Microsoft.AspNetCore.SignalR;
using CmsBillboard.DynamicContent.Models;

namespace CmsBillboard.DynamicContent.Hubs;

public class SurveyHub : Hub
{
    private readonly ILogger<SurveyHub> _logger;

    public SurveyHub(ILogger<SurveyHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinSurveyScreen(string surveyId, string? siteId = null)
    {
        var groupName = string.IsNullOrWhiteSpace(siteId) 
            ? $"survey_{surveyId}" 
            : $"survey_{surveyId}_{siteId}";

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"survey_{surveyId}");
        
        _logger.LogInformation("Connection {ConnectionId} joined group {GroupName}", Context.ConnectionId, groupName);
    }

    public async Task LeaveSurveyScreen(string surveyId, string? siteId = null)
    {
        var groupName = string.IsNullOrWhiteSpace(siteId) 
            ? $"survey_{surveyId}" 
            : $"survey_{surveyId}_{siteId}";

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Connection {ConnectionId} left group {GroupName}", Context.ConnectionId, groupName);
    }
}
