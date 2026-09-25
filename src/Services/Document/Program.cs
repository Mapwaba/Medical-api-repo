using LandaDoc.Shared.Data;
using System.Text;
using Amazon.S3;
using LandaDoc.Document.Consumers;
using LandaDoc.Document.Data;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<DocumentDbContext>(o =>
    o.UseNpgsql(PostgresConnectionString.Normalize(builder.Configuration.GetConnectionString("Conx"))));

builder.Services.AddSingleton<IAmazonS3>(_ =>
{
    var s3Config = new AmazonS3Config
    {
        ServiceURL = builder.Configuration["S3:ServiceUrl"],
        ForcePathStyle = true
    };
    return new AmazonS3Client(
        builder.Configuration["S3:AccessKey"],
        builder.Configuration["S3:SecretKey"],
        s3Config);
});

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
    x.AddConsumer<AppointmentCompletedConsumer>();
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
    using var scope = app.Services.CreateScope();

    // MinIO doesn't auto-create buckets — ensure the dev bucket exists on startup.
    var s3 = scope.ServiceProvider.GetRequiredService<Amazon.S3.IAmazonS3>();
    var bucketName = builder.Configuration["S3:BucketName"]!;
    if (!await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(s3, bucketName))
        await s3.PutBucketAsync(bucketName);
}

// Off by default outside Development so a deploy never alters the schema
// unless the host opts in (Database__MigrateOnStartup=true).
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider
        .GetRequiredService<DocumentDbContext>()
        .Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
