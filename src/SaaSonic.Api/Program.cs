using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using SaaSonic.Application;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Infrastructure;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ── Application & Infrastructure ──────────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ── Authentication ─────────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "SaaSonic",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "SaaSonic",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    })
    .AddCookie("ExternalCookie", options =>
    {
        options.Cookie.Name = "saasonic_ext_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddGoogle(options =>
    {
        options.SignInScheme = "ExternalCookie";
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? string.Empty;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
        options.SaveTokens = true;
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.CallbackPath = "/api/auth/external/google/callback";
    })
    .AddFacebook(options =>
    {
        options.SignInScheme = "ExternalCookie";
        options.AppId = builder.Configuration["Authentication:Facebook:AppId"] ?? string.Empty;
        options.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"] ?? string.Empty;
        options.SaveTokens = true;
        options.Fields.Add("email");
        options.Fields.Add("name");
        options.Fields.Add("picture");
        options.CallbackPath = "/api/auth/external/facebook/callback";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SystemAdmin", p => p.RequireRole(SaaSonic.Domain.Constants.RoleNames.SystemAdmin));
});

// ── Controllers & OpenAPI ─────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((doc, ctx, ct) =>
    {
        doc.Components ??= new();
        doc.Components.SecuritySchemes = new Dictionary<string, Microsoft.OpenApi.IOpenApiSecurityScheme>
        {
            ["Bearer"] = new Microsoft.OpenApi.OpenApiSecurityScheme
            {
                Type = Microsoft.OpenApi.SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Enter your JWT access token."
            }
        };
        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, ctx, ct) =>
    {
        var hasAuthorize = ctx.Description.ActionDescriptor.EndpointMetadata
            .OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Any();

        if (hasAuthorize)
        {
            operation.Security =
            [
                new()
                {
                    [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer")] = []
                }
            ];
        }

        return Task.CompletedTask;
    });
});

var app = builder.Build();

// ── Exception handling ────────────────────────────────────────────────────────
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    var ex = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var (status, title) = ex switch
    {
        ValidationException => (StatusCodes.Status400BadRequest, "Validation Error"),
        UnauthorizedException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
        ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
        _ => (StatusCodes.Status500InternalServerError, "Internal Server Error"),
    };

    ctx.Response.StatusCode = status;
    ctx.Response.ContentType = "application/problem+json";
    var problem = new { title, status, detail = ex?.Message };
    await ctx.Response.WriteAsync(JsonSerializer.Serialize(problem));
}));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseMigrationsEndPoint();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/scalar/"));

app.Run(builder.Configuration["App:FrontendUrl"] ?? "http://localhost:5000");
