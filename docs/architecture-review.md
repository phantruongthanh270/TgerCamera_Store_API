# 🏗️ KIỂM TOÁN KIẾN TRÚC - TgerCamera Store API

## I. PHÂN TÍCH KIẾN TRÚC LỚP

### 1. Presentation Layer (Presentation Layer)

**Vị Trí:** `TgerCamera/Controllers/`

**Thành Phần:**

- `AuthController.cs` - Các điểm cuối Authentication
- `CartController.cs` - Quản lý Cart (khách & Authentication)
- `ProductController.cs` - Danh mục sản phẩm
- `OrdersController.cs` - Xử lý đơn hàng
- `BrandController.cs`, `CategoryController.cs` - Dữ liệu chính
- `RentalProductController.cs` - Sản phẩm cho thuê

**Mẫu:** RESTful API với `[ApiController]` + `[Route("api/[controller]")]`

```csharp
// Ví Dụ: OrdersController
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    [HttpPost("checkout")]
    [Authorize]  // Tùy chọn - hỗ trợ thanh toán khách qua SessionId
    public async Task<IActionResult> Checkout([FromBody] CheckoutDto request) { }

    [HttpGet("my-orders")]
    [Authorize]  // Cần thiết - người dùng phải được Authentication
    public async Task<IActionResult> GetMyOrders() { }

    [HttpPut("{id}/cancel")]
    [Authorize]  // Người dùng có thể hủy đơn hàng của họ
    public async Task<IActionResult> CancelOrder(int id) { }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin")]  // Chỉ quản trị viên - kiểm soát truy cập dựa trên vai trò
    public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateStatusDto request) { }
}
```

**✅ Lợi Ích:**

- Tách biệt rõ ràng các vấn đề
- Một trách nhiệm cho mỗi bộ điều khiển
- Định tuyến dựa trên thuộc tính
- Authentication mô hình tích hợp

---

### 2. Business Logic Layer (Business Logic Layer)

**Vị Trí:** `TgerCamera/Services/`

**Các Dịch Vụ Chính:**

#### 2.1 ICartService & CartService

**Mục Đích:** Quản lý giỏ khách (dựa trên cache) và người dùng (dựa trên database)

```csharp
public interface ICartService
{
    // Hoạt động giỏ khách (cache)
    Task<CartDto?> GetGuestCartAsync(string sessionId);
    Task SaveGuestCartAsync(string sessionId, CartDto cart);
    Task ClearGuestCartAsync(string sessionId);

    // Hoạt động giỏ người dùng (database)
    Task<CartDto?> GetUserCartAsync(int userId);
    Task<CartDto> GetOrCreateUserCartAsync(int userId);

    // Thêm/Xóa mục
    Task<CartDto> AddItemToCacheOrDbAsync(string? sessionId, int? userId, int productId, int quantity);
    Task<CartDto> UpdateItemAsync(int? sessionId, int? userId, int productId, int quantity);
    Task<CartDto> RemoveItemAsync(int? sessionId, int? userId, int productId);

    // Logic Hợp Nhất (khách → người dùng khi đăng nhập)
    Task MergeGuestCartToUserAsync(int userId, string sessionId);
}
```

**Chiến Lược Lưu Trữ Kép:**

```
Người Dùng Khách            Người Dùng Authentication
    ↓                            ↓
Không Tác Động DB          Bền Vững Database
    ↓                            ↓
SessionId cookie           JWT token + UserId
    ↓                            ↓
Distributed Cache          SQL Server
(TTL 24 giờ)               (bền vững)
    ↓                            ↓
Tự Động Dọn Dẹp            Xóa Thủ Công
```

**Authentication Kho Hàng Khi Hợp Nhất:**

```csharp
public async Task MergeGuestCartToUserAsync(int userId, string sessionId)
{
    // 1. Lấy giỏ khách từ cache
    var guestCart = await GetGuestCartAsync(sessionId);

    // 2. Authentication từng mục
    foreach (var item in guestCart.Items)
    {
        var product = await _context.Products.FindAsync(item.ProductId);

        // Kiểm tra xóa mềm
        if (product?.IsDeleted == true) continue;

        // Authentication kho hàng
        if (product.StockQuantity < item.Quantity)
        {
            item.Quantity = Math.Max(0, product.StockQuantity);
        }
    }

    // 3. Hợp nhất vào giỏ người dùng (tính tổng số lượng nếu trùng)
    // 4. Xóa cache khách
}
```

#### 2.2 ITokenService & TokenService

**Mục Đích:** Tạo token JWT và quản lý yêu cầu

```csharp
public interface ITokenService
{
    string CreateToken(User user);
}

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;

    public string CreateToken(User user)
    {
        // 1. Khóa bí mật từ cấu hình
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]));

        // 2. Tạo yêu cầu
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };

        // 3. Thêm thông tin Authentication
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // 4. Tạo token với hết hạn
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                int.Parse(_config["Jwt:ExpireMinutes"] ?? "60")),
            signingCredentials: creds
        );

        // 5. Tuần tự hóa và trả về
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

**Yêu Cầu Token:**

- `sub` (Chủ đề): User ID
- `email`: Email người dùng để nhận dạng
- `role`: Vai trò người dùng (Khách/Quản Trị) để Authorization

#### 2.3 IOrderService & OrderService

**Mục Đích:** Xử lý đơn hàng, Authentication và quản lý trạng thái

```csharp
public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(int? userId, string? sessionId, CheckoutDto request);
    Task<IEnumerable<OrderDto>> GetUserOrdersAsync(int userId);
    Task<OrderDto?> GetOrderByIdAsync(int orderId, int? userId);
    Task<bool> CancelOrderAsync(int orderId, int userId);
    Task<bool> UpdateOrderStatusAsync(int orderId, string newStatus);
}
```

**Quy Trình Tạo Đơn Hàng:**

```
1. Lấy giỏ (khách hoặc người dùng)
2. Authentication:
   - Giỏ không trống
   - Tất cả sản phẩm vẫn có trong kho
   - Địa chỉ giao hàng tồn tại và thuộc về người dùng (nếu Authentication)
3. Gọi sp_CreateOrder (Stored Procedure) - Giao dịch nguyên tử:
   - Tạo bản ghi Đơn Hàng
   - Tạo Mục OrderItems (từ giỏ)
   - Giảm kho hàng sản phẩm
   - Tạo bản ghi Thanh Toán
4. Xóa giỏ
5. Trả về OrderDto với OrderId
```

**Authentication Kho Hàng:**

```csharp
var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);

if (product == null || product.IsDeleted == true)
    throw new InvalidOperationException("Sản phẩm không tìm thấy hoặc đã bị xóa");

if (product.StockQuantity < item.Quantity)
    throw new InvalidOperationException(
        $"Kho hàng không đủ cho: {product.Name} (Chỉ còn {product.StockQuantity})");

// Giảm kho (trong stored procedure)
product.StockQuantity -= item.Quantity;
```

---

### 3. Data Mapping Layer (Data Mapping Layer)

**Vị Trí:** `TgerCamera/Dtos/` và `TgerCamera/Mapping/`

#### 3.1 Mẫu DTO

**Mục Đích:** Tách biệt hợp đồng API khỏi mô hình database

```csharp
// DTOs Yêu Cầu
public class CheckoutDto
{
    [Required]
    public int ShippingAddressId { get; set; }

    [Required]
    [StringLength(50)]
    public string PaymentMethod { get; set; }  // COD, VNPAY, v.v.
}

// DTOs Phản Hồi
public class OrderDto
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public decimal TotalPrice { get; set; }
    public string Status { get; set; }  // Pending, Processing, Shipped, Delivered, Cancelled

    // DTOs Lồng Nhau
    public List<OrderItemDto> Items { get; set; }  // Được ánh xạ từ OrderItems
}

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }

    // DTOs Lồng Nhau
    public BrandDto Brand { get; set; }
    public CategoryDto Category { get; set; }
    public List<ProductSpecificationDto> Specifications { get; set; }

    // Dữ Liệu Làm Phẳng
    public string MainImageUrl { get; set; }  // Được tính toán trong ánh xạ
}
```

#### 3.2 Cấu Hình AutoMapper

```csharp
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Sản Phẩm → ProductDto (với logic tùy chỉnh)
        CreateMap<Product, ProductDto>()
            .ForMember(dest => dest.Brand, opt => opt.MapFrom(src => src.Brand))
            .ForMember(dest => dest.Category, opt => opt.MapFrom(src => src.Category))
            .ForMember(dest => dest.Condition, opt => opt.MapFrom(src => src.Condition))
            .ForMember(dest => dest.Specifications,
                opt => opt.MapFrom(src => src.ProductSpecifications))
            .ForMember(dest => dest.MainImageUrl, opt => opt.Ignore())  // Đặt trong AfterMap
            .AfterMap((src, dest) =>
            {
                // Logic tùy chỉnh: Lấy hình ảnh chính hoặc hình ảnh đầu tiên
                var mainImage = src.ProductImages?
                    .FirstOrDefault(pi => pi.IsMain == true)?.ImageUrl;
                dest.MainImageUrl = mainImage ?? src.ProductImages?.FirstOrDefault()?.ImageUrl;
            });

        // Đơn Hàng → OrderDto (ánh xạ OrderItems thành Items)
        CreateMap<Order, OrderDto>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.OrderItems));

        // Ánh Xạ Đơn Giản (không có logic tùy chỉnh)
        CreateMap<Category, CategoryDto>();
        CreateMap<Brand, BrandDto>();
        CreateMap<User, UserDto>();
    }
}
```

**Lợi Ích:**

- ✅ Tách biệt giữa API và mô hình DB
- ✅ Logic ánh xạ tùy chỉnh (AfterMap)
- ✅ Ánh xạ an toàn về loại
- ✅ Nguồn duy nhất về sự thực

---

### 4. Data Access Layer (Data Access Layer)

**Vị Trị:** `TgerCamera/Models/TgerCameraContext.cs`

#### 4.1 Thiết Lập DbContext

```csharp
public partial class TgerCameraContext : DbContext
{
    public TgerCameraContext(DbContextOptions<TgerCameraContext> options)
        : base(options)
    {
    }

    // 17 DbSets cho tất cả thực thể
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<Product> Products { get; set; }
    public virtual DbSet<Cart> Carts { get; set; }
    public virtual DbSet<CartItem> CartItems { get; set; }
    public virtual DbSet<Order> Orders { get; set; }
    public virtual DbSet<OrderItem> OrderItems { get; set; }
    public virtual DbSet<Payment> Payments { get; set; }
    public virtual DbSet<Brand> Brands { get; set; }
    public virtual DbSet<Category> Categories { get; set; }
    // ... các DbSets khác

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 1. Cấu hình thực thể (ánh xạ bảng, loại cột, ràng buộc)
        // 2. Mối quan hệ (khóa ngoại, thuộc tính điều hướng)
        // 3. Bộ lọc truy vấn toàn cầu (xóa mềm)
        // 4. Giá trị mặc định

        OnModelCreatingPartial(modelBuilder);
    }
}
```

#### 4.2 Bộ Lọc Truy Vấn Toàn Cầu (Soft Delete)

```csharp
// Ví Dụ: Thực thể Sản Phẩm
modelBuilder.Entity<Product>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.IsDeleted).HasDefaultValue(false);

    // Bộ lọc truy vấn toàn cầu: Tự động loại trừ các bản ghi đã xóa
    entity.HasQueryFilter(e => e.IsDeleted == null || e.IsDeleted == false);

    // Mối Quan Hệ
    entity.HasOne(e => e.Brand)
          .WithMany(b => b.Products)
          .HasForeignKey(e => e.BrandId);

    entity.HasOne(e => e.Category)
          .WithMany(c => c.Products)
          .HasForeignKey(e => e.CategoryId);
});
```

**Hoạt Động:**

```csharp
// Khi bạn truy vấn:
var products = await _context.Products.ToListAsync();

// EF Core tự động thêm bộ lọc:
SELECT * FROM Products WHERE IsDeleted = 0 OR IsDeleted IS NULL
```

#### 4.3 Tối Ưu Hóa Truy Vấn (Ngăn Chặn N+1)

```csharp
// ❌ Xấu: Vấn đề truy vấn N+1
var cart = await _context.Carts.FirstOrDefaultAsync(c => c.Id == 1);
foreach (var item in cart.CartItems)  // Truy vấn riêng cho mỗi mục
{
    Console.WriteLine(item.Product.Name);  // Truy vấn riêng cho mỗi sản phẩm
}

// ✅ Tốt: Tải sẵn với Include
var cart = await _context.Carts
    .Include(c => c.CartItems)              // Tải các mục giỏ
    .ThenInclude(ci => ci.Product)          // Tải sản phẩm cho mỗi mục
    .FirstOrDefaultAsync(c => c.Id == 1);

// Tất cả được tải trong truy vấn đơn (join 3 chiều)
foreach (var item in cart.CartItems)  // Không có truy vấn bổ sung
{
    Console.WriteLine(item.Product.Name);
}
```

---

## II. PHÂN TÍCH SOLID Principles

### S - Nguyên Tắc Trách Nhiệm Đơn ✅

**Mỗi lớp có một lý do để thay đổi:**

| Lớp              | Trách Nhiệm             | Lý Do Thay Đổi         |
| ---------------- | ----------------------- | ---------------------- |
| `TokenService`   | Tạo token JWT           | Logic tạo token        |
| `PasswordHelper` | Hash/kiểm minh mật khẩu | Thuật toán hash        |
| `CartService`    | Hoạt động giỏ           | Logic kinh doanh giỏ   |
| `OrderService`   | Xử lý đơn hàng          | Quy trình đơn hàng     |
| `AuthController` | Định tuyến HTTP         | Thay đổi điểm cuối API |
| `MappingProfile` | Ánh xạ DTO              | Cấu hình ánh xạ        |

**Ví Dụ - CartService (Tách Biệt Các Vấn Đề):**

```csharp
public class CartService : ICartService
{
    private readonly TgerCameraContext _context;
    private readonly IDistributedCache _cache;

    // Khu Vực 1: Hoạt Động Khách
    #region Hoạt Động Giỏ Khách - Cache
    public async Task<CartDto?> GetGuestCartAsync(string sessionId) { }
    public async Task SaveGuestCartAsync(string sessionId, CartDto cart) { }
    #endregion

    // Khu Vực 2: Hoạt Động Người Dùng
    #region Hoạt Động Giỏ Người Dùng - Database
    public async Task<CartDto?> GetUserCartAsync(int userId) { }
    public async Task<CartDto> GetOrCreateUserCartAsync(int userId) { }
    #endregion

    // Khu Vực 3: Logic Hợp Nhất
    #region Logic Hợp Nhất
    public async Task MergeGuestCartToUserAsync(int userId, string sessionId) { }
    #endregion
}
```

**✅ Lợi Ích SRP:**

- Dễ Testing
- Dễ hiểu
- Dễ bảo trì
- Dễ mở rộng

---

### O - Nguyên Tắc Mở/Đóng ✅

**Mở để mở rộng, đóng để sửa đổi**

**Sử Dụng Giao Diện:**

```csharp
// ICartService.cs - Hợp Đồng (đóng để sửa đổi)
public interface ICartService
{
    Task<CartDto?> GetGuestCartAsync(string sessionId);
    Task<CartDto?> GetUserCartAsync(int userId);
    Task MergeGuestCartToUserAsync(int userId, string sessionId);
}

// CartService.cs - Triển Khai (một trong nhiều)
public class CartService : ICartService { }

// Có thể thêm mà không sửa đổi mã hiện có:
public class AdvancedCartService : ICartService
{
    // Triển Khai mới với khuyến nghị AI
}

public class RedisCartService : ICartService
{
    // Được tối ưu hóa hiệu suất với backend Redis
}
```

**Tiêm Phụ Thuộc (cho phép OCP):**

```csharp
// Program.cs
builder.Services.AddScoped<ICartService, CartService>();

// Sau này, thay đổi thành triển khai mới mà không sửa đổi bộ điều khiển:
builder.Services.AddScoped<ICartService, RedisCartService>();
```

**Mã Bộ Điều Khiển:**

```csharp
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;  // Phụ thuộc vào giao diện

    // Hoạt động với BẤT KỲ triển khai ICartService
    public CartController(ICartService cartService)
    {
        _cartService = cartService;
    }
}
```

**✅ Lợi Ích OCP:**

- Thêm triển khai mới mà không thay đổi hiện có
- Nhiều chiến lược có thể (cache-first, DB-first, mixed)
- Testing A/B dễ dàng các triển khai khác nhau

---

### L - Nguyên Tắc Thay Thế Liskov ✅

**Các kiểu phụ phải có thể sử dụng được trong chỗ của kiểu cơ sở**

```csharp
// Hợp Đồng Giao Diện
public interface ITokenService
{
    string CreateToken(User user);
}

// Triển Khai 1
public class JwtTokenService : ITokenService
{
    public string CreateToken(User user)
    {
        // Tạo token JWT
        return jwtToken;
    }
}

// Triển Khai 2 (hoàn toàn khác)
public class OAuthTokenService : ITokenService
{
    public string CreateToken(User user)
    {
        // Tạo token OAuth
        return oauthToken;
    }
}

// Cách sử dụng - cả hai hoạt động giống nhau từ góc nhìn của người tiêu dùng
public class AuthController
{
    private readonly ITokenService _tokenService;  // Có thể là một trong hai triển khai

    public async Task<IActionResult> Login(LoginDto request)
    {
        var user = await _context.Users.FindAsync(request.Email);
        var token = _tokenService.CreateToken(user);  // Hoạt động với bất kỳ ITokenService
        return Ok(new { token });
    }
}
```

**Bài Kiểm Tra Thay Thế:** ✅ Cả `JwtTokenService` và `OAuthTokenService` có thể được thay thế mà không làm hỏng mã.

**✅ Lợi Ích LSP:**

- Đa hình hoạt động chính xác
- Hành vi có thể dự đoán
- Không có giả định ẩn

---

### I - Nguyên Tắc Phân Chia Giao Diện ✅

**Khách hàng không nên phụ thuộc vào các giao diện mà họ không sử dụng**

**Tách Biệt Tốt:**

```csharp
// Giao Diện Tập Trung (mỗi cái có một mục đích)

public interface ITokenService
{
    string CreateToken(User user);
}

public interface ICartService
{
    Task<CartDto?> GetGuestCartAsync(string sessionId);
    Task<CartDto?> GetUserCartAsync(int userId);
    Task MergeGuestCartToUserAsync(int userId, string sessionId);
}

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(int? userId, string? sessionId, CheckoutDto request);
    Task<bool> CancelOrderAsync(int orderId, int userId);
}

// Bộ Điều Khiển chỉ phụ thuộc vào những dịch vụ mà họ cần
public class AuthController
{
    private readonly ITokenService _tokenService;  // Chỉ cần dịch vụ token

    // Không có quyền truy cập vào hoạt động Cart/Order
    // Không thể vô tình lạm dụng chúng
}

public class OrdersController
{
    private readonly IOrderService _orderService;  // Chỉ cần dịch vụ Order
    private readonly ICartService _cartService;    // Chỉ cần dịch vụ Cart

    // Không có quyền truy cập vào token generation
    // Sạch sẽ và tập trung các phụ thuộc
}
```

**Tách Biệt Xấu (những gì chúng tôi TRÁNH):**

```csharp
// ❌ Giao Diện Mập (vi phạm ISP)
public interface IApplicationService
{
    // Hoạt Động Token
    string CreateToken(User user);

    // Hoạt Động Giỏ
    Task<CartDto?> GetCartAsync(int? userId, string? sessionId);
    Task AddToCartAsync(int? userId, string? sessionId, int productId, int quantity);

    // Hoạt Động Đơn Hàng
    Task<OrderDto> CreateOrderAsync(int? userId, string? sessionId, CheckoutDto request);
    Task CancelOrderAsync(int orderId);

    // Hoạt Động Sản Phẩm
    Task<IEnumerable<ProductDto>> GetProductsAsync(int page, int pageSize);

    // ... nhiều hoạt động liên quan khác nữa
}

// Bộ Điều Khiển bị buộc phải phụ thuộc vào toàn bộ giao diện
// Vi phạm ISP - bộ điều khiển chỉ cần một tập hợp con của các phương thức
```

**✅ Lợi Ích ISP:**

- Giao diện tập trung
- Phụ thuộc giảm
- Testing dễ hơn (chỉ mock những gì cần thiết)
- Hợp đồng rõ ràng

---

### D - Nguyên Tắc Đảo Ngược Phụ Thuộc ✅

**Phụ thuộc vào trừu tượng, không phải cụ thể**

**Đảo Ngược Phụ Thuộc trong Program.cs:**

```csharp
// Đăng Ký các phụ thuộc (ưu tiên giao diện hơn triển khai)

// ✅ Tốt: Phụ thuộc vào ITokenService (trừu tượng)
builder.Services.AddSingleton<ITokenService, TokenService>();

// ✅ Tốt: Phụ thuộc vào ICartService (trừu tượng)
builder.Services.AddScoped<ICartService, CartService>();

// ✅ Tốt: Phụ thuộc vào IOrderService (trừu tượng)
builder.Services.AddScoped<IOrderService, OrderService>();

// DbContext là đặc biệt (cụ thể, nhưng cấp thấp)
builder.Services.AddDbContext<TgerCameraContext>(options =>
    options.UseSqlServer(connectionString));
```

**Trong Bộ Điều Khiển:**

```csharp
public class OrdersController : ControllerBase
{
    // ✅ Tiêm trừu tượng (IOrderService)
    private readonly IOrderService _orderService;

    // ❌ KHÔNG bao giờ trực tiếp khởi tạo hoặc tiêm lớp cụ thể:
    // private readonly OrderService _orderService = new OrderService();

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;  // Container DI cung cấp
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(CheckoutDto request)
    {
        // Sử dụng _orderService thông qua hợp đồng giao diện
        var order = await _orderService.CreateOrderAsync(_userId, _sessionId, request);
        return Ok(order);
    }
}
```

**Tại Sao Điều Này Quan Trọng:**

```csharp
// Luồng Phụ Thuộc (Tuân Thủ DIP)
OrdersController ← Phụ Thuộc Vào → IOrderService ← Được Triển Khai Bởi → OrderService

// KHÔNG:
OrdersController ← Trực Tiếp Liên Kết Tới → OrderService (Vi Phạm DIP)

// Nếu Triển Khai OrderService Thay Đổi:
// - Các Bộ Điều Khiển Không Biết/Không Quan Tâm (phụ thuộc vào IOrderService)
// - Chỉ Tệp OrderService.cs Thay Đổi
// - Tất Cả Mã Khác Hoạt Động Không Thay Đổi
```

**✅ Lợi Ích DIP:**

- Kết Nối Lỏng
- Dễ Trao Đổi Triển Khai
- Dễ Testing (tiêm mocks)
- Tuân Theo Nguyên Tắc Hollywood ("Đừng Gọi Chúng Tôi, Chúng Tôi Sẽ Gọi Bạn")

---

## III. CÁC MẪU KIẾN TRÚC

### 1. Mẫu Xóa Mềm

**Vấn Đề:** Xóa cứng mất dữ liệu, phá vỡ tính toàn vẹn tham chiếu

**Giải Pháp:** Đánh dấu là đã xóa thay vì xóa

```csharp
// Cấu Trúc Bảng
CREATE TABLE Products (
    Id INT PRIMARY KEY,
    Name NVARCHAR(100),
    StockQuantity INT,
    IsDeleted BIT DEFAULT 0  // Cột xóa mềm
);

// Hoạt Động "Xóa"
UPDATE Products SET IsDeleted = 1 WHERE Id = 5;

// Truy Vấn Tự Động Loại Trừ Đã Xóa
SELECT * FROM Products WHERE IsDeleted = 0;

// Khôi Phục Có Thể
UPDATE Products SET IsDeleted = 0 WHERE Id = 5;  // Khôi Phục
```

**Triển Khai EF Core:**

```csharp
// Model
public class Product
{
    public int Id { get; set; }
    public bool? IsDeleted { get; set; }  // Kiểu bool nullable (mặc định: false)
}

// Cấu Hình DbContext
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Product>(entity =>
    {
        // Bộ Lọc Truy Vấn Toàn Cầu
        entity.HasQueryFilter(e => e.IsDeleted == null || e.IsDeleted == false);

        entity.Property(e => e.IsDeleted).HasDefaultValue(false);
    });
}

// Cách Sử Dụng: Lọc Tự Động
var activeProducts = await _context.Products.ToListAsync();  // IsDeleted được lọc tự động

// Lấy Tất Cả Bao Gồm Đã Xóa (Rõ Ràng)
var allProducts = await _context.Products.IgnoreQueryFilters().ToListAsync();
```

**✅ Lợi Ích Xóa Mềm:**

- Khôi Phục Dữ Liệu
- Dấu Vết Kiểm Toán
- Không Xóa Theo Tầng Vòng
- Tính Toàn Vẹn Tham Chiếu

---

### 2. Chiến Lược Lưu Trữ Kép: Khách vs. Authentication

**Vấn Đề:** Khách nên không ảnh hưởng đến database; người dùng Authentication cần bền vững

**Giải Pháp:** Kiến Trúc Lưu Trữ Kép

```
Kiến Trúc:
┌─────────────────────────────────────────────────┐
│           Giỏ Dựa Trên Session (Khách)         │
├─────────────────────────────────────────────────┤
│  Lưu Trữ: Distributed Cache (Memory/Redis)     │
│  Thời Gian: TTL 24 giờ                         │
│  Định Dạng Khóa: cart:{sessionId}              │
│  Cập Nhật: Chỉ Session, không có truy vấn DB   │
└─────────────────────────────────────────────────┘

              ↓ (Người Dùng Đăng Nhập)

┌─────────────────────────────────────────────────┐
│       Giỏ Dựa Trên Người Dùng (Authentication)       │
├─────────────────────────────────────────────────┤
│  Lưu Trữ: SQL Database (Cart + CartItems)      │
│  Thời Gian: Bền Vững                           │
│  Truy Vấn: UserId-based với tải sẵn            │
│  Cập Nhật: Hỗ Trợ Giao Dịch Đầy Đủ           │
│  Tự Động Hợp Nhất: Khách → Người Dùng         │
└─────────────────────────────────────────────────┘
```

**Quy Trình Hợp Nhất:**

```csharp
public async Task MergeGuestCartToUserAsync(int userId, string sessionId)
{
    // Bước 1: Lấy giỏ khách từ cache
    var guestCart = await GetGuestCartAsync(sessionId);
    if (guestCart?.Items == null) return;

    // Bước 2: Lấy giỏ người dùng (tạo nếu không tồn tại)
    var userCart = await GetOrCreateUserCartAsync(userId);

    // Bước 3: Authentication và hợp nhất từng mục
    foreach (var guestItem in guestCart.Items)
    {
        var product = await _context.Products.FindAsync(guestItem.ProductId);

        // Authentication Kho Hàng
        if (product.StockQuantity < guestItem.Quantity)
            guestItem.Quantity = product.StockQuantity;

        // Hợp Nhất (thêm vào hiện có hoặc tạo mới)
        var existingItem = userCart.CartItems
            .FirstOrDefault(ci => ci.ProductId == guestItem.ProductId);

        if (existingItem != null)
            existingItem.Quantity += guestItem.Quantity;
        else
            userCart.CartItems.Add(new CartItem
            {
                ProductId = guestItem.ProductId,
                Quantity = guestItem.Quantity
            });
    }

    // Bước 4: Bền vững và dọn dẹp
    await _context.SaveChangesAsync();
    await ClearGuestCartAsync(sessionId);  // Xóa khỏi cache
}
```

**✅ Lợi Ích:**

- Không có tải database cho khách duyệt
- Vẫn Bền vững cho người dùng Authentication
- Authentication Kho Hàng Trên Hợp Nhất Ngăn Chặn Bán Quá Mức
- Làm Sạch Tự Động
- Trải Nghiệm Người Dùng Mượt Mà

---

### 3. Quản Lý Giao Dịch Trong Thanh Toán

**Vấn Đề:** Tạo Một Phần Đơn Hàng Nếu Lỗi Xảy Ra (Tiền Lấy, Đơn Hàng Không Tạo)

**Giải Pháp:** Cơ Sở Dữ Liệu Giao Dịch Qua Stored Procedure

```sql
-- sp_CreateOrder - Giao Dịch Nguyên Tử
CREATE PROCEDURE sp_CreateOrder
    @UserId INT = NULL,
    @SessionId NVARCHAR(100) = NULL,
    @ShippingAddressId INT,
    @TotalPrice DECIMAL(18,2),
    @PaymentMethod NVARCHAR(50),
    @OrderId INT OUTPUT
AS
BEGIN
    BEGIN TRY
        BEGIN TRANSACTION;

        -- 1. Tạo Đơn Hàng
        INSERT INTO Orders (UserId, SessionId, TotalPrice, Status, CreatedAt)
        VALUES (@UserId, @SessionId, @TotalPrice, 'Pending', GETDATE());

        SET @OrderId = SCOPE_IDENTITY();

        -- 2. Tạo OrderItems (lấy dữ liệu từ dữ liệu session)
        -- Loop và INSERT cho mỗi mục

        -- 3. Giảm Kho Hàng Sản Phẩm
        UPDATE Products
        SET StockQuantity = StockQuantity - @Quantity
        WHERE Id = @ProductId
        AND StockQuantity >= @Quantity;

        -- 4. Tạo Bản Ghi Thanh Toán
        INSERT INTO Payments (OrderId, PaymentMethod, Amount, Status)
        VALUES (@OrderId, @PaymentMethod, @TotalPrice, 'Pending');

        COMMIT TRANSACTION;

        RETURN 0;  -- Thành Công

    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;  -- Truyền Lỗi
    END CATCH
END
```

**Đảm Bảo Tất Cả-Hoặc-Không:**

- ✅ Đơn Hàng Được Tạo HOẶC
- ✅ Kho Hàng Đã Giảm HOẶC
- ✅ Thanh Toán Được Tạo

Tất Cả Thành Công Hoặc Tất Cả Rollback. Không Có Trạng Thái Một Phần Nào.

---

## IV. CHẤT LƯỢNG MÃ

### Phân Tích Độ Phức Tạp

| Thành Phần                      | Độ Phức Tạp | Đánh Giá                     |
| ------------------------------- | ----------- | ---------------------------- |
| **MergeGuestCartToUserAsync**   | Trung Bình  | Authentication Kho + Hợp Nhất      |
| **CreateOrderAsync**            | Trung Bình  | Authentication Multuple + Gọi SP   |
| **TokenService.CreateToken**    | Thấp        | Tạo JWT Thẳng Tuyến          |
| **PasswordHelper**              | Thấp        | Triển Khai PBKDF2 Tuyến Tính |
| **ExceptionHandlingMiddleware** | Thấp        | Ánh Xạ Ngoại Lệ Rõ Ràng      |

### Tái Sử Dụng Mã

✅ **Cao:**

- `PasswordHelper` được sử dụng trong Endpoints Authentication và Testing
- `TokenService` Tạo Token Tập Trung
- `MappingProfile` Nguồn Duy Nhất Cho DTOs
- `ExceptionHandlingMiddleware` Xử Lý Lỗi Toàn Cầu

### Phạm Vi Testing

✅ **Tốt:**

- Testing Đơn Vị Cho `CartService`, `PasswordHelper`
- Khung Mock (Moq) Được Sử Dụng Đúng Cách
- Dữ Liệu Testing Được Cung Cấp Cho Các Kịch Bản

---

## V. KHUYẾN NGHỊ

### 1. **Thêm Testing Tích Hợp**

```csharp
// Ví Dụ: Testing Tích Hợp Quy Trình Thanh Toán
[Fact]
public async Task CheckoutFlow_WithValidCart_ShouldCreateOrder()
{
    // Arrange: Thiết Lập Database Testing, Người Dùng, Sản Phẩm, Giỏ
    var user = await CreateTestUser();
    var product = await CreateTestProduct(quantity: 10);
    var cart = await CreateUserCart(user.Id);
    await AddItemToCart(cart.Id, product.Id, 2);

    // Act
    var result = await _orderService.CreateOrderAsync(user.Id, null, new CheckoutDto
    {
        ShippingAddressId = 1,
        PaymentMethod = "COD"
    });

    // Assert
    Assert.NotNull(result);
    Assert.Equal(product.Price * 2, result.TotalPrice);

    // Xác Minh Kho Hàng Đã Giảm
    var updatedProduct = await _context.Products.FindAsync(product.Id);
    Assert.Equal(8, updatedProduct.StockQuantity);
}
```

### 2. **Thêm Giới Hạn Tốc Độ Yêu Cầu**

```csharp
// Ngăn Chặn Tấn Công Brute-Force Trên Endpoint Đăng Nhập
builder.Services.AddRateLimiting(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,              // 5 Yêu Cầu
                Window = TimeSpan.FromMinutes(1)  // mỗi phút
            }));
});
```

### 3. **Thêm Tài Liệu Swagger**

```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TgerCamera Store API",
        Version = "v1.0",
        Description = "API Cửa Hàng Máy Ảnh Hoàn Chỉnh"
    });

    // Thêm Định Nghĩa Security JWT
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { ... });
});
```

### 4. **Ghi Nhật Ký Kiểm Toán Cho Các Hoạt Động Nhạy Cảm**

```csharp
// Ghi Lại Ai Thay Đổi Cái Gì Và Khi Nào
private async Task LogAuditAsync(string action, string entityType, int entityId, int? userId)
{
    await _context.AuditLogs.AddAsync(new AuditLog
    {
        Action = action,
        EntityType = entityType,
        EntityId = entityId,
        UserId = userId,
        Timestamp = DateTime.UtcNow
    });
    await _context.SaveChangesAsync();
}

// Sử Dụng Trong OrderService
await LogAuditAsync("CancelOrder", "Order", orderId, userId);
```

---

## VI. Tóm Tắt

| Khía Cạnh            | Xếp Hạng   | Nhận Xét                                 |
| -------------------- | ---------- | ---------------------------------------- |
| **SOLID Principles** | ⭐⭐⭐⭐⭐ | Tuân Thủ Xuất Sắc                        |
| **Architecture**     | ⭐⭐⭐⭐⭐ | Được Lớp Hóa Tốt, SoC Rõ Ràng            |
| **Security**         | ⭐⭐⭐⭐   | Strong (Có Thể Cải Thiện: Rate Limiting) |
| **Error Handling**   | ⭐⭐⭐⭐⭐ | Global Middleware, Phản Hồi Tốt          |
| **Database**         | ⭐⭐⭐⭐   | EF Core + Soft Delete Pattern            |
| **Caching**          | ⭐⭐⭐⭐   | Dual-Storage Triển Khai Tốt              |
| **Testing**          | ⭐⭐⭐     | Nền Tảng Tốt, Cần Testing Tích Hợp      |
| **Logging**          | ⭐⭐⭐⭐   | Yêu Cầu/Phản Hồi Được Nắm Bắt Tốt        |

**Tổng Thể:** ✅ Sẵn Sàng Triển Khai Với Cải Thiện Nhỏ

**Khuyến Nghị:** Tiếp Tục Với Triển Khai Sau Khi Thêm Testing Tích Hợp Và Giới Hạn Tốc Độ Yêu Cầu.
