using LandaDoc.ServiceDefaults;
using LandaDoc.Shared.Data;
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
builder.AddLandaDocLogging("Payment");

builder.Services.AddDbContext<PaymentDbContext>(o =>
    o.UseNpgsql(PostgresConnectionString.Normalize(builder.Configuration.GetConnectionString("Conx"))));

StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

builder.Services.AddScoped<DoctorBalances>();

builder.Services.Configure<MokoAfrikaOptions>(builder.Configuration.GetSection("MokoAfrika"));
// The sandbox (https://sandbox.gofreshpay.com/api/v1/gateway) while testing; an empty setting
// (e.g. a blank Render variable) means FreshPay's production gateway
var mokoBaseUrl = builder.Configuration["MokoAfrika:BaseUrl"];
builder.Services.AddHttpClient<IMokoAfrikaClient, MokoAfrikaClient>(c =>
    c.BaseAddress = new Uri(string.IsNullOrWhiteSpace(mokoBaseUrl) ? "https://api.gofreshpay.com/api/v1/gateway" : mokoBaseUrl));
// Records mobile money outcomes (callbacks and reconciliation), and catches lost callbacks
builder.Services.AddScoped<MobileMoneySettlement>();
builder.Services.AddHostedService<MobileMoneyReconciliationService>();

var jwtSecret = builder.Configuration.GetJwtSecret();
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
    x.AddConsumer<InsuranceReviewTimedOutConsumer>()
        .Endpoint(e => e.Name = "payment-insurance-review-timed-out");
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"], ushort.Parse(builder.Configuration["RabbitMq:Port"] ?? "5672"), builder.Configuration["RabbitMq:VirtualHost"] ?? "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"]);
            h.Password(builder.Configuration["RabbitMq:Password"]);
            if (builder.Configuration.GetValue<bool>("RabbitMq:UseSsl"))
                h.UseSsl(s =>
                {
                    s.ServerName = builder.Configuration["RabbitMq:Host"];
                    s.Protocol = System.Security.Authentication.SslProtocols.Tls12;
                });
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

// Enums as strings, like the other services — the web apps send e.g. "Provider": "Stripe"
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseLandaDocRequestLogging();

// Which FreshPay account this instance talks to, so a wrong deploy setting shows in the logs
// ("Merchant is not active or not found" means the merchant id isn't one FreshPay knows).
// The id is shortened and the secret only counted, never written out.
{
    static string Shorten(string? value) => string.IsNullOrWhiteSpace(value) ? "(not set)"
        : value.Length <= 10 ? $"({value.Length} characters)" : $"{value[..4]}…{value[^4..]} ({value.Length} characters)";
    var moko = app.Configuration.GetSection("MokoAfrika");
    app.Logger.LogInformation("Mobile money: FreshPay gateway {Gateway}, merchant {MerchantId}, secret {Secret}",
        string.IsNullOrWhiteSpace(mokoBaseUrl) ? "https://api.gofreshpay.com/api/v1/gateway (default)" : mokoBaseUrl,
        Shorten(moko["MerchantId"]),
        string.IsNullOrWhiteSpace(moko["MerchantSecret"]) ? "(not set)" : $"set ({moko["MerchantSecret"]!.Length} characters)");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Off by default outside Development so a deploy never alters the schema
// unless the host opts in (Database__MigrateOnStartup=true).
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
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
