using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChatBoxPRJ.Pages.SignalR;

[Authorize(Roles = "Admin,Lecturer")]
public sealed class DocumentHub : Hub;
