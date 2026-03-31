using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Reflection;
using TgerCamera.Models;
using TgerCamera.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddDbContext<TgerCameraContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Distributed Cache - AddDistributedMemoryCache for local development/testing
// For production, replace with: builder.Services.AddStackExchangeRedisCache(options => { ... })
builder.Services.AddDistributedMemoryCache(options =>
{
    // Optional: Configure memory cache size if needed
    options.SizeLimit = 104_857_600; // 100 MB
});

// AutoMapper
builder.Services.AddAutoMapper(cfg => { }, typeof(Program).Assembly);

// Token service
builder.Services.AddSingleton<TgerCamera.Services.ITokenService, TgerCamera.Services.TokenService>();

// Cart service
builder.Services.AddScoped<TgerCamera.Services.ICartService, TgerCamera.Services.CartService>();

// Order service
builder.Services.AddScoped<TgerCamera.Services.IOrderService, TgerCamera.Services.OrderService>();

// Configure cookie policy for SessionId cookie
builder.Services.Configure<Microsoft.AspNetCore.Builder.CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
});

// JWT Authentication
// Note: Jwt settings are read from configuration (appsettings.json) but should be
// overridden by environment variables or user-secrets in production. Example env var names: Jwt__Key, Jwt__Issuer, Jwt__Audience
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? ""))
        };
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

// Request logging middleware - should be before exception handling to log all requests
app.UseMiddleware<RequestLoggingMiddleware>();

// Global exception handling middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCookiePolicy();

// Authentication must be before Authorization
app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
