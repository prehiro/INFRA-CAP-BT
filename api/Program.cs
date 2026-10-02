using System.Text;
using Api.Data;
using Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---- config ----
var conn = builder.Configuration.GetConnectionString("Default")
           ?? "Server=192.168.4.3,1433;Database=InternalApp;User Id=sa;Password=YourStr0ngP@ssword!;TrustServerCertificate=True;Encrypt=False";

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(conn));

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions
{
    Key = "dev-only-signing-key-change-me-in-production-please-32b",
    Issuer = "InternalApp.Api",
    Audience = "InternalApp.Web"
};
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<DynamicRecordService>();
builder.Services.AddScoped<DynamicSchemaService>();
builder.Services.AddScoped<SeedService>();
builder.Services.AddScoped<LogbookNumberService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
{
    Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header
}));
builder.Services.AddCors(o =>
{
    var allowed = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
    o.AddDefaultPolicy(p =>
    {
        if (allowed.Length > 0)
        {
            p.WithOrigins(allowed).AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            // Same-origin deployment (IIS: SPA at /INFRA-CAP, API at /INFRA-CAP-api) needs no CORS.
            // Kept permissive only so the Swagger UI on the API host stays usable.
            p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    });
});

var app = builder.Build();

// ---- create database + seed on first run ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    var seed = scope.ServiceProvider.GetRequiredService<SeedService>();
    var adminUser = app.Configuration["Seed:AdminUsername"] ?? "admin";
    var adminPass = app.Configuration["Seed:AdminPassword"] ?? "Admin@123";
    await seed.SeedAsync(adminUser, adminPass);
    app.Logger.LogInformation("Database ready. Admin login: {User} / {Pass}", adminUser, adminPass);
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Domain exceptions carry messages meant for the caller. Anything else is a bug and
// must never leak a stack trace or framework internals to the browser.
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;

    ctx.Response.StatusCode = ex switch
    {
        NotFoundException => StatusCodes.Status404NotFound,
        RecordValidationException => StatusCodes.Status400BadRequest,
        UnauthorizedAccessException => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError
    };
    ctx.Response.ContentType = "application/json";

    var log = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Api.Exception");
    if (ex is NotFoundException or RecordValidationException)
        log.LogInformation("Handled {Type}: {Message}", ex.GetType().Name, ex.Message);
    else
        log.LogError(ex, "Unhandled exception on {Path}", ctx.Request.Path);

    // Branches intentionally return different shapes (plain message vs message+errors),
    // so they cannot share an anonymous type.
    object body = ex switch
    {
        NotFoundException => new { message = ex.Message },
        RecordValidationException v => (object)new { message = "Validation failed", errors = v.Errors },
        _ => new { message = "Terjadi kesalahan pada server." }
    };
    await ctx.Response.WriteAsJsonAsync(body);
}));

app.MapControllers();

app.Run();
