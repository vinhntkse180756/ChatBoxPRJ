using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChatBoxPRJ.Hubs;

[Authorize(Roles = "Admin,Lecturer")]
public sealed class DocumentHub : Hub;
