using Microsoft.AspNetCore.SignalR;

namespace my_comment_api.Hubs;

public class CommentHub : Hub
{

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);

    }

}