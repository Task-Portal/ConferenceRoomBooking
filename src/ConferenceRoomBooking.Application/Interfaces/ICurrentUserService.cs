namespace ConferenceRoomBooking.Application.Interfaces;

/// <summary>
/// Abstracts "who is making this request" away from HttpContext, which Application must
/// never reference directly (it would pull a web-framework dependency into business logic
/// that should also be usable/testable outside a web request - e.g. from a future console
/// job, or directly from a unit test with a fake implementation of this interface).
/// The real implementation (Api/Auth/CurrentUserService) reads it from the validated JWT's
/// claims; nothing in Application needs to know that detail.
/// </summary>
public interface ICurrentUserService
{
    Guid UserId { get; }
    bool IsAdmin { get; }
}