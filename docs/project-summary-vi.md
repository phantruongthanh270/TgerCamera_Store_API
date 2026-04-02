# 📋 TÓM TẮT DỰ ÁN TGERCAMERA STORE API

## 🎯 Tổng Quan

**TgerCamera Store API** là một hệ thống thương mại điện tử chuyên về thiết bị chụp ảnh (máy ảnh, ống kính, phụ kiện), được xây dựng trên **ASP.NET Core 10** với SQL Server database. Dự án hỗ trợ:

- ✅ Khách hàng vãng lai (Guest checkout)
- ✅ Khách hàng đã Authentication (Authenticated users)
- ✅ Quản trị viên (Admin management)
- ✅ Quản lý sản phẩm (Products, categories, brands)
- ✅ Quản lý đơn hàng (Orders, payments, shipping)

---

## 📊 Thống Kê Dự Án

| Yếu Tố           | Chi Tiết                                 |
| ---------------- | ---------------------------------------- |
| **Framework**    | ASP.NET Core 10 (.NET 10.0)              |
| **Database**     | SQL Server (SQL Type, Stored Procedures) |
| **Authentication**     | JWT Bearer Tokens                        |
| **Authorization**   | Dựa trên vai trò (Khách, Quản Trị)       |
| **ORM**          | Entity Framework Core 9.0                |
| **Cache**        | Distributed Cache (Memory/Redis)         |
| **Tài Liệu API** | OpenAPI/Scalar                           |
| **Testing**     | xUnit + Moq                              |
| **URL API Base** | http://localhost:8000                    |
| **Hết Hạn JWT**  | 60 phút (mặc định)                       |

---

## 🏗️ Kiến Trúc Lớp (Layered Architecture)

```
┌─────────────────────────────────────┐
│    Presentation Layer                    │
│  (AuthController, CartController,   │
│   ProductController, OrdersController)
└────────────┬────────────────────────┘
             │
┌────────────▼────────────────────────┐
│    Business Logic Layer             │
│  (ICartService, ITokenService,      │
│   IOrderService implementations)    │
└────────────┬────────────────────────┘
             │
┌────────────▼────────────────────────┐
│    Data Mapping Layer              │
│  (AutoMapper, MappingProfile,       │
│   DTOs, Specifications)             │
└────────────┬────────────────────────┘
             │
┌────────────▼────────────────────────┐
│    Data Access Layer            │
│  (EntityFramework Core,             │
│   TgerCameraContext, Models)        │
└────────────┬────────────────────────┘
             │
┌────────────▼────────────────────────┐
│    Lớp Database                     │
│  (SQL Server, Stored Procedures,    │
│   Tables, Relationships)            │
└─────────────────────────────────────┘

Vấn Đề Xuyên Suốt: Middleware (Ghi Nhật Ký, Ngoại Lệ)
```

---

## 📁 Cấu Trúc Thư Mục

```
TgerCamera/
├── Controllers/                      # API endpoints
│   ├── AuthController.cs
│   ├── CartController.cs
│   ├── ProductController.cs
│   ├── OrdersController.cs
│   ├── BrandController.cs
│   ├── CategoryController.cs
│   └── RentalProductController.cs
│
├── Services/                         # Logic kinh doanh
│   ├── ICartService.cs
│   ├── CartService.cs
│   ├── ITokenService.cs
│   ├── TokenService.cs
│   ├── IOrderService.cs
│   └── OrderService.cs
│
├── Models/                           # Thực thể database
│   ├── TgerCameraContext.cs
│   ├── User.cs
│   ├── Product.cs
│   ├── Cart.cs
│   ├── Order.cs
│   ├── Payment.cs
│   └── ... (17 thực thể tổng cộng)
│
├── Dtos/                             # Đối Tượng Truyền Dữ Liệu
│   ├── ProductDto.cs
│   ├── CartDto.cs
│   ├── OrderDto.cs
│   ├── Auth/
│   │   ├── LoginDto.cs
│   │   ├── RegisterDto.cs
│   │   └── AuthResultDto.cs
│   └── Order/
│       ├── OrderDto.cs
│       └── CheckoutDto.cs
│
├── Middleware/                       # Vấn đề xuyên suốt
│   ├── ExceptionHandlingMiddleware.cs
│   └── RequestLoggingMiddleware.cs
│
├── Mapping/                          # Cấu hình AutoMapper
│   └── MappingProfile.cs
│
├── Helpers/                          # Hàm tiện ích
│   └── PasswordHelper.cs
│
├── Migrations/                       # Migrations EF Core
│
└── Program.cs                        # Bootstrap ứng dụng
```

---

## 🔑 Tính Năng Chính

### 1. **Authentication & Authorization**

#### Đăng Ký (Register)

```
POST /api/auth/register
{
  "email": "test@example.com",
  "password": "Test123!",
  "fullName": "Test User",
  "phone": "0123456789"
}
→ Phản Hồi: { "id": 2, "email": "test@example.com", "role": "Customer" }
```

#### Đăng Nhập (Login)

```
POST /api/auth/login
{
  "email": "test@example.com",
  "password": "Test123!"
}
→ Phản Hồi: {
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-03-31T20:30:00"
}
```

**Yêu Cầu Token (JWT Claims):**

- `sub` (Chủ đề): User ID
- `email`: Email người dùng
- `role`: Vai trò người dùng (Khách, Quản Trị)
- `exp`: Hết hạn token

### 2. **Quản Lý Cart (Cart Management)**

#### Giỏ Khách (Using Session)

```
POST /api/cart/items
Headers: Cookie: SessionId=550e8400-e29b-41d4-a716-446655440000
Body: { "productId": 1, "quantity": 2 }
→ Phản Hồi: {
  "sessionId": "550e8400-e29b-41d4-a716-446655440000",
  "items": [ { "productId": 1, "quantity": 2, "product": {...} } ]
}
```

**Lưu Trữ:** Distributed Cache (TTL 24 giờ)

#### Giỏ Người Dùng (Using UserId)

```
POST /api/cart/items
Headers: Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
Body: { "productId": 1, "quantity": 2 }
→ Phản Hồi: {
  "id": 1,
  "userId": 2,
  "items": [ { "id": 10, "productId": 1, "quantity": 2 } ]
}
```

**Lưu Trữ:** SQL Database

#### Tự Động Hợp Nhất Khi Đăng Nhập

```
Khi người dùng đăng nhập:
1. Lấy giỏ khách từ cache (sử dụng sessionId)
2. Lấy/tạo giỏ người dùng từ database
3. Authentication kho hàng cho mỗi mục khách
4. Hợp nhất số lượng (tính tổng nếu sản phẩm tồn tại ở cả hai)
5. Xóa cache khách
```

### 3. **Thanh Toán & Đơn Hàng (Orders)**

#### Thanh Toán (Khách & Người Dùng)

```
POST /api/orders/checkout
Headers: Authorization: Bearer eyJhbGciOiJIUzI1NiIs... (tùy chọn)
Body: {
  "shippingAddressId": 1,
  "paymentMethod": "VNPAY"
}
→ Phản Hồi: {
  "orderId": 6,
  "totalPrice": 70000000,
  "status": "Success",
  "message": "Order created successfully"
}
```

**Quy Trình:**

```
1. Authentication giỏ không trống
2. Authentication kho hàng sản phẩm vẫn có sẵn
3. Authentication địa chỉ giao hàng
4. Tạo Đơn Hàng + OrderItems (thông qua Stored Procedure)
5. Giảm kho hàng sản phẩm
6. Tạo bản ghi thanh toán
7. Xóa giỏ
8. Trả về ID đơn hàng
```

#### Xem Đơn Hàng (Get My Orders)

```
GET /api/orders/my-orders
Headers: Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
→ Phản Hồi: [
  {
    "id": 6,
    "userId": 2,
    "totalPrice": 70000000,
    "status": "Pending",
    "shippingAddressId": 1,
    "createdAt": "2026-03-31T14:30:00",
    "orderItems": [
      { "id": 12, "productId": 4, "quantity": 1, "price": 45000000 }
    ]
  }
]
```

#### Hủy Đơn Hàng (Cancel Order)

```
PUT /api/orders/{id}/cancel
Headers: Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
→ Phản Hồi: 204 No Content
```

**Khi hủy:**

- Trạng thái đơn hàng → "Cancelled"
- Kho hàng sản phẩm → Được khôi phục

#### Cập Nhật Trạng Thái (Admin Only)

```
PUT /api/orders/{id}/status
Headers: Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
Body: { "status": "Processing" }
→ Phản Hồi: 204 No Content

Quy Trình Trạng Thái: Pending → Processing → Shipped → Delivered
```

**Authorization:**

- ✅ Quản Trị (role = "Admin"): Được phép
- ❌ Khách: 403 Forbidden

### 4. **Quản Lý Sản Phẩm**

#### Danh Sách Sản Phẩm

```
GET /api/products?page=1&pageSize=10&category=Mirrorless&brand=Sony
→ Phản Hồi: {
  "data": [ ... ],
  "totalCount": 150,
  "pageSize": 10,
  "totalPages": 15
}
```

#### Chi Tiết Sản Phẩm

```
GET /api/products/1
→ Phản Hồi: {
  "id": 1,
  "name": "Sony A7 III",
  "price": 35000000,
  "stockQuantity": 10,
  "brand": { "id": 1, "name": "Sony" },
  "category": { "id": 5, "name": "Mirrorless" },
  "condition": { "id": 1, "name": "Mới 100%" },
  "specifications": [ ... ]
}
```

---

## 🚀 Cách Chạy Dự Án

### 1. **Chuẩn Bị**

```bash
# Điều hướng đến dự án
cd D:\Github\TgerCamera_Store_API\TgerCamera\TgerCamera

# Khôi phục dependencies
dotnet restore

# Xây dựng dự án
dotnet build
```

### 2. **Database Migration**

```bash
# Áp dụng migrations
dotnet ef database update

# Xác minh bảng
# Mở SQL Server Management Studio → Kết nối → TgerCamera database
```

### 3. **Chạy Server**

```bash
# Bắt đầu máy chủ phát triển
dotnet run

# Máy chủ chạy tại http://localhost:8000
```

### 4. **Truy Cập API Documentation**

```
# OpenAPI / Swagger UI
http://localhost:8000/swagger/ui

# Hoặc Scalar
http://localhost:8000/scalar/v1
```

---

## ✅ Danh Sách Kiểm Tra Triển Khai

### Tính Năng Hoàn Thành

- [x] Authentication & Đăng Nhập
- [x] Quản Lý Giỏ (Khách & Người Dùng)
- [x] Thanh Toán
- [x] Quản Lý Đơn Hàng
- [x] Quản Lý Sản Phẩm
- [x] Quản Lý Nhãn Hiệu & Danh Mục
- [x] Xóa Mềm Sản Phẩm
- [x] Kiểm Soát Truy Cập Dựa Trên Vai Trò
- [x] Authentication Kho Hàng
- [x] Hợp Nhất Giỏ Khách Tự Động

### Kiến Trúc & Thiết Kế

- [x] Kiến Trúc Lớp (5 lớp)
- [x] SOLID Principles
- [x] Tiêm Phụ Thuộc
- [x] AutoMapper
- [x] Entity Framework Core
- [x] Xóa Mềm
- [x] Stored Procedures
- [x] Middleware (Ghi Nhật Ký, Ngoại Lệ)

### Security

- [x] Hash Mật Khẩu PBKDF2
- [x] Token JWT HS256
- [x] Authentication IDOR
- [x] Authentication Dữ Liệu DTO
- [x] Ngăn Chặn SQL Injection
- [x] Authentication Kho Hàng
- [x] Kiểm Tra Authorization

### Testing

- [x] Testing Đơn Vị (xUnit + Moq)
- [x] Hướng Dẫn Testing Thủ Công
- [x] Kịch Bản Testing

### Tài Liệu

- [x] PROJECT_SUMMARY.md
- [x] ARCHITECTURE_REVIEW.md
- [x] SECURITY_AUDIT.md
- [x] FINAL_SUMMARY.md
- [x] CHECKOUT_TEST_GUIDE.md
- [x] Tài Liệu Swagger/Scalar

---

## 🎯 Các Điểm Chính

### 1. **Kiến Trúc Xuất Sắc**

- 5 lớp: Presentation → Business Logic → Data Mapping → Data Access → Database
- Tách Biệt Rõ Ràng Các Vấn Đề (SoC)
- Tuân Thủ SOLID Principles

### 2. **Hiệu Suất**

- Chiến Lược Lưu Trữ Kép: Cache Cho Khách, Database Cho Người Dùng
- Không Có Truy Vấn N+1 (Tải Sẵn)
- Xóa Mềm Giảm Lỗi Ràng Buộc Khóa Ngoài

### 3. **Security**

- Hash Mật Khẩu PBKDF2 (10.000 lần lặp)
- Token JWT An Toàn Với Authentication Đầy Đủ
- Kiểm Tra IDOR Trên Tất Cả Endpoint Người Dùng
- Authentication Toàn Cầu & Authentication Dữ Liệu

### 4. **Độ Tin Cậy**

- Giao Dịch Nguyên Tử (Không Bán Quá Mức)
- Xử Lý Lỗi Toàn Cầu
- Ghi Nhật Ký Chi Tiết Yêu Cầu/Phản Hồi

### 5. **Khả Năng Bảo Trì**

- Mã Sạch & Có Tổ Chức
- Tài Liệu Toàn Diện
- Testing Sẵn Sàng (Đơn Vị + Thủ Công)

---

## 📞 Liên Hệ & Hỗ Trợ

**Phiên Bản:** 1.0  
**Trạng Thái:** Sẵn Sàng Demo  
**Môi Trường**: ASP.NET Core 10 | SQL Server | .NET 10.0

---

**Tài Liệu Được Cung Cấp:**

1. TOM_TAT_DU_AN_VI.md (Bản Tóm Tắt - Tiếng Việt)
2. KIEN_TRUC_REVIEW_VI.md (Kiểm Toán Kiến Trúc - Tiếng Việt)
3. BAO_MAT_REVIEW_VI.md (Kiểm Toán Security - Tiếng Việt)
4. FINAL_SUMMARY.md (Tóm Tắt Cuối - Tiếng Việt, Tài Liệu Demo)
5. CHECKOUT_TEST_GUIDE.md (Hướng Dẫn Testing Thanh Toán)
   dotnet run

# Khi thấy kết quả:

# Now listening on: http://localhost:8000

# API sẵn sàng để Testing

````

### 4. **Testing API**

```bash
# Tùy Chọn A: PowerShell
$response = Invoke-WebRequest `
  -Uri "http://localhost:8000/api/auth/login" `
  -Method POST `
  -ContentType "application/json" `
  -Body '{"email":"test@example.com","password":"Test123!"}'

$response.Content | ConvertFrom-Json

# Tùy Chọn B: cURL
curl -X POST http://localhost:8000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!"}'

# Tùy Chọn C: OpenAPI/Scalar
# Truy cập: http://localhost:8000/scalar/v1
````

---

## ✅ Danh Sách Kiểm Tra Triển Khai

- [ ] Database migrations được áp dụng
- [ ] Người dùng Testing được tạo (test@example.com, admin@example.com)
- [ ] Cấu hình JWT được xác minh (khóa bí mật, hết hạn)
- [ ] Chính sách CORS được cấu hình cho origins production
- [ ] Hash mật khẩu được Testing
- [ ] Xử lý lỗi được Testing (400, 404, 401, 500)
- [ ] Ghi nhật ký được cấu hình (tệp/console)
- [ ] Authentication kho hàng được Testing
- [ ] Quy trình thanh toán được Testing (khách + người dùng)
- [ ] Hủy đơn hàng được Testing
- [ ] Các điểm cuối quản trị viên được Testing
- [ ] SSL/TLS được cấu hình (nếu công khai)

---

**Trạng Thái:** ✅ Sẵn Sàng Cho Demo & Production
