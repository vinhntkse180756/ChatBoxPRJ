using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Presentation.SignalR;

[Authorize(Roles = "Admin,Lecturer")]
public sealed class DocumentHub : Hub;
