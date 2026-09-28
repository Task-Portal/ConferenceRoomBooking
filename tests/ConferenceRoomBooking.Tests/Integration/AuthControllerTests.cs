using System.Net;
using System.Net.Http.Json;
using ConferenceRoomBooking.Application.DTOs;
using ConferenceRoomBooking.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ConferenceRoomBooking.Tests.Integration;

public class AuthControllerTests : IntegrationTestBase
{
    private const string TestPassword = "CorrectHorse123!";

    public AuthControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Register_NewEmail_ReturnsTokenAndCustomerRole()
    {
        RegisterRequest request = new RegisterRequest { Email = "Mixed.Case@Test.local", Password = TestPassword };
        var result = await Client.PostAsJsonAsync("api/auth/register", request);
        var body = await result.Content.ReadAsStringAsync();
        Assert.True(result.StatusCode == HttpStatusCode.OK, $"Unexpected {result.StatusCode}: {body}");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        var response = await result.Content.ReadFromJsonAsync<AuthResponse>();


        Assert.NotNull(response);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
        Assert.Equal("Customer", response?.Role);
        Assert.Equal("mixed.case@test.local", response?.Email);
    }

    [Fact]
    public async Task Register_SameEmailTwice_ReturnsConflict()
    {
        var registerRequest = new RegisterRequest { Email = "dup@test.local", Password = TestPassword };
        var response = await Client.PostAsJsonAsync("api/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var registerRequest1 = new RegisterRequest { Email = "DUP@test.local", Password = TestPassword };
        var response1 = await Client.PostAsJsonAsync("api/auth/register", registerRequest1);
        Assert.Equal(HttpStatusCode.Conflict, response1.StatusCode);


        var readRespnse = await response1.Content.ReadFromJsonAsync<ProblemResponse>(CaseInsensitiveJson);
        Assert.Equal("Email already registered", readRespnse?.Title);
    }

    [Theory]
    [InlineData("not-an-email", "CorrectHorse123!")]
    [InlineData("valid@test.local", "short")]
    [InlineData("", "CorrectHorse123!")]
    public async Task Register_InvalidInput_ReturnsBadRequest(string email, string password)
    {
        var rr = new RegisterRequest { Email = email, Password = password };

        var response = await Client.PostAsJsonAsync("api/auth/register", rr);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);


        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal("Validation failed", body?.Title);
    }

    [Fact]
    public async Task Login_AfterRegister_WorksRegardlessOfEmailCase()
    {
      
        var request = new RegisterRequest { Email = "login.case@test.local", Password = TestPassword };
        var result = await Client.PostAsJsonAsync("api/auth/register", request);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        var loginResponse = await Client.PostAsJsonAsync("api/auth/login", request);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var result1 = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(result1);
        Assert.False(string.IsNullOrWhiteSpace(result1.Token));
    }

    [Fact]
    public async Task Login_AsSeededAdmin_ReturnsAdminRole()
    {
        
        var request = new RegisterRequest
            { Email = CustomWebApplicationFactory.AdminEmail, Password = CustomWebApplicationFactory.AdminPassword };
        var response = await Client.PostAsJsonAsync("api/auth/login", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal("Admin", result?.Role);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
       
        var request = new  RegisterRequest { Email = "wrongpass@test.local", Password = TestPassword };
        var result = await Client.PostAsJsonAsync("api/auth/login", request);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        var body = await result.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("Unauthorized", body?.Title);
        
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
       
        var request = new RegisterRequest { Email = "new_email@com.ua", Password = TestPassword };
        var result = await Client.PostAsJsonAsync("api/auth/login", request);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        var body = await result.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.Equal("Unauthorized", body?.Title);
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_AreIndistinguishable()
    {

        var request = new RegisterRequest { Email = CustomWebApplicationFactory.AdminEmail, Password = TestPassword };
        var result = await Client.PostAsJsonAsync("api/auth/login", request);
        var body = await result.Content.ReadFromJsonAsync<ProblemResponse>();
        
        var request1 = new RegisterRequest{Email = "new_email@com.ua", Password = TestPassword};
        var result1 = await Client.PostAsJsonAsync("api/auth/login", request1);
        var body1 = await result1.Content.ReadFromJsonAsync<ProblemResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body1);
        Assert.Equal(result.StatusCode, result1.StatusCode);
        Assert.Equal(body.Title, body1.Title);
        Assert.Equal(body.Detail,  body1.Detail);
        
    }
}