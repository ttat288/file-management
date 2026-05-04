using Amazon.S3;
using FileManagement.Api.Auth;
using FileManagement.Api.Realtime;
using FileManagement.Core.Interfaces;
using FileManagement.Core.Services;
using FileManagement.Data.Services;
using FileManagement.Data.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

static void LoadDotEnvFromParents(string fileName = ".env", int maxDepth = 4)
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    for (var i = 0; i <= maxDepth && dir != null; i++, dir = dir.Parent)
    {
        var path = Path.Combine(dir.FullName, fileName);
        if (!File.Exists(path))
            continue;

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
                continue;

            var key = line[..equalsIndex].Trim();
            var value = line[(equalsIndex + 1)..].Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(key))
                continue;

            // Don't override real environment variables.
            if (Environment.GetEnvironmentVariable(key) is not null)
                continue;

            Environment.SetEnvironmentVariable(key, value);
        }

        return;
    }
}

// Load Backend/FileManagement/.env for local development so ConnectionStrings__PostgreSQL works without manually exporting env vars.
LoadDotEnvFromParents();

static void NormalizePostgresConnectionStringEnv(string envKey = "ConnectionStrings__PostgreSQL")
{
    var raw = Environment.GetEnvironmentVariable(envKey);
    if (string.IsNullOrWhiteSpace(raw))
        return;

    var value = raw.Trim();

    if (value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return;

        var user = "";
        var password = "";
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            user = Uri.UnescapeDataString(parts.Length > 0 ? parts[0] : "");
            password = Uri.UnescapeDataString(parts.Length > 1 ? parts[1] : "");
        }

        var host = uri.Host;
        var port = uri.IsDefaultPort ? 5432 : uri.Port;
        var database = uri.AbsolutePath.TrimStart('/');

        // Many hosted providers (e.g. Render) require SSL.
        var normalized =
            $"Host={host};Port={port};Database={database};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true";

        Environment.SetEnvironmentVariable(envKey, normalized);
    }
}

NormalizePostgresConnectionStringEnv();

var builder = WebApplication.CreateBuilder(args);

// ========== Services ==========

// Add controllers
builder.Services.AddControllers();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FileManagement API", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    };

    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// ========== Auth (JWT) ==========
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
if (string.IsNullOrWhiteSpace(jwt.SigningKey))
    jwt.SigningKey = builder.Configuration["Jwt__SigningKey"] ?? "";

if (string.IsNullOrWhiteSpace(jwt.SigningKey))
    throw new InvalidOperationException("JWT signing key not configured (Jwt:SigningKey)");

builder.Services.AddSingleton<TokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // EventSource can't set Authorization header; allow `access_token` query only for SSE endpoint.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var path = ctx.HttpContext.Request.Path;
                if (path.StartsWithSegments("/api/events/stream"))
                {
                    var token = ctx.Request.Query["access_token"].ToString();
                    if (!string.IsNullOrWhiteSpace(token))
                        ctx.Token = token;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ========== AWS S3 Configuration ==========
// Force SigV4 for S3 (SigV2 presigned URLs look like ?AWSAccessKeyId=...&Signature=... and are rejected by many buckets/regions).
Amazon.AWSConfigsS3.UseSignatureVersion4 = true;

var awsS3Section = builder.Configuration.GetSection("AWSS3");
var awsAccessKey = awsS3Section["AccessKeyId"];
var awsSecretKey = awsS3Section["SecretAccessKey"];
var awsRegion = awsS3Section["Region"] ?? "us-east-1";

if (!string.IsNullOrEmpty(awsAccessKey) && !string.IsNullOrEmpty(awsSecretKey))
{
    var s3Config = new AmazonS3Config
    {
        RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(awsRegion),
        // Presigned URLs should use SigV4. Some buckets/regions reject SigV2 (often surfaced as 400/403 on PUT).
        SignatureVersion = "4"
    };
    var s3Client = new AmazonS3Client(awsAccessKey, awsSecretKey, s3Config);
    builder.Services.AddSingleton<IAmazonS3>(s3Client);
    builder.Services.AddScoped<IAWSS3Service, AWSS3Service>();
}
else
{
    builder.Services.AddScoped<IAWSS3Service, MissingS3Service>();
}

// ========== Dependency Injection ==========
// Services (Core)
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IFolderService, FolderService>();

// Repositories (Data)
builder.Services.AddScoped<IFileRepository, FileRepository>();
builder.Services.AddScoped<IFolderRepository, FolderRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

// Realtime (SSE)
builder.Services.AddSingleton<EventBus>();

// ========== Logging ==========
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
});

// ========== Build Application ==========
var app = builder.Build();

// ========== Middleware ==========
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
