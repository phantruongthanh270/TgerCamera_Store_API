# Hướng Dẫn Test API Checkout - Toàn Diện

## 📝 Mục Lục

1. [Chuẩn Bị](#chuẩn-bị)
2. [Test Khách Vãng Lai](#test-khách-vãng-lai)
3. [Test User Đã Xác Thực](#test-user-đã-xác-thực)
4. [Test Lỗi & Edge Cases](#test-lỗi--edge-cases)
5. [Test Admin Functions](#test-admin-functions)
6. [Checklist Validation](#checklist-validation)

---

## 🔧 Chuẩn Bị

### Bước 1: Đảm Bảo Server Đang Chạy

```bash
# Mở terminal, navigate đến project
cd D:\Github\TgerCamera_Store_API\TgerCamera\TgerCamera

# Chạy server
dotnet run

# Khi thấy dòng này, server sẵn sàng:
# Now listening on: http://localhost:8000
```

### Bước 2: Tạo Dữ Liệu Test

**Thêm User Test**:

```sql
-- Chạy trong SQL Server Management Studio
USE TgerCamera;

INSERT INTO Users (Email, PasswordHash, FullName, Phone, Role)
VALUES ('test@example.com', 'hashed_password_here', N'Test User', '0123456789', 'Customer');

INSERT INTO Users (Email, PasswordHash, FullName, Phone, Role)
VALUES ('admin@example.com', 'hashed_password_here', N'Admin User', '0123456789', 'Admin');

-- Kiểm tra
SELECT * FROM Users;
```

**Tạo Shipping Address**:

```sql
-- Lấy UserId từ query trên (giả sử = 2)
INSERT INTO ShippingAddresses (UserId, FullName, Phone, AddressLine, City, District, IsDefault)
VALUES
(2, N'Test User', '0123456789', N'123 Lê Lợi', N'HCM', N'Quận 1', 1),
(NULL, N'Guest Address', '0987654321', N'456 Nguyễn Huệ', N'Hà Nội', N'Ba Đình', 0);

-- Kiểm tra
SELECT * FROM ShippingAddresses;
```

**Kiểm Tra Sản Phẩm Có Tồn Kho**:

```sql
SELECT TOP 10 Id, Name, Price, StockQuantity
FROM Products
WHERE StockQuantity > 0
ORDER BY StockQuantity DESC;
```

### Bước 3: Chuẩn Bị Tools

**Option A: PowerShell (Windows)**

```powershell
# Không cần cài gì thêm, dùng Invoke-WebRequest
$headers = @{ "Content-Type" = "application/json" }
$body = @{ email = "test@example.com"; password = "pass123" } | ConvertTo-Json
Invoke-WebRequest -Uri "http://localhost:8000/api/auth/login" -Headers $headers -Body $body
```

**Option B: cURL (Git Bash / WSL / Windows 10+)**

```bash
curl -X POST http://localhost:8000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"pass123"}'
```

---

## 🧪 Test Khách Vãng Lai

### Kịch Bản 1: Khách Thêm Item & Checkout

#### Bước 1: Thêm Item Vào Giỏ (Khách)

```bash
# PowerShell
$response = Invoke-WebRequest `
  -Uri "http://localhost:8000/api/cart/items" `
  -Method POST `
  -ContentType "application/json" `
  -Body @"
{
  "productId": 1,
  "quantity": 2
}
"@ `
  -ResponseHeadersVariable headers

# Lấy SessionId từ response
$response.Content | ConvertFrom-Json
# Output sẽ chứa SessionId (lưu ý cookie)
```

**Hoặc cURL**:

```bash
curl -X POST http://localhost:8000/api/cart/items \
  -H "Content-Type: application/json" \
  -d '{"productId": 1, "quantity": 2}' \
  -c cookies.txt \
  -i
```

**Dự Kiến Response** (200 OK):

```json
{
  "id": 0,
  "sessionId": "550e8400-e29b-41d4-a716-446655440000",
  "userId": null,
  "items": [
    {
      "id": -1,
      "productId": 1,
      "quantity": 2,
      "product": {
        "id": 1,
        "name": "Sony A7 III",
        "price": 35000000,
        "stockQuantity": 10,
        "description": "Máy ảnh mirrorless full-frame nổi tiếng của Sony",
        "brand": "Sony",
        "category": "Mirrorless",
        "condition": "Mới 100%",
        "mainImageUrl": null,
        "specifications": []
      }
    }
  ]
}
```

✅ **Lưu ý**:

- Lấy `sessionId` từ response
- Lấy cookie `SessionId` (nếu dùng curl: từ cookies.txt)
- Item ID = -1 (negative = guest)

---

#### Bước 2: Checkout (Khách)

```bash
# PowerShell
$sessionId = "550e8400-e29b-41d4-a716-446655440000"  # Từ bước 1

$headers = @{
    "Content-Type" = "application/json"
}

$body = @{
    shippingAddressId = 2
    paymentMethod = "COD"
    sessionId = $sessionId
} | ConvertTo-Json

$response = Invoke-WebRequest `
  -Uri "http://localhost:8000/api/orders/checkout" `
  -Method POST `
  -Headers $headers `
  -Body $body

$response.Content | ConvertFrom-Json
```

**Hoặc cURL**:

```bash
curl -X POST http://localhost:8000/api/orders/checkout \
  -H "Content-Type: application/json" \
  -b cookies.txt \
  -d '{
    "shippingAddressId": 2,
    "paymentMethod": "COD"
  }' \
  -i
```

**Dự Kiến Response** (200 OK):

```json
{
  "orderId": 5,
  "totalPrice": 70000000,
  "status": "Success",
  "message": "Order created successfully"
}
```

✅ **Kiểm Tra**:

- `orderId` được tạo
- `totalPrice` = 35000000 × 2 = 70000000
- Kiểm tra database: `SELECT * FROM Orders WHERE Id = 5;`

---

#### Bước 3: Kiểm Tra Giỏ Được Xóa

```bash
# PowerShell - Lấy giỏ hàng sau checkout
$response = Invoke-WebRequest `
  -Uri "http://localhost:8000/api/cart" `
  -Method GET

$response.Content | ConvertFrom-Json
```

**Dự Kiến Response** (200 OK):

```json
{
  "id": 0,
  "sessionId": "550e8400-e29b-41d4-a716-446655440000",
  "userId": null,
  "items": []  # ← Giỏ trống (được dọn)
}
```

✅ **Lưu ý**: Items mảng rỗng = rất tốt ✅

---

### Kịch Bản 2: Khách Thêm Nhiều Item

```bash
# Thêm item 2
curl -X POST http://localhost:8000/api/cart/items \
  -H "Content-Type: application/json" \
  -b cookies.txt \
  -d '{"productId": 2, "quantity": 1}'

# Thêm item 3
curl -X POST http://localhost:8000/api/cart/items \
  -H "Content-Type: application/json" \
  -b cookies.txt \
  -d '{"productId": 3, "quantity": 3}'

# Lấy giỏ hàng
curl -X GET http://localhost:8000/api/cart -b cookies.txt

# Kết quả: 3 items (id: -1, -2, -3)
```

---

## 🧪 Test User Đã Xác Thực

### Kịch Bản 1: User Đăng Nhập & Checkout

#### Bước 1: Đăng Nhập User

**Trước tiên, tạo user với hashed password**:

```csharp
// Chạy code C# để hash password
using System.Security.Cryptography;
using System.Text;

public static string HashPassword(string password)
{
    using (var sha256 = SHA256.Create())
    {
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }
}

// Hash: "Test123!"
var hash = HashPassword("Test123!");
Console.WriteLine(hash);
// Output: kWi3KX7xH8s/P2n4R5jQ9vM8A1bC2dE3fG4hI5jK6lM7nO8pQ9r=
```

**Cập nhật database**:

```sql
UPDATE Users
SET PasswordHash = 'kWi3KX7xH8s/P2n4R5jQ9vM8A1bC2dE3fG4hI5jK6lM7nO8pQ9r='
WHERE Email = 'test@example.com';
```

**Đăng Nhập**:

```bash
# PowerShell
$response = Invoke-WebRequest `
  -Uri "http://localhost:8000/api/auth/login" `
  -Method POST `
  -ContentType "application/json" `
  -Body @"
{
  "email": "test@example.com",
  "password": "Test123!"
}
"@

$json = $response.Content | ConvertFrom-Json
$token = $json.token

Write-Host "Token: $token"
```

**hoặc cURL**:

```bash
TOKEN=$(curl -X POST http://localhost:8000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!"
  }' | jq -r '.token')

echo "Token: $TOKEN"
```

**Dự Kiến Response** (200 OK):

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2026-03-31T20:30:00"
}
```

✅ **Lưu ý**: Lấy token từ `response.token`

---

#### Bước 2: Thêm Item (User Xác Thực)

```bash
# PowerShell
$token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

$body = @{
    productId = 4
    quantity = 1
} | ConvertTo-Json

$response = Invoke-WebRequest `
  -Uri "http://localhost:8000/api/cart/items" `
  -Method POST `
  -Headers $headers `
  -Body $body

$response.Content | ConvertFrom-Json
```

**Dự Kiến Response** (200 OK):

```json
{
  "id": 1,  # ← ID dương = từ database (user)
  "sessionId": null,
  "userId": 2,
  "items": [
    {
      "id": 10,  # ← ID dương = từ database
      "productId": 4,
      "quantity": 1,
      "product": { ... }
    }
  ]
}
```

✅ **Lưu ý**:

- ID cart = dương (từ DB)
- Item ID = dương (từ DB)
- `userId` = 2 (user đăng nhập)
- Không có `sessionId`

---

#### Bước 3: Checkout (User Xác Thực)

```bash
# PowerShell
$token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

$body = @{
    shippingAddressId = 1
    paymentMethod = "VNPAY"
} | ConvertTo-Json

$response = Invoke-WebRequest `
  -Uri "http://localhost:8000/api/orders/checkout" `
  -Method POST `
  -Headers $headers `
  -Body $body

$response.Content | ConvertFrom-Json
```

**Dự Kiến Response** (200 OK):

```json
{
  "orderId": 6,
  "totalPrice": 45000000,
  "status": "Success",
  "message": "Order created successfully"
}
```

---

#### Bước 4: Lấy Đơn Hàng Của User

```bash
# PowerShell
$token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

$headers = @{ "Authorization" = "Bearer $token" }

$response = Invoke-WebRequest `
  -Uri "http://localhost:8000/api/orders/my-orders" `
  -Method GET `
  -Headers $headers

$response.Content | ConvertFrom-Json
```

**Dự Kiến Response** (200 OK):

```json
[
  {
    "id": 6,
    "userId": 2,
    "totalPrice": 45000000,
    "status": "Pending",
    "shippingAddressId": 1,
    "createdAt": "2026-03-31T14:30:00",
    "orderItems": [
      {
        "id": 12,
        "productId": 4,
        "quantity": 1,
        "price": 45000000
      }
    ]
  }
]
```

---

### Kịch Bản 2: Hủy Đơn Hàng

```bash
# PowerShell
$token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
$orderId = 6

$headers = @{ "Authorization" = "Bearer $token" }

$response = Invoke-WebRequest `
  -Uri "http://localhost:8000/api/orders/$orderId/cancel" `
  -Method PUT `
  -Headers $headers

# Response: 204 No Content (thành công)
```

**Kiểm Tra Đơn Hàng Bị Hủy**:

```bash
curl -X GET http://localhost:8000/api/orders/6 \
  -H "Authorization: Bearer $TOKEN"

# Response sẽ có "status": "Cancelled"
```

---

## 🚨 Test Lỗi & Edge Cases

### Lỗi 1: Giỏ Hàng Trống

```bash
# PowerShell - Tạo user mới không có items
$response = Invoke-WebRequest `
  -Uri "http://localhost:8000/api/orders/checkout" `
  -Method POST `
  -Headers @{ "Authorization" = "Bearer $token" } `
  -Body '{"shippingAddressId":1,"paymentMethod":"COD"}' `
  -ErrorAction Continue

# Response (400 Bad Request):
# { "error": "Cart is empty." }
```

**✅ Kết Quả Mong Đợi**:

- Status: 400 Bad Request
- Message: "Cart is empty."

---

### Lỗi 2: Hết Hàng (Stock = 0)

**Chuẩn Bị**:

```sql
-- Giả sử product ID=5 hiện có 1 cái
UPDATE Products SET StockQuantity = 0 WHERE Id = 5;

-- Verify
SELECT Id, Name, StockQuantity FROM Products WHERE Id = 5;
```

**Test**:

```bash
# Thêm item hết hàng vào giỏ
curl -X POST http://localhost:8000/api/cart/items \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"productId": 5, "quantity": 1}'

# Checkout sẽ lỗi
curl -X POST http://localhost:8000/api/orders/checkout \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"shippingAddressId": 1, "paymentMethod": "VNPAY"}'
```

**Dự Kiến Response** (400 Bad Request):

```json
{
  "error": "Insufficient stock for: Nikon D750 (Only 0 left)"
}
```

**✅ Kết Quả Mong Đợi**:

- Status: 400 Bad Request
- Pesan bao gồm tên sản phẩm + số lượng còn lại

---

### Lỗi 3: Địa Chỉ Giao Hàng Không Hợp Lệ

```bash
# Checkout với shipping address không tồn tại (ID=999)
curl -X POST http://localhost:8000/api/orders/checkout \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "shippingAddressId": 999,
    "paymentMethod": "VNPAY"
  }'
```

**Dự Kiến Response** (400 Bad Request):

```json
{
  "error": "Invalid shipping address."
}
```

---

### Lỗi 4: Vượt Quá Tồn Kho

**Chuẩn Bị**:

```sql
-- Product ID=6 có stock=5
UPDATE Products SET StockQuantity = 5 WHERE Id = 6;
```

**Test**:

```bash
# Thêm 10 item (nhưng chỉ có 5)
curl -X POST http://localhost:8000/api/cart/items \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"productId": 6, "quantity": 10}'

# Checkout
curl -X POST http://localhost:8000/api/orders/checkout \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"shippingAddressId": 1, "paymentMethod": "VNPAY"}'
```

**Dự Kiến Response** (400 Bad Request):

```json
{
  "error": "Insufficient stock for: Sony FE 50mm f/1.8 (Only 5 left)"
}
```

---

### Lỗi 5: Không Được Phép Xem Đơn Hàng Của Người Khác

```bash
# User 2 cố xem order của user 1 (ID=5)
curl -X GET http://localhost:8000/api/orders/5 \
  -H "Authorization: Bearer $TOKEN_USER2"
```

**Dự Kiến Response** (403 Forbidden):

```
HTTP/1.1 403 Forbidden
```

---

## 🔧 Test Admin Functions

### Bước 1: Đăng Nhập Admin

```bash
# Đăng nhập với admin@example.com
curl -X POST http://localhost:8000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@example.com",
    "password": "Admin123!"
  }'

# Lấy ADMIN_TOKEN
```

---

### Test 1: Lấy Tất Cả Đơn Hàng (Admin)

```bash
curl -X GET http://localhost:8000/api/orders \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

**Dự Kiến Response** (200 OK):

```json
[
  {
    "id": 5,
    "userId": null,
    "totalPrice": 70000000,
    "status": "Pending",
    "orderItems": [ ... ]
  },
  {
    "id": 6,
    "userId": 2,
    "totalPrice": 45000000,
    "status": "Pending",
    "orderItems": [ ... ]
  }
]
```

---

### Test 2: Cập Nhật Trạng Thái Order (Admin)

```bash
# Cập nhật order 6: Pending → Processing
curl -X PUT http://localhost:8000/api/orders/6/status \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{"status": "Processing"}'

# Response: 204 No Content
```

**Kiểm Tra**:

```bash
# Lấy chi tiết order
curl -X GET http://localhost:8000/api/orders/6 \
  -H "Authorization: Bearer $ADMIN_TOKEN"

# "status": "Processing" ✅
```

**Cập Nhật Tiếp Tục**:

```bash
# Pending → Processing → Shipped → Delivered
curl -X PUT http://localhost:8000/api/orders/6/status \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{"status": "Shipped"}'

curl -X PUT http://localhost:8000/api/orders/6/status \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{"status": "Delivered"}'
```

---

### Test 3: Permission Check - Customer Không Được Cập Nhật Status

```bash
# User 2 cố cập nhật status (không có role Admin)
curl -X PUT http://localhost:8000/api/orders/6/status \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN_USER2" \
  -d '{"status": "Shipped"}'

# Response: 403 Forbidden ✅
```

---

## ✅ Checklist Validation

Sau khi chạy tất cả test, kiểm tra database:

```sql
-- Kiểm tra Orders được tạo
SELECT Id, UserId, SessionId, TotalPrice, Status, CreatedAt
FROM Orders
ORDER BY CreatedAt DESC
LIMIT 5;

-- Kiểm tra OrderItems
SELECT oi.Id, oi.OrderId, oi.ProductId, oi.Quantity, oi.Price
FROM OrderItems oi
ORDER BY oi.Id DESC
LIMIT 10;

-- Kiểm tra Payments
SELECT Id, OrderId, PaymentMethod, Amount, Status
FROM Payments
ORDER BY Id DESC
LIMIT 5;

-- Kiểm tra Stock Được Cập Nhật
SELECT Id, Name, Price, StockQuantity
FROM Products
WHERE Id IN (1, 2, 3, 4, 5, 6)
ORDER BY Id;

-- Kiểm tra Carts Trống (Guest carts xóa sau checkout)
SELECT * FROM Carts WHERE UserId IS NULL;

-- Kiểm tra CartItems (nên trống)
SELECT * FROM CartItems;
```

**✅ Kết Quả Mong Đợi**:

- [ ] Orders: 6+ records (từ test)
- [ ] OrderItems: 10+ records (tương ứng)
- [ ] Payments: 6+ records (status=Pending cho checkout thành công)
- [ ] Stock: Giảm đúng số lượng trong đơn hàng
- [ ] Carts: Trống hoặc chỉ user carts

---

## 🐛 Log Debugging

Nếu có lỗi, kiểm tra console output:

```
[14:30:45] INFO: Order 6 created successfully for userId: 2, orderId: 6
[14:31:20] WARN: Stock validation failed: Insufficient stock
[14:31:45] ERROR: Error creating order: Index out of range
```

**Common Issues**:

| Lỗi                            | Nguyên Nhân                  | Giải Pháp            |
| ------------------------------ | ---------------------------- | -------------------- |
| "Object reference not set"     | Token null                   | Lấy token đúng cách  |
| "Invalid type 'OrderItemType'" | SQL type không tạo           | Chạy SQL CREATE TYPE |
| "Unauthorized"                 | Sai token/expired            | Đăng nhập lại        |
| "Bad Request"                  | Payload sai                  | Check JSON format    |
| "Stock validation failed"      | Giỏ hàng có sản phẩm deleted | Thêm sản phẩm khác   |

---

**Status**: ✅ Ready for Full Testing
