using ConferenceRoomBooking.Domain.Entities;
using Xunit;

namespace ConferenceRoomBooking.Tests;

public class UserEntityTests
{
    [Fact]
    public void CheckUserRole()
    {
        var user = new User("a@a.com", "sdfkjsdfkj", UserRole.Admin);
        

        Assert.Equal(UserRole.Admin, user.Role);
    }
}