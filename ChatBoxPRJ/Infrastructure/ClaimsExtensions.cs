using System.Security.Claims;
using ChatBoxPRJ.Business.DTOs;

namespace ChatBoxPRJ.Infrastructure;

public static class ClaimsExtensions
{
    public static Guid UserId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static UserRole UserRole(this ClaimsPrincipal user) => Enum.Parse<UserRole>(user.FindFirstValue(ClaimTypes.Role)!);
}
