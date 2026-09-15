using ConferenceRoomBooking.Domain.Entities;

namespace ConferenceRoomBooking.Domain.Interfaces;

public interface IUserRepository
{
    // TODO 7: Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    // Needed for both "is this email already registered" (on register) and "look up the
    // user to check their password" (on login). Same "return null, don't throw" contract
    // as IRoomRepository/IBookingRepository - the Application layer decides what a missing
    // user means in each case (register: proceed; login: "invalid credentials").
    Task<User?>GetByEmailAsync(string email, CancellationToken token = default(CancellationToken));

    // TODO 8: Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken token = default(CancellationToken));
}