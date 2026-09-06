using System.Reflection;
using AspNetCoreRateLimit;
using ConferenceRoomBooking.Api.Middleware;
using ConferenceRoomBooking.Application.Interfaces;
using ConferenceRoomBooking.Application.Services;
using ConferenceRoomBooking.Domain.Interfaces;
using ConferenceRoomBooking.Infrastructure;
using ConferenceRoomBooking.Infrastructure.Repositories;
using ConferenceRoomBooking.Infrastructure.Seed;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
});

// ---------- CORS (adjust the allowed origins for your real front-end domains) ----------
const string CorsPolicyName = "DefaultCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
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
builder.Services.AddScoped<IReportService, ReportService>();

var app = builder.Build();

// Apply any pending EF Core migrations, then seed the starting data from the requirements
// (Зал А/B/C + services) if the database is empty. Both must run before the app starts
// handling requests, since the very first query would otherwise fail with "relation does not exist".
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    var roomRepository = scope.ServiceProvider.GetRequiredService<IRoomRepository>();
    await DataSeeder.SeedAsync(roomRepository);
}

// ---------- Middleware pipeline ----------
app.UseMiddleware<ExceptionHandlingMiddleware>(); // First, so it wraps everything below.
app.UseIpRateLimiting();

// Swagger is exposed in all environments by default since this is a small demo API;
// gate it behind app.Environment.IsDevelopment() if the docs should be internal-only in production.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Conference Room Booking API v1");
});

app.UseHttpsRedirection();
app.UseCors(CorsPolicyName);
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
