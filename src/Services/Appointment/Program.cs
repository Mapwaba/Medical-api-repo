using System.Text;
using LandaDoc.Appointment.Consumers;
using LandaDoc.Appointment.Data;
using LandaDoc.Appointment.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Seq(Environment.GetEnvironmentVariable("SEQ_URL") ?? "http://localhost:5341")
    .Enrich.WithProperty("Service", "Appointment")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

builder.Services.AddDbContext<AppointmentDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Conx")));

builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddHostedService<PendingPaymentExpiryService>();

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
    // Register any consumers this service has
    // (Appointment service consumes PaymentFailed to auto-cancel, PaymentCompleted to confirm)
    // Explicit endpoint names — Notification also has consumer classes named
    // PaymentCompletedConsumer/PaymentFailedConsumer for the same events; without explicit
    // names MassTransit's default queue-per-class-name would collide the two services
    // onto one shared queue and turn intended fan-out into competing consumers.
    x.AddConsumer<PaymentFailedConsumer>()
        .Endpoint(e => e.Name = "appointment-payment-failed");
    x.AddConsumer<PaymentCompletedConsumer>()
        .Endpoint(e => e.Name = "appointment-payment-completed");
    // Same explicit-naming reasoning as above — Notification is a plausible future
    // consumer of these same family events under identically-named classes.
    x.AddConsumer<FamilyLinkAcceptedConsumer>()
        .Endpoint(e => e.Name = "appointment-family-link-accepted");
    x.AddConsumer<FamilyLinkRevokedConsumer>()
        .Endpoint(e => e.Name = "appointment-family-link-revoked");
    x.AddConsumer<DependentAddedConsumer>()
        .Endpoint(e => e.Name = "appointment-dependent-added");
    x.AddConsumer<DependentRemovedConsumer>()
        .Endpoint(e => e.Name = "appointment-dependent-removed");
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

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider
        .GetRequiredService<AppointmentDbContext>()
        .Database.MigrateAsync();
}
else
{
    app.UseExceptionHandler();
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
