using System.Reflection;
using System.Text;
using AspNetCoreRateLimit;
using ConferenceRoomBooking.Api.Auth;
using ConferenceRoomBooking.Api.Middleware;
using ConferenceRoomBooking.Application.Interfaces;
using ConferenceRoomBooking.Application.Services;
using ConferenceRoomBooking.Domain.Entities;
using ConferenceRoomBooking.Domain.Interfaces;
using ConferenceRoomBooking.Infrastructure;
using ConferenceRoomBooking.Infrastructure.Auth;
using ConferenceRoomBooking.Infrastructure.Repositories;
using ConferenceRoomBooking.Infrastructure.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ----------  Connection String  ----------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// ----------  DbContext (PostgreSQL)  ----------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ---------- MVC / API behaviour ----------
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Return a consistent problem+json payload for model-validation failures too,
        // matching the format produced by ExceptionHandlingMiddleware.
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(e => e.Key, e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray());

            var problem = new ValidationProblemDetails(errors)
            {
                Title = "Validation failed",
                Status = StatusCodes.Status400BadRequest
            };

            return new BadRequestObjectResult(problem);
        };
    });

// ---------- Swagger / OpenAPI ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Conference Room Booking API",
        Version = "v1",
        Description = "API для пошуку, бронювання конференц-залів та розрахунку вартості оренди."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Adds the "Authorize" button in Swagger UI - paste a JWT (just the token, "Bearer "
    // is added for you) after logging in via POST /api/auth/login to call protected
    // endpoints directly from the docs page.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the JWT token you got from POST /api/auth/login."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
                { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// ---------- CORS (adjust the allowed origins for your real front-end domains) ----------
const string CorsPolicyName = "DefaultCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ??
                             Array.Empty<string>();
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            // Sensible default for local development only.
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    });
});

// ---------- Authentication (JWT) / Authorization ----------
var jwtKey = builder.Configuration["Jwt:Key"]
             ?? throw new InvalidOperationException(
                 "Jwt:Key must be configured (see appsettings.json / user-secrets).");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            // No clock skew tolerance beyond the default 5 minutes is configured here -
            // the default is usually fine, but it's worth knowing it exists if tokens
            // ever seem to expire "too early" across servers with slightly different clocks.
        };
        // // Without this, the JWT bearer handler silently rewrites the "sub" claim to
        // // ClaimTypes.NameIdentifier on the way in - CurrentUserService reads
        // // JwtRegisteredClaimNames.Sub directly, so without this line it would always get null
        // // and every protected endpoint would throw InvalidOperationException on UserId access.
        options.MapInboundClaims = false;
    });

builder.Services.AddAuthorization();


// ---------- Rate limiting (basic API hardening against abuse) ----------
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// ---------- Dependency Injection: repositories are Scoped (one per HTTP request, matching AppDbContext's lifetime) ----------
builder.Services.AddScoped<IRoomRepository, PostgresRoomRepository>();
builder.Services.AddScoped<IBookingRepository, PostgresBookingRepository>();

builder.Services.AddScoped<IPricingCalculator, PricingCalculator>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IUserRepository, PostgresUserRepository>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenGenerator, JwtTokenGenerator>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

var app = builder.Build();

// Apply any pending EF Core migrations, then seed the starting data from the requirements
// (Зал А/B/C + services) if the database is empty. Both must run before the app starts
// handling requests, since the very first query would otherwise fail with "relation does not exist".
// checking is using because if a program run for tests this will ruin sqlite database
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();

    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    var roomRepository = scope.ServiceProvider.GetRequiredService<IRoomRepository>();
    await DataSeeder.SeedAsync(roomRepository);

    // Bootstraps the very first Admin account, since the public /api/auth/register
    // endpoint can only ever create Customers (see AuthService.RegisterAsync) - without
    // this, there would be no way to reach any [Authorize(Roles = "Admin")] endpoint at all.
    var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
    await DataSeeder.SeedAdminAsync(
        userRepository,
        passwordHasher,
        adminEmail: builder.Configuration["AdminSeed:Email"] ?? "admin@conferenceroombooking.local",
        adminPassword: builder.Configuration["AdminSeed:Password"]
                       ?? throw new InvalidOperationException("AdminSeed:Password must be configured."));
}


// ---------- Middleware pipeline ----------
app.UseMiddleware<ExceptionHandlingMiddleware>(); // First, so it wraps everything below.
app.UseIpRateLimiting();

// Swagger is exposed in all environments by default since this is a small demo API;
// gate it behind app.Environment.IsDevelopment() if the docs should be internal-only in production.
app.UseSwagger();
app.UseSwaggerUI(options => { options.SwaggerEndpoint("/swagger/v1/swagger.json", "Conference Room Booking API v1"); });

app.UseHttpsRedirection();
app.UseCors(CorsPolicyName);

// UseAuthentication MUST come before UseAuthorization - authentication figures out WHO
// is making the request (reads/validates the JWT into a ClaimsPrincipal); authorization
// then decides WHAT that identity is allowed to do ([Authorize], [Authorize(Roles=...)]).
// Getting this order backwards means every [Authorize] check runs against an anonymous
// user even with a perfectly valid token attached, silently producing 401s everywhere.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program
{
}