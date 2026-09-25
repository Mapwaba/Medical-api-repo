using System.Text;
using LandaDoc.Payment.Consumers;
using LandaDoc.Payment.Data;
using LandaDoc.Payment.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PaymentDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("payment")));

StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

builder.Services.Configure<MokoAfrikaOptions>(builder.Configuration.GetSection("MokoAfrika"));
builder.Services.AddHttpClient<IMokoAfrikaClient, MokoAfrikaClient>(c =>
    c.BaseAddress = new Uri(builder.Configuration["MokoAfrika:BaseUrl"]!));

var jwtSecret = builder.Configuration["Jwt:Secret"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "landadoc";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtIssuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddMassTransit(x =>
{
    // Explicit endpoint name — MassTransit derives queue names from the consumer's
    // short type name by default, and Payment/Notification/Availability all have a
    // class named BookingCreatedConsumer, which would otherwise collide onto one
    // shared queue and turn intended fan-out into competing consumers.
    x.AddConsumer<BookingCreatedConsumer>()
        .Endpoint(e => e.Name = "payment-booking-created");
    // Explicit endpoint name — Identity and Search also have a class named
    // DoctorApprovedConsumer; without this it collides onto their shared queue.
    x.AddConsumer<DoctorApprovedConsumer>()
        .Endpoint(e => e.Name = "payment-doctor-approved");
    // Explicit endpoint name — Notification also has a class named BookingExpiredConsumer.
    x.AddConsumer<BookingExpiredConsumer>()
        .Endpoint(e => e.Name = "payment-booking-expired");
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"], ushort.Parse(builder.Configuration["RabbitMq:Port"] ?? "5672"), "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"]);
            h.Password(builder.Configuration["RabbitMq:Password"]);
        });
        cfg.ConfigureEndpoints(ctx);
    });
});

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5299", "http://localhost:5003", "http://localhost:5500" };

builder.Services.AddCors(o => o.AddPolicy("Frontend", p => p
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider
        .GetRequiredService<PaymentDbContext>()
        .Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
