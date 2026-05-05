using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Reflection;
using TgerCamera.Models;
using TgerCamera.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký services vào container.

builder.Services.AddControllers();

builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddDbContext<TgerCameraContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// Distributed Cache - dùng AddDistributedMemoryCache cho local development/testing
// Với production, thay bằng: builder.Services.AddStackExchangeRedisCache(options => { ... })
builder.Services.AddDistributedMemoryCache(options =>
{
    // Tuỳ chọn: cấu hình kích thước memory cache nếu cần
    options.SizeLimit = 104_857_600; // 100 MB
});

// AutoMapper
builder.Services.AddAutoMapper(cfg => { }, typeof(Program).Assembly);

// HttpClient cho các external API call
builder.Services.AddHttpClient();

// Token service - xử lý việc tạo JWT và refresh token
builder.Services.AddScoped<TgerCamera.Services.ITokenService, TgerCamera.Services.TokenService>();

// Refresh token service - quản lý lưu trữ và validation của refresh token
builder.Services.AddScoped<TgerCamera.Services.IRefreshTokenService, TgerCamera.Services.RefreshTokenService>();

// Auth service - điều phối toàn bộ authentication operations
builder.Services.AddScoped<TgerCamera.Services.IAuthService, TgerCamera.Services.AuthService>();

// Cart service
builder.Services.AddScoped<TgerCamera.Services.ICartService, TgerCamera.Services.CartService>();

// Product service
builder.Services.AddScoped<TgerCamera.Services.IProductService, TgerCamera.Services.ProductService>();

// Order service
builder.Services.AddScoped<TgerCamera.Services.IOrderService, TgerCamera.Services.OrderService>();

// Cấu hình cookie policy cho cookie SessionId
builder.Services.Configure<Microsoft.AspNetCore.Builder.CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
});

// JWT Authentication
// Lưu ý: Jwt settings được đọc từ configuration (appsettings.json) nhưng nên được
// override bằng environment variables hoặc user-secrets trong production. Ví dụ tên env var: Jwt__Key, Jwt__Issuer, Jwt__Audience
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
                System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "")),
            ClockSkew = TimeSpan.Zero
        };
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });




var app = builder.Build();

// Chỉ tự động apply migrations trong môi trường development hoặc khi được bật rõ ràng.
var applyMigrationsOnStartup = app.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");

if (applyMigrationsOnStartup)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<TgerCameraContext>();
    dbContext.Database.Migrate();
}

// Cấu hình HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

// Request logging middleware - nên đặt trước exception handling để log toàn bộ requests
app.UseMiddleware<RequestLoggingMiddleware>();

// Global exception handling middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCookiePolicy();

// Authentication phải được đặt trước Authorization
app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
