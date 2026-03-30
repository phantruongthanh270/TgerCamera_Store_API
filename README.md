# TgerCamera - Cửa hàng Máy ảnh Trực tuyến (E-Commerce)

Một nền tảng e-commerce full-stack dành cho việc mua bán, cho thuê và quản lý thiết bị máy ảnh với backend ASP.NET Core và frontend HTML/CSS/JavaScript.

## 📋 Mục lục

- [Tính năng](#tính-năng)
- [Kiến trúc Dự án](#kiến-trúc-dự-án)
- [Công nghệ Sử dụng (Tech Stack)](#công-nghệ-sử-dụng-tech-stack)
- [Hướng dẫn Cài đặt](#hướng-dẫn-cài-đặt)
- [Thiết lập Cơ sở dữ liệu](#thiết-lập-cơ-sở-dữ-liệu)
- [Chạy Ứng dụng](#chạy-ứng-dụng)
- [Tài liệu API](#tài-liệu-api)
- [Cấu hình](#cấu-hình)
- [Kiểm thử (Testing)](#kiểm-thử-testing)
- [Cấu trúc Dự án](#cấu-trúc-dự-án)
- [Phát triển](#phát-triển)

## ✨ Tính năng

### E-Commerce Cốt lõi

- **Product Catalog** (Danh mục sản phẩm) - Duyệt sản phẩm với bộ lọc, tìm kiếm và phân trang (pagination)
- **Soft Delete** (Xóa mềm) - Xóa sản phẩm an toàn với khả năng khôi phục dữ liệu
- **Shopping Cart** (Giỏ hàng) - Hỗ trợ giỏ hàng cho khách (không cần đăng nhập) và tự động gộp (merge) khi xác thực
- **User Accounts** (Tài khoản người dùng) - Đăng ký/đăng nhập an toàn với xác thực JWT
- **Orders** (Đơn hàng) - Hệ thống hoàn tất thanh toán (checkout) và quản lý đơn hàng
- **Wishlist** (Sản phẩm yêu thích) - Lưu các sản phẩm yêu thích để xem sau

### Hệ thống Cho thuê (Rental System)

- **Product Rental** (Thuê sản phẩm) - Thuê máy ảnh và thiết bị theo ngày
- **Availability Tracking** (Theo dõi tình trạng sẵn có) - Quản lý tồn kho cho thuê theo thời gian thực
- **Pricing** (Định giá) - Định giá thuê theo ngày linh hoạt

### Bảo mật & Chất lượng

- **JWT Authentication** - Xác thực API bảo mật dựa trên token
- **Authorization** - Kiểm soát truy cập dựa trên vai trò (Role-based access control: Admin/Customer)
- **Data Validation** - Xác thực đầu vào tại tầng DTO với DataAnnotations
- **Exception Handling** - Xử lý lỗi tập trung với phản hồi JSON nhất quán
- **Request Logging** - Ghi nhật ký HTTP request/response để debug và giám sát
- **Unit Tests** - Hơn 16 unit tests toàn diện bao phủ xác thực (authentication), giỏ hàng (cart), và các thao tác mật khẩu
- **XML Documentation** - Tóm tắt tài liệu API đầy đủ với hỗ trợ IntelliSense

## 🏗️ Kiến trúc Dự án

### Kiến trúc phân tầng (Layered Architecture)

```
┌─────────────────────────────────────────┐
│         Frontend (HTML/CSS/JS)          │
│    Port 3000 - Python http.server      │
└────────────────┬────────────────────────┘
                 │ HTTP REST API
┌────────────────▼────────────────────────┐
│      ASP.NET Core Backend                │
│           Port 8000                      │
├─────────────────────────────────────────┤
│        Controllers Layer                 │
│  (ProductController, AuthController,    │
│   CartController, OrdersController)     │
├─────────────────────────────────────────┤
│        Services Layer                    │
│  (CartService, TokenService,            │
│   PasswordHelper)                       │
├─────────────────────────────────────────┤
│      Entity Framework Core               │
│     (Data Access & Mapping)             │
├─────────────────────────────────────────┤
│   SQL Server Database                    │
│  (Products, Orders, Users, Carts, ...)  │
└─────────────────────────────────────────┘
```

### Các Design Patterns chính

- **DTO Pattern** - Data Transfer Objects để xác thực request/response của API
- **Soft Delete** - Xóa logic bằng cách đánh dấu `IsDeleted = true` thay vì xóa cứng (hard deletion)
- **Session-based Carts** - Giỏ hàng tạm thời cho khách thông qua HttpOnly cookies
- **JWT Tokens** - Xác thực không trạng thái (stateless authentication) với tự động gộp giỏ hàng khi login
- **AutoMapper** - Tự động mapping từ model sang DTO

## 🛠️ Công nghệ Sử dụng (Tech Stack)

### Backend

- **Runtime**: .NET 10.0
- **Framework**: ASP.NET Core
- **ORM**: Entity Framework Core 10.0.5
- **Database**: SQL Server
- **Authentication**: JWT (JSON Web Tokens)
- **Validation**: DataAnnotations
- **Logging**: Built-in ILogger
- **Testing**: XUnit 2.9.3, Moq 4.20.70

### Frontend

- **Markup**: HTML5
- **Styling**: Tailwind CSS
- **Typography**: Google Fonts (Manrope, Inter)
- **Icons**: Material Symbols
- **JavaScript**: Vanilla ES6+
- **Server**: Python http.server (cho môi trường phát triển - development)

## 🚀 Hướng dẫn Cài đặt

### Yêu cầu hệ thống (Prerequisites)

- **.NET SDK 10.0 trở lên** - [Download](https://dotnet.microsoft.com/download)
- **SQL Server 2019+** hoặc **LocalDB** - [Download](https://www.microsoft.com/sql-server/sql-server-downloads)
- **Python 3.8+** (dành cho frontend dev server) - [Download](https://www.python.org)
- **Git** - [Download](https://git-scm.com)

### Thiết lập Backend

1. **Điều hướng đến thư mục backend:**

   ```bash
   cd TgerCamera/TgerCamera
   ```

2. **Cài đặt dependencies:**

   ```bash
   dotnet restore
   ```

3. **Cấu hình chuỗi kết nối database** (xem phần [Thiết lập Cơ sở dữ liệu](#thiết-lập-cơ-sở-dữ-liệu))

4. **Build dự án:**

   ```bash
   dotnet build
   ```

5. **Chạy migrations** (nếu có):
   ```bash
   dotnet ef database update
   ```

### Thiết lập Frontend

1. **Điều hướng đến thư mục frontend:**

   ```bash
   cd ../TgerCamera_UI
   ```

2. **Khởi chạy Python development server:**

   ```bash
   python -m http.server 3000
   ```

3. **Truy cập trang web:**
   - Mở trình duyệt và truy cập `http://localhost:3000`
   - Trang chính: `index.html`

## 🗄️ Thiết lập Cơ sở dữ liệu (Database Setup)

### Cấu hình Connection String

Chỉnh sửa file `TgerCamera/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=TgerCamera;Trusted_Connection=true;Encrypt=false;"
  }
}
```

### Các ví dụ về Connection String

**SQL Server (Windows Authentication):**

```
Server=YOUR_SERVER;Database=TgerCamera;Trusted_Connection=true;Encrypt=false;
```

**SQL Server (SQL Authentication):**

```
Server=YOUR_SERVER;Database=TgerCamera;User Id=sa;Password=YOUR_PASSWORD;Encrypt=false;
```

**LocalDB (Development):**

```
Server=(localdb)\mssqllocaldb;Database=TgerCamera;Integrated Security=true;Encrypt=false;
```

### Khởi tạo Database

Database sẽ được tự động tạo và seed dữ liệu trong lần chạy ứng dụng đầu tiên (EF Core Migrations).

**Để cập nhật database theo cách thủ công:**

```bash
dotnet ef database update --project TgerCamera --startup-project TgerCamera
```

## 🏃 Chạy Ứng dụng

### Lựa chọn 1: Chỉ chạy Backend (Để Test API)

```bash
cd TgerCamera/TgerCamera
dotnet run
```

**Đầu ra (Output):**

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:8000
      Now listening on: https://localhost:8001
```

**Tài liệu API:**

- Scalar UI: `http://localhost:8000/scalar/v1`
- Swagger/OpenAPI: `http://localhost:8000/openapi/v1.json`

### Lựa chọn 2: Chạy cả Frontend và Backend

**Terminal 1 (Backend):**

```bash
cd TgerCamera/TgerCamera
dotnet run
```

**Terminal 2 (Frontend):**

```bash
cd ../TgerCamera_UI
python -m http.server 3000
```

**Truy cập trang web:**

- Frontend: `http://localhost:3000`
- Backend API: `http://localhost:8000/api`

## 📚 Tài liệu API

### Base URL

```
http://localhost:8000/api
```

### Authentication (Xác thực)

**Register User (Đăng ký)**

- **Endpoint:** `POST /auth/register`
- **Request:**
  ```json
  {
    "email": "user@example.com",
    "password": "SecurePass123!",
    "fullName": "John Doe"
  }
  ```
- **Response:** JWT token + thời hạn 60 phút

**Login User (Đăng nhập)**

- **Endpoint:** `POST /auth/login`
- **Request:**
  ```json
  {
    "email": "user@example.com",
    "password": "SecurePass123!"
  }
  ```
- **Response:** JWT token + thời hạn 60 phút

### Products (Sản phẩm)

**Get All Products (Paginated - Lấy tất cả sản phẩm có phân trang)**

- **Endpoint:** `GET /api/products`
- **Query Parameters:**
  - `brandId` (int, optional) - Lọc theo thương hiệu (brand)
  - `categoryId` (int, optional) - Lọc theo danh mục (có hỗ trợ phân cấp hierarchy)
  - `conditionId` (int, optional) - Lọc theo tình trạng (condition)
  - `minPrice` (decimal, optional) - Lọc giá tối thiểu
  - `maxPrice` (decimal, optional) - Lọc giá tối đa
  - `q` (string, optional) - Tìm kiếm theo tên sản phẩm
  - `page` (int, default: 1) - Số trang
  - `pageSize` (int, default: 20, max: 200) - Số item mỗi trang
  - `sortBy` (string, default: createdAt) - Trường để sắp xếp: "price", "name", hoặc "createdAt"
  - `sortDir` (string, default: desc) - Hướng sắp xếp: "asc" hoặc "desc"

**Example Request:**

```bash
curl "http://localhost:8000/api/products?categoryId=1&minPrice=100&maxPrice=2000&page=1&pageSize=20&sortBy=price&sortDir=asc"
```

**Get Single Product (Lấy 1 sản phẩm)**

- **Endpoint:** `GET /api/products/{id}`
- **Response:** ProductDto với images, specifications, brand, category, và condition

**Create Product (Chỉ dành cho Admin)**

- **Endpoint:** `POST /api/products`
- **Headers:** `Authorization: Bearer {token}`
- **Request:**
  ```json
  {
    "name": "Canon EOS R5",
    "description": "Professional mirrorless camera",
    "price": 3499.99,
    "stockQuantity": 50,
    "brandId": 1,
    "categoryId": 4,
    "conditionId": 1,
    "mainImageUrl": "https://example.com/image.jpg",
    "specifications": [
      { "key": "Sensor", "value": "Full Frame" },
      { "key": "Resolution", "value": "45MP" }
    ]
  }
  ```

**Delete Product (Chỉ dành cho Admin, Soft Delete)**

- **Endpoint:** `DELETE /api/products/{id}`
- **Headers:** `Authorization: Bearer {token}`
- **Note:** Mark (Đánh dấu) sản phẩm là đã xóa trong cơ sở dữ liệu (IsDeleted = true)

### Shopping Cart (Giỏ hàng)

**Get Cart (Lấy giỏ hàng)**

- **Endpoint:** `GET /api/cart`
- **Note:** Trả về giỏ hàng của guest hoặc user dựa trên trạng thái authentication
- **Guest Session:** SessionId cookie tự động được tạo

**Add Item to Cart (Thêm item vào giỏ)**

- **Endpoint:** `POST /api/cart/items`
- **Request:**
  ```json
  {
    "productId": 1,
    "quantity": 2
  }
  ```

**Update Item Quantity (Cập nhật số lượng)**

- **Endpoint:** `PUT /api/cart/items/{itemId}`
- **Request:**
  ```json
  {
    "quantity": 5
  }
  ```

**Remove Item from Cart (Xóa item khỏi giỏ)**

- **Endpoint:** `DELETE /api/cart/items/{itemId}`

### Rental Products (Sản phẩm Phục vụ Thuê)

**Get All Rental Products (Lấy tất cả sản phẩm cho thuê)**

- **Endpoint:** `GET /api/rentalproduct`

**Get Single Rental Product (Lấy 1 sản phẩm cho thuê)**

- **Endpoint:** `GET /api/rentalproduct/{id}`

**Create Rental Product (Chỉ dành cho Admin)**

- **Endpoint:** `POST /api/rentalproduct`
- **Headers:** `Authorization: Bearer {token}`
- **Request:**
  ```json
  {
    "productId": 1,
    "pricePerDay": 25.0,
    "availableQuantity": 10
  }
  ```

### Response Format (Định dạng phản hồi)

**Success Response (200, 201):**

```json
{
  "id": 1,
  "name": "Product Name",
  "price": 99.99,
  "data": "..."
}
```

**Error Response (400, 401, 404, 500):**

```json
{
  "statusCode": 400,
  "message": "Validation failed",
  "errors": ["Email is required", "Password must be at least 6 characters"],
  "timestamp": "2026-03-28T10:30:00Z"
}
```

## ⚙️ Cấu hình (Configuration)

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=TgerCamera;..."
  },
  "Jwt": {
    "Key": "your_secret_key_min_32_characters_long_recommended",
    "Issuer": "TgerCamera",
    "Audience": "TgerCameraUsers",
    "DurationInMinutes": 60
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

### Các điểm cần lưu ý khi cấu hình

1. **JWT Secret:** Hãy thay đổi `Jwt:Key` thành một giá trị mạnh và duy nhất khi đưa lên môi trường production.
2. **Connection String:** Cập nhật với thông tin SQL Server của bạn.
3. **CORS:** Hiện tại đang cho phép tất cả các nguồn (origins) ở môi trường phát triển (xem Program.cs).
4. **HTTPS:** Cập nhật port 8001 cho chạy HTTPS khi cần thiết.

### Kế thừa Biến Môi trường (Environment Variables - Production)

Sử dụng biến môi trường (environment variables) để ghi đè (override) cấu hình trong appsettings:

```bash
# Linux/Mac
export ConnectionStrings__DefaultConnection="Server=...;Database=TgerCamera;..."
export Jwt__Key="your_production_secret_key"

# Windows PowerShell
$env:ConnectionStrings__DefaultConnection="Server=...;Database=TgerCamera;..."
$env:Jwt__Key="your_production_secret_key"
```

## 🧪 Kiểm thử (Testing)

### Chạy Unit Tests

**Run all tests (Chạy tất cả test):**

```bash
cd TgerCamera.Tests
dotnet test
```

**Test Coverage (Độ phủ):**

- PasswordHelperTests.cs - 6 tests cho hashing/verification (băm và xác thực) mật khẩu
- CartServiceTests.cs - 3 tests cho các thao tác với giỏ hàng
- AuthControllerTests.cs - 5 tests cho luồng xác thực

**Run specific test class (Chạy class test cụ thể):**

```bash
dotnet test --filter "ClassName=TgerCamera.Tests.Services.PasswordHelperTests"
```

**Run with verbose output (Chạy test hiển thị chi tiết):**

```bash
dotnet test --logger "console;verbosity=detailed"
```

### Kết quả Test (Test Results)

```
Passed:     16
Failed:     0
Skipped:    0
Duration:   200ms
```

## 📁 Cấu trúc Dự án (Project Structure)

```
anti/
├── TgerPhotos/                           # Backend & Solution
│   ├── TgerCamera/                      # Backend ASP.NET Core project
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs        # Các endpoint xác thực User
│   │   │   ├── ProductController.cs     # Product CRUD và lọc
│   │   │   ├── CartController.cs        # Thao tác Shopping cart
│   │   │   ├── OrdersController.cs      # Xử lý đơn hàng
│   │   │   ├── RentalProductController.cs # Quản lý sản phẩm thuê
│   │   │   ├── BrandController.cs       # Quản lý Brand
│   │   │   ├── CategoryController.cs    # Quản lý Category
│   │   │   └── ...                      # Các controllers khác
│   │   ├── Models/
│   │   │   ├── Product.cs               # Entity Product (có soft delete)
│   │   │   ├── Brand.cs                 # Entity Brand (có soft delete)
│   │   │   ├── Category.cs              # Entity Category (có soft delete)
│   │   │   ├── Order.cs                 # Entity Order (có soft delete)
│   │   │   ├── Cart.cs                  # Entity Shopping cart
│   │   │   ├── User.cs                  # Entity User account
│   │   │   └── ...                      # Các models khác
│   │   ├── Dtos/
│   │   │   ├── Auth/
│   │   │   │   ├── LoginDto.cs          # Validate request Login
│   │   │   │   ├── RegisterDto.cs       # Validate request Registration
│   │   │   │   └── AuthResultDto.cs     # DTO response Auth
│   │   │   ├── CartItemCreateDto.cs     # Validate cho thao tác Add to cart
│   │   │   ├── ProductDto.cs            # DTO response Product
│   │   │   └── ...                      # Các DTOs khác
│   │   ├── Services/
│   │   │   ├── TokenService.cs          # Trình tạo JWT token
│   │   │   ├── ITokenService.cs         # Interface của Token service
│   │   │   ├── CartService.cs           # Business logic của Cart
│   │   │   ├── ICartService.cs          # Interface của Cart service
│   │   │   └── PasswordHelper.cs        # Password hashing (PBKDF2)
│   │   ├── Middleware/
│   │   │   ├── ExceptionHandlingMiddleware.cs  # Khối xử lý lỗi tập trung
│   │   │   └── RequestLoggingMiddleware.cs     # Ghi nhật ký HTTP request/response
│   │   ├── Mapping/
│   │   │   └── MappingProfile.cs        # Cấu hình AutoMapper
│   │   ├── Helpers/
│   │   │   └── PasswordHelper.cs        # Các tiện ích (utilities) cho password
│   │   ├── Program.cs                   # Cấu hình startup của ứng dụng
│   │   ├── appsettings.json             # Cấu hình
│   │   └── TgerCamera.csproj            # Project file
│   ├── TgerCamera.Tests/                 # Unit test project
│   │   ├── Controllers/
│   │   │   └── AuthControllerTests.cs   # Test Auth controller (5 tests)
│   │   ├── Services/
│   │   │   └── CartServiceTests.cs      # Test Cart service (3 tests)
│   │   ├── Helpers/
│   │   │   └── PasswordHelperTests.cs   # Test Password helper (6 tests)
│   │   └── TgerCamera.Tests.csproj      # Test project file
│   ├── TgerCamera.sln                   # Visual Studio solution file
│   └── README.md                        # Khóa file chứa các tài liệu này
├── TgerCamera_UI/                        # Frontend project (độc lập)
│   ├── html/
│   │   ├── index.html                   # Trang chủ (Home)
│   │   ├── ProductDetail.html           # Trang Product details
│   │   ├── ListProduct.html             # Trang danh sách sản phẩm
│   │   ├── Cart.html                    # Trang giỏ hàng
│   │   ├── Login.html                   # Trang đăng nhập
│   │   ├── CheckOut.html                # Trang Checkout
│   │   └── ...                          # Các trang khác
│   ├── js/
│   │   ├── product-detail.js            # Logic của trang Detail
│   │   ├── product-list.js              # Logic danh sách sản phẩm
│   │   └── cart.js                      # Logic quản lý giỏ hàng
│   ├── doc/
│   │   └── DESIGN_*.md                  # Thông số kỹ thuật UI design
│   └── (static assets)
```

## 💻 Phát triển (Development)

### Các bước thêm tính năng mới

1. **Create Model** ở thư mục `Models/` (thêm `IsDeleted` nếu thích hợp)
2. **Create DTO** ở thư mục `Dtos/` kèm các attributes validation
3. **Create/Update Controller** ở thư mục `Controllers/` kèm XML documentation
4. **Create/Update Service** ở thư mục `Services/` nếu cần có business logic
5. **Add Tests** ở thư mục `TgerCamera.Tests/`
6. **Update MappingProfile.cs** với cấu hình của AutoMapper
7. **Update README.md** với API documentation (nếu thêm mới API)

### Tiêu chuẩn viết Code (Code Standards)

- **Naming:** Sử dụng PascalCase cho classes/methods, camelCase cho variables (tên biến)
- **Documentation:** Sử dụng XML comments (`///`) cho tất cả các members dạng public
- **Validation:** Sử dụng DataAnnotations bên trong các DTOs
- **Error Handling:** Để ExceptionHandlingMiddleware tự động bắt và format lại các lỗi
- **Testing:** Hướng tới code coverage >80% cho các luồng xử lý quan trọng (critical paths)
- **Soft Delete:** Luôn luôn filter `IsDeleted` ở các câu truy vấn

### Debugging

**Enable detailed logging (Bật log chi tiết):**

1. Cập nhật file `appsettings.Development.json`
2. Đặt `Logging:LogLevel:Default` thành `"Debug"`
3. Kiểm tra các request/response từ output của RequestLoggingMiddleware

**Test API endpoints:**

- Sử dụng Scalar UI: `http://localhost:8000/scalar/v1`
- Sử dụng Postman: Import file từ `http://localhost:8000/openapi/v1.json`

### Performance Considerations (Tối ưu Hiệu suất)

- **Pagination:** Luôn sử dụng `page` và `pageSize` cho lượng dữ liệu (datasets) lớn
- **Includes:** Dùng `.Include()` cho các navigation properties trong các truy vấn
- **Caching:** Cân nhắc cache (lưu trữ bộ nhớ đệm) lại các categories/brands được hay truy cập
- **Indexing:** Đảm bảo thêm index vào các khóa trong database như `ProductId`, `UserId`, `IsDeleted`

## 🔐 Security Best Practices (Các Mẫu Bảo mật Tốt)

- **Passwords:** Không bao giờ lưu hay tiết lộ password hashes của User
- **Tokens:** Chỉ lưu JWT tokens trong HttpOnly cookies ở (frontend)
- **HTTPS:** Bật HTTPS ở môi trường production với chứng chỉ SSL hợp lệ
- **CORS:** Chỉ cho phép truy cập từ các origins hợp lệ ở môi trường production
- **SQL Injection:** Dùng tính năng parameterized queries (đã được tự động hỗ trợ trong EF Core)
- **Validation:** Luôn validate đầu vào ở tầng DTO
- **Authorization:** Kiểm tra phân quyền sử dụng attribute `[Authorize(Roles="Admin")]`

## 📝 Soft Delete System (Hệ thống Xóa Mềm)

Tất cả các business entities chính trong hệ thống đều sử dụng xóa mềm (soft delete) để phòng mất dữ liệu và khôi phục sau này:

### Soft Delete Implementation

**Các Models áp dụng Soft Delete:**

- Product (IsDeleted = bool?)
- Brand (IsDeleted = bool?)
- Category (IsDeleted = bool?)
- Order (IsDeleted = bool?)
- RentalProduct (IsDeleted = bool?)

**Query Pattern for Soft Delete:**

```csharp
// Filter out deleted records (loại bỏ các record đã xóa)
query = query.Where(p => p.IsDeleted == null || p.IsDeleted == false);
```

**Delete Pattern (Soft Delete):**

```csharp
entity.IsDeleted = true;
entity.UpdatedAt = DateTime.UtcNow;
_context.Update(entity);
await _context.SaveChangesAsync();
```

**Recovery Pattern (Nếu muốn khôi phục dữ liệu):**

```csharp
entity.IsDeleted = false;
entity.UpdatedAt = DateTime.UtcNow;
_context.Update(entity);
await _context.SaveChangesAsync();
```

## 📞 Hỗ trợ & Khắc phục Lỗi (Support & Troubleshooting)

### Các Vấn đề Phổ biến (Common Issues)

**Database Connection Failed (Kết nối cơ sở dữ liệu thất bại)**

- Kiểm tra SQL Server có đang chạy không
- Xác minh lại chuỗi connection string bên trong file `appsettings.json`
- Đảm bảo cơ sở dữ liệu có tồn tại (hoặc để EF Core tự tạo nó)

**Port Already in Use (Cổng port đã được sử dụng)**

- Backend: Đổi port trong `Properties/launchSettings.json`
- Frontend: Sử dụng lệnh `python -m http.server 3001` tới một cổng khác

**CORS Errors (Lỗi CORS)**

- Frontend cần được match URL giống API base URL (http://localhost:8000)
- Kiểm tra thiết lập cấu hình CORS policy ở `Program.cs`

**JWT Token Expired (JWT Token bị hết hạn)**

- Các tokens chỉ mặc định kéo dài trong 60 phút
- Tiến hành Login lại để sinh (get) token mới
- Tăng giá trị `Jwt:DurationInMinutes` trong `appsettings.json`

## 📄 Bản quyền (License)

Dự án thuộc nền tảng ứng dụng mua sắm TgerCamera e-commerce platform.

---

**Cập nhật lần cuối:** 28 Tháng 03, 2026  
**Phiên bản (Version):** 1.0git
