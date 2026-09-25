using LandaDoc.Shared.Data;
using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using LandaDoc.Notification.Consumers;
using LandaDoc.Notification.Data;
using LandaDoc.Notification.Hubs;
using LandaDoc.Notification.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<NotificationDbContext>(o =>
    o.UseNpgsql(PostgresConnectionString.Normalize(builder.Configuration.GetConnectionString("Conx"))));

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddScoped<INotificationQueryService, NotificationQueryService>();
builder.Services.AddScoped<INotificationPublisher, NotificationPublisher>();
builder.Services.AddSignalR();

builder.Services.AddHangfire(h => h
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(
        PostgresConnectionString.Normalize(builder.Configuration.GetConnectionString("Conx")))));
builder.Services.AddHangfireServer();

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
        // Browser SignalR clients can't set the Authorization header on the WebSocket
        // upgrade request — the standard pattern is to pass the token as a query string
        // parameter for hub requests instead.
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
                {
                    ctx.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddMassTransit(x =>
{
    // Explicit endpoint names — see Payment/Program.cs for why this is required.
    x.AddConsumer<BookingCreatedConsumer>()
        .Endpoint(e => e.Name = "notification-booking-created");
    x.AddConsumer<UserRegisteredConsumer>()
        .Endpoint(e => e.Name = "notification-user-registered");
    x.AddConsumer<PaymentCompletedConsumer>()
        .Endpoint(e => e.Name = "notification-payment-completed");
    x.AddConsumer<PaymentFailedConsumer>()
        .Endpoint(e => e.Name = "notification-payment-failed");
    x.AddConsumer<BookingExpiredConsumer>()
        .Endpoint(e => e.Name = "notification-booking-expired");
    x.AddConsumer<AppointmentCompletedConsumer>()
        .Endpoint(e => e.Name = "notification-appointment-completed");
    x.AddConsumer<AppointmentRescheduledConsumer>()
        .Endpoint(e => e.Name = "notification-appointment-rescheduled");
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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

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
        .GetRequiredService<NotificationDbContext>()
        .Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
