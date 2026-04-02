# Các Endpoint Giỏ Hàng - Xử Lý Item Khách & User

## 🔑 **Khái Niệm Chính: ID Item**

- **ID Âm** (`-1, -2, -3`): Item giỏ khách (lưu trong cache)
- **ID Dương** (`1, 2, 3`): Item giỏ user (lưu trong database)

---

## 📋 **Các Endpoint API**

### **POST /api/cart/items** - Thêm Sản Phẩm

**Cho cả Khách & User**

```bash
POST http://localhost:8000/api/cart/items
Content-Type: application/json

{
  "productId": 1,
  "quantity": 2
}
```

**Phản Hồi** (Khách):

```json
{
  "id": 0,
  "sessionId": "uuid-xxx",
  "userId": null,
  "items": [
    {
      "id": -1,        ← ID âm (cache)
      "productId": 1,
      "quantity": 2,
      "product": { ... }
    }
  ]
}
```

**Phản Hồi** (User):

```json
{
  "id": 1,
  "sessionId": null,
  "userId": 5,
  "items": [
    {
      "id": 10,       ← ID dương (DB)
      "productId": 1,
      "quantity": 2,
      "product": { ... }
    }
  ]
}
```

---

### **PUT /api/cart/items/{id}** - Cập Nhật Số Lượng

#### **Trường Hợp 1: Giỏ Khách (ID < 0)**

```bash
PUT http://localhost:8000/api/cart/items/-1
Content-Type: application/json
Cookie: SessionId=uuid-xxx

{
  "quantity": 5
}
```

**✅ Thành Công**: 204 No Content
**❌ Lỗi (404)**: Item không tìm thấy hoặc SessionId không khớp
**❌ Lỗi (400)**: quantity ≤ 0 hoặc vượt quá tồn kho

---

#### **Trường Hợp 2: Giỏ User (ID > 0)**

```bash
PUT http://localhost:8000/api/cart/items/10
Content-Type: application/json
Authorization: Bearer {JWT_TOKEN}

{
  "quantity": 5
}
```

**✅ Thành Công**: 204 No Content
**❌ Lỗi (401)**: Item không thuộc giỏ hàng của user
**❌ Lỗi (404)**: Item không tìm thấy
**❌ Lỗi (400)**: quantity ≤ 0 hoặc vượt quá tồn kho

---

### **DELETE /api/cart/items/{id}** - Xóa Item

#### **Trường Hợp 1: Giỏ Khách (ID < 0)**

```bash
DELETE http://localhost:8000/api/cart/items/-1
Cookie: SessionId=uuid-xxx
```

**✅ Thành Công**: 204 No Content
**❌ Lỗi (404)**: Item không tìm thấy

---

#### **Trường Hợp 2: Giỏ User (ID > 0)**

```bash
DELETE http://localhost:8000/api/cart/items/10
Authorization: Bearer {JWT_TOKEN}
```

**✅ Thành Công**: 204 No Content
**❌ Lỗi (401)**: Item không thuộc giỏ hàng của user
**❌ Lỗi (404)**: Item không tìm thấy

---

## 🧪 **Kịch Bản Test Hoàn Chỉnh**

### **Bước 1: Thêm Item Là Khách**

```bash
curl -X POST http://localhost:8000/api/cart/items \
  -H "Content-Type: application/json" \
  -d '{"productId": 1, "quantity": 2}' \
  -c cookies.txt

# Phản hồi: Item có id: -1, lưu vào cookies.txt (SessionId)
```

### **Bước 2: Cập Nhật Item Khách**

```bash
curl -X PUT http://localhost:8000/api/cart/items/-1 \
  -H "Content-Type: application/json" \
  -d '{"quantity": 5}' \
  -b cookies.txt

# ✅ 204 No Content
```

### **Bước 3: Xóa Item Khách**

```bash
curl -X DELETE http://localhost:8000/api/cart/items/-1 \
  -b cookies.txt

# ✅ 204 No Content
```

### **Bước 4: Đăng Ký User**

```bash
curl -X POST http://localhost:8000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!",
    "fullName": "Test User"
  }' \
  -c auth_cookies.txt

# Phản hồi: { "token": "eyJ0eXA...", "expiresAt": "..." }
# Lưu token cho các request tiếp theo
```

### **Bước 5: Thêm Item Là User**

```bash
TOKEN="eyJ0eXA..."

curl -X POST http://localhost:8000/api/cart/items \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"productId": 2, "quantity": 1}'

# Phản hồi: Item có id: 1 (từ database)
```

### **Bước 6: Cập Nhật Item User**

```bash
TOKEN="eyJ0eXA..."

curl -X PUT http://localhost:8000/api/cart/items/1 \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"quantity": 3}'

# ✅ 204 No Content
```

### **Bước 7: Xóa Item User**

```bash
TOKEN="eyJ0eXA..."

curl -X DELETE http://localhost:8000/api/cart/items/1 \
  -H "Authorization: Bearer $TOKEN"

# ✅ 204 No Content
```

---

## 🎯 **Thay Đổi Chính Trong PUT/DELETE**

### **Trước (Bị Lỗi)**

```csharp
[HttpPut("items/{id}")]
public async Task<IActionResult> UpdateItem(int id, [FromBody] CartItemUpdateDto dto)
{
    // ❌ Luôn query database
    var item = await _context.CartItems.FindAsync(id);

    // ❌ Thất bại với ID < 0 (không có trong DB)
    if (item == null) return NotFound();

    // ...cập nhật trong DB
}
```

### **Sau (Đã Sửa)**

```csharp
[HttpPut("items/{id}")]
public async Task<IActionResult> UpdateItem(int id, [FromBody] CartItemUpdateDto dto)
{
    // ✅ Route dựa trên dấu ID
    if (id < 0)
    {
        // ✅ Xử lý giỏ khách (cache)
        var guestCart = await _cartService.GetGuestCartAsync(sessionId);
        var item = guestCart.Items.FirstOrDefault(i => i.Id == id);
        if (item != null)
        {
            item.Quantity = dto.Quantity;
            await _cartService.SaveGuestCartAsync(sessionId, guestCart);
        }
    }
    else
    {
        // ✅ Xử lý giỏ user (DB)
        var item = await _context.CartItems.FindAsync(id);
        if (item != null)
        {
            item.Quantity = dto.Quantity;
            _context.CartItems.Update(item);
            await _context.SaveChangesAsync();
        }
    }
}
```

---

## ✅ **Quy Tắc Validation**

| Thao Tác        | Khách (ID < 0)   | User (ID > 0)  |
| --------------- | ---------------- | -------------- |
| **PUT**         | Session bắt buộc | JWT bắt buộc   |
| **DELETE**      | Session bắt buộc | JWT bắt buộc   |
| **Sở Hữu Item** | SessionId khớp   | UserId khớp    |
| **Số Lượng**    | > 0, ≤ tồn kho   | > 0, ≤ tồn kho |

---

## 🔍 **Mẹo Gỡ Lỗi**

### **Q: Getting 404 trên PUT?**

A: Kiểm tra ID là âm (`-1`) cho khách, dương (`1`) cho user

### **Q: Getting 401 (Unauthorized)?**

A: Xác minh JWT token trong Authorization header hoặc SessionId trong cookie

### **Q: Getting 400 (Bad Request)?**

A: Đảm bảo quantity > 0 và sản phẩm còn tồn kho

### **Q: Nội dung hiển thị rỗng?**

A: Đảm bảo JSON body hợp lệ:

```json
{
  "quantity": 5
}
```

KHÔNG:

```json
{
  "quantity":
}
```

---

## 📊 **Bộ Sơ Đồ ID**

| Kịch Bản              | Item ID | Lưu Trữ  | Endpoint         |
| --------------------- | ------- | -------- | ---------------- |
| Khách thêm item 1     | -1      | Cache    | POST /items      |
| Khách thêm item 2     | -2      | Cache    | POST /items      |
| Khách cập nhật item 1 | -1      | Cache    | PUT /items/-1    |
| Khách xóa item 1      | -1      | Cache    | DELETE /items/-1 |
| User đăng nhập        | merge   | Cache→DB | (tự động)        |
| User thêm item        | 1       | DB       | POST /items      |
| User cập nhật item    | 1       | DB       | PUT /items/1     |
| User xóa item         | 1       | DB       | DELETE /items/1  |

---

**Trạng Thái**: ✅ Đã Sửa & Sẵn Sàng Test
