using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.PostgreSql;
using Marketplace.API.Features.Extras;
using Marketplace.API.Features.Legacy;
using Marketplace.API.Features.Products;
using Marketplace.Application.Mapping;
using Marketplace.API.Validation;
using Marketplace.Application.Validation;
using Marketplace.Core.Entities;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Infrastructure.Seed;
using Marketplace.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
var isTesting = builder.Environment.IsEnvironment("Testing");

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Marketplace API",
        Version = "v1",
        Description = "Marketplace backend API."
    });
    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });
    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddAutoMapper(typeof(MappingProfile));
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(MappingProfile).Assembly));
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<WishlistCreateRequestValidator>();

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("Connection string Default is missing");
    opt.UseNpgsql(cs);
});

builder.Services.AddIdentityCore<AppUser>(opt =>
{
    opt.User.RequireUniqueEmail = true;
    opt.Password.RequiredLength = 8;
    opt.Password.RequireDigit = true;
    opt.Password.RequireNonAlphanumeric = true;
    opt.Password.RequireLowercase = true;
    opt.Password.RequireUppercase = true;
})
.AddRoles<IdentityRole<Guid>>()
.AddEntityFrameworkStores<AppDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<LiqPayOptions>(builder.Configuration.GetSection("LiqPay"));
builder.Services.Configure<MinioOptions>(builder.Configuration.GetSection("Minio"));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<SmsUaOptions>(builder.Configuration.GetSection("SmsUa"));
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ILiqPayService, LiqPayService>();
builder.Services.AddScoped<ISmtpClientWrapper, SmtpClientWrapper>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ISmsService, SmsUaService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IExternalAuthService, GoogleExternalAuthService>();
builder.Services.AddHttpClient();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? throw new InvalidOperationException("Jwt options are missing");
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

if (!isTesting)
{
    builder.Services.AddHangfire(config =>
    {
        var cs = builder.Configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("Connection string Default is missing");
        config.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(cs));
    });
    builder.Services.AddHangfireServer();
}

builder.Services.AddSingleton<IStorageService>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MinioOptions>>();
    var baseUrl = builder.Configuration["AppBaseUrl"] ?? "http://localhost:5000";
    return new StorageService(options, baseUrl);
});

builder.Services.AddCors(opt =>
{
    opt.AddPolicy("default", p =>
    {
        var origins = builder.Configuration.GetSection("Frontend:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        if (origins.Length == 0) p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
        else p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors("default");
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return canConnect
        ? Results.Ok(new { status = "ok", database = "postgresql", utc = DateTime.UtcNow })
        : Results.Problem("Database connection failed", statusCode: 500);
}).WithTags("System").AllowAnonymous();

app.MapGet("/", () => Results.Ok(new
{
    service = "Marketplace API",
    swagger = "/swagger",
    hangfire = "/hangfire",
    health = "/health"
})).AllowAnonymous();

app.MapLegacyEndpoints();
app.MapProductsEndpoints();
app.MapExtrasEndpoints();
if (!isTesting)
{
    app.UseHangfireDashboard("/hangfire");
}

if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var rm = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        try
        {
            await DbSeeder.SeedAsync(db, um, rm);
            Log.Information("Seeding completed successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Seeding failed: {Message}", ex.Message);
        }
    }
}

app.Run();

public partial class Program { }
