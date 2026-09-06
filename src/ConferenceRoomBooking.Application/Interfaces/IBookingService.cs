using ConferenceRoomBooking.Application.DTOs;

namespace ConferenceRoomBooking.Application.Interfaces;

public interface IBookingService
{
    Task<BookingDto> CreateBookingAsync(CreateBookingRequest request, CancellationToken cancellationToken = default);

    Task<BookingDto> GetBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<BookingDto>> GetAllBookingsAsync(CancellationToken cancellationToken = default);

    Task CancelBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);
}
