using ConferenceRoomBooking.Application.DTOs;
using ConferenceRoomBooking.Application.Interfaces;
using ConferenceRoomBooking.Domain.Entities;
using ConferenceRoomBooking.Domain.Exceptions;
using ConferenceRoomBooking.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace ConferenceRoomBooking.Application.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ITokenGenerator _tokenGenerator;

    public AuthService(IUserRepository userRepository, IPasswordHasher<User> passwordHasher,
        ITokenGenerator tokenGenerator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user is not null)
        {
            throw new EmailAlreadyRegisteredException(request.Email);
        }

        var hash = _passwordHasher.HashPassword(null!, request.Password);

        var newUser = new User(request.Email, hash, UserRole.Customer);

        await _userRepository.AddAsync(newUser, cancellationToken);

        return new AuthResponse(_tokenGenerator.GenerateToken(newUser), newUser.Email, newUser.Role.ToString());
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {   
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null)
        {
            throw new InvalidCredentialsException();
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            return new AuthResponse(_tokenGenerator.GenerateToken(user), user.Email, user.Role.ToString());
        }

        throw new InvalidCredentialsException();
    }
}