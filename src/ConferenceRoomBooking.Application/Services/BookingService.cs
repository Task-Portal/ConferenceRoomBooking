using System.Collections.Concurrent;
using ConferenceRoomBooking.Application.DTOs;
using ConferenceRoomBooking.Application.Interfaces;
using ConferenceRoomBooking.Domain.Entities;
using ConferenceRoomBooking.Domain.Exceptions;
using ConferenceRoomBooking.Domain.Interfaces;

namespace ConferenceRoomBooking.Application.Services;

public sealed class BookingService : IBookingService
{
    private readonly IRoomRepository _roomRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IPricingCalculator _pricingCalculator;

    // Guards the "check availability, then create" sequence per room so two concurrent
    // requests for the same slot cannot both pass the overlap check and double-book a room.
    // Keyed per-room (not a single global lock) so bookings for different rooms never block each other.
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> RoomLocks = new();

    public BookingService(IRoomRepository roomRepository, IBookingRepository bookingRepository, IPricingCalculator pricingCalculator)
    {
        _roomRepository = roomRepository;
        _bookingRepository = bookingRepository;
        _pricingCalculator = pricingCalculator;
    }

    public async Task<BookingDto> CreateBookingAsync(CreateBookingRequest request, CancellationToken cancellationToken = default)
    {
        if (request.EndTime <= request.StartTime)
        {
            throw new InvalidBookingRequestException("Booking end time must be after start time.");
        }

        var room = await _roomRepository.GetByIdAsync(request.RoomId, cancellationToken)
                    ?? throw new RoomNotFoundException(request.RoomId);

        var roomLock = RoomLocks.GetOrAdd(room.Id, _ => new SemaphoreSlim(1, 1));
        await roomLock.WaitAsync(cancellationToken);
        try
        {
            // Prevent double-booking: re-check overlap against every existing confirmed booking for this room.
            var existingBookings = await _bookingRepository.GetByRoomIdAsync(room.Id, cancellationToken);
            if (existingBookings.Any(b => b.OverlapsWith(request.StartTime, request.EndTime)))
            {
                throw new RoomNotAvailableException(room.Id, request.StartTime, request.EndTime);
            }

            var selectedServices = room.ResolveServices(request.SelectedServices);
            var pricing = _pricingCalculator.Calculate(room, request.StartTime, request.EndTime, selectedServices);

            var booking = new Booking(
                room.Id,
                request.StartTime,
                request.EndTime,
                request.SelectedServices,
                pricing.TotalPrice,
                request.CustomerName);

            await _bookingRepository.AddAsync(booking, cancellationToken);

            return ToDto(booking, room, pricing);
        }
        finally
        {
            roomLock.Release();
        }
    }

    public async Task<BookingDto> GetBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId, cancellationToken)
                      ?? throw new BookingNotFoundException(bookingId);

        var room = await _roomRepository.GetByIdAsync(booking.RoomId, cancellationToken)
                   ?? throw new RoomNotFoundException(booking.RoomId);

        var pricing = _pricingCalculator.Calculate(room, booking.StartTime, booking.EndTime, room.ResolveServices(booking.SelectedServiceNames));
        return ToDto(booking, room, pricing);
    }

    public async Task<IReadOnlyCollection<BookingDto>> GetAllBookingsAsync(CancellationToken cancellationToken = default)
    {
        var bookings = await _bookingRepository.GetAllAsync(cancellationToken);
        var result = new List<BookingDto>();

        foreach (var booking in bookings)
        {
            var room = await _roomRepository.GetByIdAsync(booking.RoomId, cancellationToken);
            if (room is null)
            {
                continue; // Room was hard-removed from data; skip gracefully instead of failing the whole list.
            }

            var services = booking.SelectedServiceNames
                .Select(name => room.Services.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
                .Where(s => s is not null)
                .Select(s => s!)
                .ToList();

            var pricing = _pricingCalculator.Calculate(room, booking.StartTime, booking.EndTime, services);
            result.Add(ToDto(booking, room, pricing));
        }

        return result;
    }

    public async Task CancelBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId, cancellationToken)
                      ?? throw new BookingNotFoundException(bookingId);

        booking.Cancel();
        await _bookingRepository.UpdateAsync(booking, cancellationToken);
    }

    private static BookingDto ToDto(Booking booking, Room room, PricingResult pricing) => new(
        booking.Id,
        booking.RoomId,
        room.Name,
        booking.StartTime,
        booking.EndTime,
        booking.SelectedServiceNames,
        pricing.ServicesCost,
        pricing.RoomRentalCost,
        pricing.TotalPrice,
        booking.CustomerName,
        booking.Status.ToString(),
        pricing.Breakdown);
}
