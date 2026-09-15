using ConferenceRoomBooking.Domain.Entities;

namespace ConferenceRoomBooking.Application.Interfaces;

/// <summary>
/// Application doesn't know or care HOW a token is produced (JWT today, something else
/// tomorrow) - only that a signed-in User can be turned into a string the client sends
/// back on subsequent requests. The actual implementation (Infrastructure/JwtTokenGenerator)
/// deals with signing keys/configuration, same separation as IPricingCalculator.
/// </summary>
public interface ITokenGenerator
{
    string GenerateToken(User user);
}