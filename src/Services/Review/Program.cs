using LandaDoc.Shared.Data;
using System.Text;
using LandaDoc.Review.Data;
using LandaDoc.Review.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ReviewDbContext>(o =>
    o.UseNpgsql(PostgresConnectionString.Normalize(builder.Configuration.GetConnectionString("Conx"))));

builder.Services.AddHttpClient<IAppointmentServiceClient, AppointmentServiceClient>(c =>
    c.BaseAddress = new Uri(builder.Configuration["Services:AppointmentBaseUrl"] ?? "http://localhost:5001"));

builder.Services.AddScoped<IReviewService, ReviewService>();

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

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
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
        .GetRequiredService<ReviewDbContext>()
        .Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
