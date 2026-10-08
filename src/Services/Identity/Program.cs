using LandaDoc.ServiceDefaults;
using LandaDoc.Shared.Data;
using System.Text;
using LandaDoc.Identity.Consumers;
using LandaDoc.Identity.Data;
using LandaDoc.Identity.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.AddLandaDocLogging("Identity");

builder.Services.AddDbContext<IdentityDbContext>(o =>
    o.UseNpgsql(PostgresConnectionString.Normalize(builder.Configuration.GetConnectionString("Conx"))));

builder.Services.AddSingleton<IPasswordHasher, PasswordService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFamilyService, FamilyService>();

// The Patients page asks the Appointment service which patients a doctor has seen
builder.Services.AddHttpClient<IAppointmentPatientsClient, AppointmentPatientsClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Services:AppointmentBaseUrl"] ?? "http://localhost:5001");
    // Render's free services can take ~60s to wake; fail with 503 rather than hang forever
    c.Timeout = TimeSpan.FromSeconds(70);
});

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
    // Explicit endpoint names — Search also has consumers named DoctorApprovedConsumer/
    // DoctorSuspendedConsumer; without this, MassTransit's default naming (derived from
    // the consumer's short type name) would collide both services onto the same shared
    // queue and turn intended fan-out into competing consumers.
    x.AddConsumer<DoctorApprovedConsumer>()
        .Endpoint(e => e.Name = "identity-doctor-approved");
    x.AddConsumer<DoctorSuspendedConsumer>()
        .Endpoint(e => e.Name = "identity-doctor-suspended");
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost",
            ushort.Parse(builder.Configuration["RabbitMq:Port"] ?? "5672"), builder.Configuration["RabbitMq:VirtualHost"] ?? "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
            if (builder.Configuration.GetValue<bool>("RabbitMq:UseSsl"))
                h.UseSsl(s =>
                {
                    s.ServerName = builder.Configuration["RabbitMq:Host"];
                    s.Protocol = System.Security.Authentication.SslProtocols.Tls12;
                });
        });
        cfg.ConfigureEndpoints(context);
    });
});

// Blunt brute-force/credential-stuffing on the endpoints that take a password or mint tokens
// (the actions marked [EnableRateLimiting("auth")] in AuthController): 10 tries a minute per
// client IP. Counting per IP matters — one shared counter would let a handful of ordinary users
// lock everybody out — and everything else (profile reads on every page load, admin calls) is
// left unlimited.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Repeated refusals for one address in the logs usually mean someone is guessing passwords
    o.OnRejected = (ctx, _) =>
    {
        ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RateLimit")
            .LogWarning("Too many attempts from {ClientIp} on {Path}: request refused",
                ClientAddress.Of(ctx.HttpContext), ctx.HttpContext.Request.Path.Value);
        return ValueTask.CompletedTask;
    };
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(ClientAddress.Of(ctx), _ => new FixedWindowRateLimiterOptions
    {
        Window = TimeSpan.FromMinutes(1),
        PermitLimit = 10,
        QueueLimit = 0,
    }));
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
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseLandaDocRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler();
    app.UseHsts();
}

// Off by default outside Development so a deploy never alters the schema
// unless the host opts in (Database__MigrateOnStartup=true).
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider
        .GetRequiredService<IdentityDbContext>()
        .Database.MigrateAsync();
}

using (var scope = app.Services.CreateScope())
{
    await AdminPromoter.PromoteAsync(
        scope.ServiceProvider.GetRequiredService<IdentityDbContext>(),
        scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
        app.Configuration["Admin:Email"],
        app.Configuration["Admin:Password"],
        app.Logger);
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
