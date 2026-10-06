using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ConferenceRoomBooking.Application.Interfaces;

namespace ConferenceRoomBooking.Api.Auth;

/// <summary>
/// Reads the authenticated identity from the current HTTP request's validated JWT claims.
/// This is the one place in the whole codebase allowed to touch IHttpContextAccessor for
/// this purpose - Application only ever sees it through ICurrentUserService.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(value, out var userId)
                ? userId
                : throw new InvalidOperationException("No authenticated user id found on the current request.");
        }
    }

    public bool IsAdmin => _httpContextAccessor.HttpContext?.User.IsInRole("Admin") ?? false;
}