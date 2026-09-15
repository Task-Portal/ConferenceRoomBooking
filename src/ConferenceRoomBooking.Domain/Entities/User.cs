namespace ConferenceRoomBooking.Domain.Entities;

public enum UserRole
{
    Customer = 0,
    Admin = 1
}

/// <summary>
/// A registered account. Like Room and Booking, this entity protects its own invariants
/// and never exposes a way to set the password hash directly from outside except through
/// the constructor/ChangePasswordHash - nothing in Application or Api should ever see or
/// construct a plaintext password on this type.
/// </summary>
public class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;

   
    public string PasswordHash { get; private set; } = string.Empty;
   
    public UserRole Role { get; private set; }
    private User() { } // for EF Core

    public User(string email, string passwordHash, UserRole role)
    {
       
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be null or whitespace.", nameof(email));
        }

        Email = email.Trim().ToLowerInvariant();
        
        
        
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password cannot be null or whitespace.", nameof(passwordHash));
        }
        PasswordHash = passwordHash;

        Id = Guid.NewGuid();
        // TODO 5: assign Email, PasswordHash, Role from the (validated/normalized) parameters.
        Role = role;
    }

   
    public void ChangePasswordHash(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            throw new ArgumentException("Password cannot be null or whitespace.", nameof(newPasswordHash));
        }
        PasswordHash = newPasswordHash;
    }
    
}