# TgerCamera Store API - Tài liệu API Toàn bộ

**Base URL:** `http://localhost:5259/api`

**API Version:** 1.0

**Ngày cập nhật:** April 16, 2026

---

## Mục lục

1. [Cấu trúc chung](#cấu-trúc-chung)
2. [Xác thực (Auth API)](#xác-thực-auth-api)
3. [Sản phẩm (Product API)](#sản-phẩm-product-api)
4. [Danh mục (Category API)](#danh-mục-category-api)
5. [Thương hiệu (Brand API)](#thương-hiệu-brand-api)
6. [Điều kiện Sản phẩm (Product Condition API)](#điều-kiện-sản-phẩm-product-condition-api)
7. [Giỏ hàng (Cart API)](#giỏ-hàng-cart-api)
8. [Đơn hàng (Orders API)](#đơn-hàng-orders-api)
9. [Sản phẩm cho Thuê (Rental Product API)](#sản-phẩm-cho-thuê-rental-product-api)
10. [Mã lỗi & Xử lý ngoại lệ](#mã-lỗi--xử-lý-ngoại-lệ)

---

## Cấu trúc chung

### 1. Cấu trúc Response thành công (200, 201)

```json
{
  "data": {},
  "message": "Success",
  "statusCode": 200
}
```

### 2. Cấu trúc Response lỗi

```json
{
  "error": "Error message",
  "statusCode": 400
}
```

### 3. Xác thực

- **Loại:** JWT Bearer Token
- **Header:** `Authorization: Bearer <token>`
- **Token TTL:** 60 phút
- **Refresh:** Cần đăng nhập lại để có token mới

### 4. CORS Policy

- **Cho phép Origin:** Tất cả (`*`)
- **Cho phép Method:** Tất cả
- **Cho phép Header:** Tất cả

### 5. Session & Cookie

- **Session Cookie:** `SessionId` (HttpOnly, 30 ngày TTL)
- **Sử dụng:** Cho guest users (chưa đăng nhập)
- **Distributed Cache:** Dùng cho guest cart (24 giờ TTL)

---

## Xác thực (Auth API)

**Base Endpoint:** `/api/auth`

### 1. Đăng ký người dùng mới

**Endpoint:** `POST /api/auth/register`

**Mô tả:** Tạo tài khoản người dùng mới và tự động hợp nhất giỏ hàng khách vãng lai

**Authentication:** Không cần

**Request Body:**

```json
{
  "email": "user@example.com",
  "password": "password123",
  "fullName": "Tên người dùng"
}
```

**Validation Rules:**

- Email: Bắt buộc, định dạng email hợp lệ
- Password: Bắt buộc, 6-100 ký tự
- FullName: Bắt buộc, 2-100 ký tự

**Response (200):**

```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-04-16T12:30:00Z"
}
```

**Trường hợp lỗi:**

- `400`: Email already in use / Email và password là bắt buộc
- `422`: Validation error

**Ghi chú:**

- Tự động hợp nhất giỏ hàng khách vãng lai nếu có SessionId cookie
- Xóa SessionId cookie sau khi hợp nhất

---

### 2. Đăng nhập

**Endpoint:** `POST /api/auth/login`

**Mô tả:** Xác thực người dùng với email và password, trả về JWT token

**Authentication:** Không cần

**Request Body:**

```json
{
  "email": "user@example.com",
  "password": "password123"
}
```

**Response (200):**

```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-04-16T12:30:00Z"
}
```

**Trường hợp lỗi:**

- `400`: Email và password là bắt buộc
- `401`: Invalid email or password

**Ghi chú:**

- Tự động hợp nhất giỏ hàng khách vãng lai nếu có SessionId cookie

---

## Sản phẩm (Product API)

**Base Endpoint:** `/api/product`

### 1. Lấy danh sách sản phẩm (có phân trang & lọc)

**Endpoint:** `GET /api/product`

**Mô tả:** Lấy danh sách sản phẩm với hỗ trợ lọc, tìm kiếm, sắp xếp và phân trang

**Authentication:** Không cần

**Query Parameters:**

| Tham số       | Kiểu    | Bắt buộc | Mô tả                                          | Mặc định  |
| ------------- | ------- | -------- | ---------------------------------------------- | --------- |
| `brandId`     | int     | Không    | Lọc theo ID thương hiệu                        | -         |
| `categoryId`  | int     | Không    | Lọc theo ID danh mục (hỗ trợ phân cấp cha-con) | -         |
| `conditionId` | int     | Không    | Lọc theo ID điều kiện sản phẩm                 | -         |
| `minPrice`    | decimal | Không    | Giá tối thiểu                                  | -         |
| `maxPrice`    | decimal | Không    | Giá tối đa                                     | -         |
| `q`           | string  | Không    | Tìm kiếm theo tên sản phẩm                     | -         |
| `page`        | int     | Không    | Số trang                                       | 1         |
| `pageSize`    | int     | Không    | Số mục trên trang (max 200)                    | 20        |
| `sortBy`      | string  | Không    | Sắp xếp theo: "price", "name", "createdAt"     | createdAt |
| `sortDir`     | string  | Không    | Hướng sắp xếp: "asc", "desc"                   | desc      |

**Ví dụ Request:**

```
GET /api/product?categoryId=1&minPrice=100&maxPrice=5000&page=1&pageSize=20&sortBy=price&sortDir=asc
```

**Response (200):**

```json
{
  "items": [
    {
      "id": 1,
      "name": "Canon EOS R5",
      "description": "Máy ảnh mirrorless đỉnh cao",
      "price": 3999.99,
      "stockQuantity": 10,
      "brand": {
        "id": 1,
        "name": "Canon"
      },
      "category": {
        "id": 4,
        "name": "Mirrorless"
      },
      "condition": {
        "id": 1,
        "name": "New"
      },
      "mainImageUrl": "https://example.com/image.jpg",
      "specifications": [
        {
          "id": 1,
          "key": "Sensor",
          "value": "Full-Frame"
        }
      ]
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 150,
  "totalPages": 8
}
```

**Phân cấp Danh mục:**

- `categoryId = 1` (Máy ảnh): Bao gồm 1, 4, 5, 10, 11
- `categoryId = 2` (Ống kính): Bao gồm 2, 6, 7
- `categoryId = 3` (Phụ kiện): Bao gồm 3, 8, 9
- `categoryId = 4` (Mirrorless): Bao gồm 4, 10, 11

---

### 3. Tìm kiếm sản phẩm

**Endpoint:** `GET /api/products/search`

**Mô tả:** Tìm kiếm gợi ý sản phẩm theo tên để hỗ trợ autocomplete.

**Authentication:** Không cần

**Query Parameters:**

| Tham số | Kiểu   | Bắt buộc | Mô tả            |
| ------- | ------ | -------- | ---------------- |
| `q`     | string | Có       | Từ khóa tìm kiếm |

**Response (200):**

```json
[
  {
    "id": 1,
    "name": "Canon EOS R5",
    "price": 3999.99,
    "mainImageUrl": "https://example.com/image.jpg"
  },
  {
    "id": 2,
    "name": "Canon EOS R6",
    "price": 2499.99,
    "mainImageUrl": "https://example.com/image2.jpg"
  }
]
```

**Ghi chú:**

- Trả về tối đa 10 kết quả
- Ưu tiên kết quả bắt đầu bằng từ khóa
- Không phân biệt hoa thường
- Nếu `q` rỗng thì trả về mảng rỗng

---

### 4. Lấy chi tiết sản phẩm

**Endpoint:** `GET /api/product/{id}`

**Mô tả:** Lấy thông tin chi tiết của một sản phẩm cụ thể

**Authentication:** Không cần

**Path Parameters:**

| Tham số | Kiểu | Mô tả       |
| ------- | ---- | ----------- |
| `id`    | int  | ID sản phẩm |

**Response (200):**

```json
{
  "id": 1,
  "name": "Canon EOS R5",
  "description": "Máy ảnh mirrorless đỉnh cao",
  "price": 3999.99,
  "stockQuantity": 10,
  "brand": {
    "id": 1,
    "name": "Canon"
  },
  "category": {
    "id": 4,
    "name": "Mirrorless"
  },
  "condition": {
    "id": 1,
    "name": "New"
  },
  "mainImageUrl": "https://example.com/image.jpg",
  "specifications": [
    {
      "id": 1,
      "key": "Sensor",
      "value": "Full-Frame"
    }
  ]
}
```

**Trường hợp lỗi:**

- `404`: Sản phẩm không tìm thấy

---

### 5. Tạo sản phẩm

**Endpoint:** `POST /api/product`

**Mô tả:** Tạo sản phẩm mới (Chỉ Admin)

**Authentication:** Cần JWT Token (Role: Admin)

**Request Body:**

```json
{
  "name": "Canon EOS R5",
  "description": "Máy ảnh mirrorless đỉnh cao",
  "price": 3999.99,
  "stockQuantity": 10,
  "brandId": 1,
  "categoryId": 4,
  "conditionId": 1,
  "mainImageUrl": "https://example.com/image.jpg",
  "specifications": [
    {
      "key": "Sensor",
      "value": "Full-Frame"
    },
    {
      "key": "Megapixels",
      "value": "45MP"
    }
  ]
}
```

**Response (201):**

```json
{
  "id": 1,
  "name": "Canon EOS R5",
  "description": "Máy ảnh mirrorless đỉnh cao",
  "price": 3999.99,
  "stockQuantity": 10,
  "brand": { "id": 1, "name": "Canon" },
  "category": { "id": 4, "name": "Mirrorless" },
  "condition": { "id": 1, "name": "New" },
  "mainImageUrl": "https://example.com/image.jpg",
  "specifications": [ ... ]
}
```

**Trường hợp lỗi:**

- `401`: Chưa xác thực
- `403`: Không phải Admin

---

### 6. Cập nhật sản phẩm

**Endpoint:** `PUT /api/product/{id}`

**Mô tả:** Cập nhật thông tin sản phẩm

**Authentication:** Cần JWT Token

**Path Parameters:**

| Tham số | Kiểu | Mô tả       |
| ------- | ---- | ----------- |
| `id`    | int  | ID sản phẩm |

**Request Body:** (Giống như tạo sản phẩm)

**Response (204):** No Content

**Trường hợp lỗi:**

- `404`: Sản phẩm không tìm thấy

---

### 7. Xóa sản phẩm

**Endpoint:** `DELETE /api/product/{id}`

**Mô tả:** Xóa sản phẩm (Soft Delete - đánh dấu là đã xóa)

**Authentication:** Cần JWT Token

**Path Parameters:**

| Tham số | Kiểu | Mô tả       |
| ------- | ---- | ----------- |
| `id`    | int  | ID sản phẩm |

**Response (204):** No Content

**Trường hợp lỗi:**

- `404`: Sản phẩm không tìm thấy

---

### 8. Thêm Specification cho sản phẩm

**Endpoint:** `POST /api/product/{productId}/specifications`

**Mô tả:** Thêm thông số kỹ thuật mới cho sản phẩm

**Authentication:** Không cần

**Path Parameters:**

| Tham số     | Kiểu | Mô tả       |
| ----------- | ---- | ----------- |
| `productId` | int  | ID sản phẩm |

**Request Body:**

```json
{
  "key": "Weight",
  "value": "738g"
}
```

**Response (201):**

```json
{
  "id": 1,
  "key": "Weight",
  "value": "738g"
}
```

**Trường hợp lỗi:**

- `400`: Key và Value là bắt buộc
- `404`: Sản phẩm không tìm thấy

---

### 9. Cập nhật Specification

**Endpoint:** `PUT /api/product/{productId}/specifications/{id}`

**Authentication:** Không cần

**Request Body:**

```json
{
  "key": "Weight",
  "value": "750g"
}
```

**Response (204):** No Content

---

### 10. Xóa Specification

**Endpoint:** `DELETE /api/product/{productId}/specifications/{id}`

**Response (204):** No Content

---

## Danh mục (Category API)

**Base Endpoint:** `/api/category`

### 1. Lấy tất cả danh mục

**Endpoint:** `GET /api/category`

**Mô tả:** Lấy danh sách tất cả danh mục sản phẩm

**Authentication:** Không cần

**Response (200):**

```json
[
  {
    "id": 1,
    "name": "Máy ảnh",
    "description": "Các loại máy ảnh"
  },
  {
    "id": 2,
    "name": "Ống kính",
    "description": "Ống kính máy ảnh"
  },
  {
    "id": 3,
    "name": "Phụ kiện",
    "description": "Phụ kiện camera"
  }
]
```

---

## Thương hiệu (Brand API)

**Base Endpoint:** `/api/brand`

### 1. Lấy tất cả thương hiệu

**Endpoint:** `GET /api/brand`

**Mô tả:** Lấy danh sách tất cả thương hiệu

**Authentication:** Không cần

**Response (200):**

```json
[
  {
    "id": 1,
    "name": "Canon"
  },
  {
    "id": 2,
    "name": "Nikon"
  },
  {
    "id": 3,
    "name": "Sony"
  }
]
```

---

## Điều kiện Sản phẩm (Product Condition API)

**Base Endpoint:** `/api/product-conditions`

### 1. Lấy tất cả điều kiện sản phẩm

**Endpoint:** `GET /api/product-conditions`

**Mô tả:** Lấy danh sách tất cả các điều kiện sản phẩm

**Authentication:** Không cần

**Response (200):**

```json
[
  {
    "id": 1,
    "name": "New"
  },
  {
    "id": 2,
    "name": "Refurbished"
  },
  {
    "id": 3,
    "name": "Used"
  }
]
```

---

## Giỏ hàng (Cart API)

**Base Endpoint:** `/api/cart`

**Cấu trúc:**

- **Guest Cart:** Lưu trong Distributed Cache (Redis/MemoryCache), 24h TTL
- **User Cart:** Lưu trong SQL Server Database
- **Merge Logic:** Khi người dùng đăng nhập, giỏ hàng khách vãng lai sẽ hợp nhất với giỏ hàng người dùng

**Item ID:**

- **Guest Items:** ID âm (< 0)
- **User Items:** ID dương (> 0)

### 1. Lấy giỏ hàng hiện tại

**Endpoint:** `GET /api/cart`

**Mô tả:** Lấy giỏ hàng của người dùng hiện tại hoặc khách vãng lai

**Authentication:** Không cần

**Flow:**

1. Nếu chưa đăng nhập: trả về giỏ hàng từ cache (hoặc tạo mới)
2. Nếu đã đăng nhập:
   - Hợp nhất giỏ hàng khách vãng lai (nếu có)
   - Xóa SessionId cookie
   - Trả về giỏ hàng từ Database

**Response (200):**

```json
{
  "id": 1,
  "sessionId": "guid-string",
  "userId": 1,
  "items": [
    {
      "id": 1,
      "productId": 5,
      "productName": "Canon EOS R5",
      "quantity": 1,
      "price": 3999.99,
      "totalPrice": 3999.99
    }
  ]
}
```

---

### 2. Thêm sản phẩm vào giỏ hàng

**Endpoint:** `POST /api/cart/items`

**Mô tả:** Thêm sản phẩm vào giỏ hàng hoặc tăng số lượng nếu sản phẩm đã tồn tại

**Authentication:** Không cần

**Request Body:**

```json
{
  "productId": 5,
  "quantity": 1
}
```

**Validation:**

- Quantity > 0
- Sản phẩm phải tồn tại và không bị xóa
- Stock phải đủ
- Tổng số lượng sau khi thêm không vượt quá stock

**Response (200):**

```json
{
  "id": 1,
  "sessionId": "guid-string",
  "userId": null,
  "items": [ ... ]
}
```

**Trường hợp lỗi:**

- `400`: Quantity must be > 0 / Product not found / Insufficient stock

---

### 3. Cập nhật số lượng sản phẩm trong giỏ hàng

**Endpoint:** `PUT /api/cart/items/{id}`

**Mô tả:** Cập nhật số lượng của sản phẩm trong giỏ hàng

**Authentication:** Không cần

**Path Parameters:**

| Tham số | Kiểu | Mô tả                                          |
| ------- | ---- | ---------------------------------------------- |
| `id`    | int  | ID mục giỏ hàng (âm cho guest, dương cho user) |

**Request Body:**

```json
{
  "quantity": 2
}
```

**Validation:**

- Quantity > 0
- Quantity không vượt quá stock

**Response (204):** No Content

**Trường hợp lỗi:**

- `400`: Invalid quantity / Exceeds stock
- `404`: Cart item not found
- `401`: SessionId required for guest cart

---

### 4. Xóa sản phẩm khỏi giỏ hàng

**Endpoint:** `DELETE /api/cart/items/{id}`

**Mô tả:** Xóa sản phẩm khỏi giỏ hàng

**Authentication:** Không cần

**Path Parameters:**

| Tham số | Kiểu | Mô tả           |
| ------- | ---- | --------------- |
| `id`    | int  | ID mục giỏ hàng |

**Response (204):** No Content

**Trường hợp lỗi:**

- `404`: Cart item not found

---

## Đơn hàng (Orders API)

**Base Endpoint:** `/api/orders`

### 1. Checkout / Tạo đơn hàng

**Endpoint:** `POST /api/orders/checkout`

**Mô tả:** Xử lý thanh toán và tạo đơn hàng từ giỏ hàng (Sử dụng Stored Procedure)

**Authentication:** Không cần (nhưng hỗ trợ cả authenticated và guest)

**Request Body:**

```json
{
  "shippingAddressId": 1,
  "paymentMethod": "COD"
}
```

**Lưu ý:**

- `paymentMethod`: "COD", "VNPAY", etc.
- Giỏ hàng sẽ được xóa sau khi tạo đơn hàng thành công

**Flow:**

1. Xác định user (nếu đã đăng nhập)
2. Lấy SessionId từ cookie (cho guest)
3. Lấy các mục giỏ hàng
4. Xác thực địa chỉ giao hàng
5. Gọi stored procedure `sp_CreateOrder`
6. Xóa giỏ hàng của khách sau khi thành công
7. Xóa SessionId cookie

**Response (200):**

```json
{
  "orderId": 1,
  "totalPrice": 3999.99,
  "status": "Success",
  "message": ""
}
```

**Trường hợp lỗi:**

- `400`: Cart is empty / Invalid shipping address
- `500`: An error occurred while processing your order

---

### 2. Lấy các đơn hàng của người dùng hiện tại

**Endpoint:** `GET /api/orders/my-orders`

**Mô tả:** Lấy danh sách tất cả đơn hàng của người dùng đã xác thực

**Authentication:** Cần JWT Token

**Response (200):**

```json
[
  {
    "id": 1,
    "userId": 1,
    "totalPrice": 3999.99,
    "status": "Pending",
    "createdAt": "2026-04-16T10:30:00Z",
    "items": [
      {
        "id": 1,
        "productId": 5,
        "productName": "Canon EOS R5",
        "quantity": 1,
        "price": 3999.99
      }
    ]
  }
]
```

**Trường hợp lỗi:**

- `401`: Chưa xác thực

---

### 3. Lấy tất cả đơn hàng

**Endpoint:** `GET /api/orders`

**Mô tả:** Lấy danh sách tất cả đơn hàng trong hệ thống (Chỉ Admin)

**Authentication:** Cần JWT Token (Role: Admin)

**Response (200):**

```json
[
  {
    "id": 1,
    "userId": 1,
    "totalPrice": 3999.99,
    "status": "Pending",
    "createdAt": "2026-04-16T10:30:00Z",
    "items": [ ... ]
  }
]
```

**Trường hợp lỗi:**

- `401`: Chưa xác thực
- `403`: Không phải Admin

---

### 4. Lấy chi tiết đơn hàng

**Endpoint:** `GET /api/orders/{id}`

**Mô tả:** Lấy thông tin chi tiết của một đơn hàng

**Authentication:** Cần JWT Token

**Path Parameters:**

| Tham số | Kiểu | Mô tả       |
| ------- | ---- | ----------- |
| `id`    | int  | ID đơn hàng |

**Response (200):**

```json
{
  "id": 1,
  "userId": 1,
  "totalPrice": 3999.99,
  "status": "Pending",
  "createdAt": "2026-04-16T10:30:00Z",
  "items": [
    {
      "id": 1,
      "productId": 5,
      "productName": "Canon EOS R5",
      "quantity": 1,
      "price": 3999.99
    }
  ]
}
```

**Quyền:**

- User: Chỉ xem được đơn hàng của họ
- Admin: Xem được tất cả đơn hàng

**Trường hợp lỗi:**

- `404`: Đơn hàng không tìm thấy
- `403`: Không có quyền xem đơn hàng này

---

### 5. Cập nhật trạng thái đơn hàng

**Endpoint:** `PUT /api/orders/{id}/status`

**Mô tả:** Cập nhật trạng thái đơn hàng (Chỉ Admin)

**Authentication:** Cần JWT Token (Role: Admin)

**Path Parameters:**

| Tham số | Kiểu | Mô tả       |
| ------- | ---- | ----------- |
| `id`    | int  | ID đơn hàng |

**Request Body:**

```json
{
  "status": "Processing"
}
```

**Các trạng thái hợp lệ:**

- "Pending"
- "Processing"
- "Shipped"
- "Delivered"
- "Cancelled"

**Response (204):** No Content

**Trường hợp lỗi:**

- `404`: Đơn hàng không tìm thấy

---

### 6. Hủy đơn hàng

**Endpoint:** `PUT /api/orders/{id}/cancel`

**Mô tả:** Hủy đơn hàng (Chỉ hủy được đơn hàng có trạng thái "Pending")

**Authentication:** Cần JWT Token

**Path Parameters:**

| Tham số | Kiểu | Mô tả       |
| ------- | ---- | ----------- |
| `id`    | int  | ID đơn hàng |

**Response (204):** No Content

**Quyền:**

- User: Chỉ hủy được đơn hàng của họ
- Admin: Hủy được bất kỳ đơn hàng nào

**Hiệu ứng:**

- Stock sẽ được hoàn lại

**Trường hợp lỗi:**

- `404`: Đơn hàng không tìm thấy
- `400`: Only pending orders can be cancelled
- `403`: Không có quyền hủy đơn hàng này

---

## Sản phẩm cho Thuê (Rental Product API)

**Base Endpoint:** `/api/rentalproduct`

### 1. Lấy tất cả sản phẩm cho thuê

**Endpoint:** `GET /api/rentalproduct`

**Mô tả:** Lấy danh sách tất cả sản phẩm có sẵn để thuê

**Authentication:** Không cần

**Response (200):**

```json
[
  {
    "id": 1,
    "productId": 5,
    "productName": "Canon EOS R5",
    "dailyRate": 50.0,
    "weeklyRate": 300.0,
    "monthlyRate": 1000.0,
    "availabilityStatus": "Available",
    "totalQuantity": 10,
    "rentedQuantity": 2
  }
]
```

---

### 2. Lấy chi tiết sản phẩm cho thuê

**Endpoint:** `GET /api/rentalproduct/{id}`

**Mô tả:** Lấy thông tin chi tiết của sản phẩm cho thuê

**Authentication:** Không cần

**Path Parameters:**

| Tham số | Kiểu | Mô tả                |
| ------- | ---- | -------------------- |
| `id`    | int  | ID sản phẩm cho thuê |

**Response (200):**

```json
{
  "id": 1,
  "productId": 5,
  "productName": "Canon EOS R5",
  "dailyRate": 50.0,
  "weeklyRate": 300.0,
  "monthlyRate": 1000.0,
  "availabilityStatus": "Available",
  "totalQuantity": 10,
  "rentedQuantity": 2
}
```

**Trường hợp lỗi:**

- `404`: Sản phẩm cho thuê không tìm thấy

---

### 3. Tạo sản phẩm cho thuê

**Endpoint:** `POST /api/rentalproduct`

**Mô tả:** Tạo sản phẩm cho thuê mới (Chỉ Admin)

**Authentication:** Cần JWT Token (Role: Admin)

**Request Body:**

```json
{
  "productId": 5,
  "dailyRate": 50.0,
  "weeklyRate": 300.0,
  "monthlyRate": 1000.0,
  "availabilityStatus": "Available",
  "totalQuantity": 10,
  "rentedQuantity": 0
}
```

**Response (201):**

```json
{
  "id": 1,
  "productId": 5,
  "dailyRate": 50.0,
  "weeklyRate": 300.0,
  "monthlyRate": 1000.0,
  "availabilityStatus": "Available",
  "totalQuantity": 10,
  "rentedQuantity": 0
}
```

**Trường hợp lỗi:**

- `401`: Chưa xác thực
- `403`: Không phải Admin

---

### 4. Cập nhật sản phẩm cho thuê

**Endpoint:** `PUT /api/rentalproduct/{id}`

**Mô tả:** Cập nhật thông tin sản phẩm cho thuê (Chỉ Admin)

**Authentication:** Cần JWT Token (Role: Admin)

**Path Parameters:**

| Tham số | Kiểu | Mô tả                |
| ------- | ---- | -------------------- |
| `id`    | int  | ID sản phẩm cho thuê |

**Request Body:** (Giống như tạo sản phẩm cho thuê)

**Response (204):** No Content

**Trường hợp lỗi:**

- `404`: Sản phẩm cho thuê không tìm thấy

---

### 5. Xóa sản phẩm cho thuê

**Endpoint:** `DELETE /api/rentalproduct/{id}`

**Mô tả:** Xóa sản phẩm cho thuê (Soft Delete - Chỉ Admin)

**Authentication:** Cần JWT Token (Role: Admin)

**Path Parameters:**

| Tham số | Kiểu | Mô tả                |
| ------- | ---- | -------------------- |
| `id`    | int  | ID sản phẩm cho thuê |

**Response (204):** No Content

**Trường hợp lỗi:**

- `404`: Sản phẩm cho thuê không tìm thấy

---

## Mã lỗi & Xử lý ngoại lệ

### HTTP Status Codes

| Mã    | Mô tả                                                  |
| ----- | ------------------------------------------------------ |
| `200` | OK - Thành công                                        |
| `201` | Created - Resource được tạo thành công                 |
| `204` | No Content - Thành công nhưng không có nội dung trả về |
| `400` | Bad Request - Request không hợp lệ                     |
| `401` | Unauthorized - Chưa xác thực hoặc token không hợp lệ   |
| `403` | Forbidden - Không có quyền truy cập                    |
| `404` | Not Found - Resource không tìm thấy                    |
| `422` | Unprocessable Entity - Validation error                |
| `500` | Internal Server Error - Lỗi server                     |

### Error Response Format

```json
{
  "error": "Error message",
  "statusCode": 400
}
```

### Global Exception Handling

Tất cả các ngoại lệ sẽ được bắt lại bởi `ExceptionHandlingMiddleware` và trả về response có cấu trúc nhất quán.

---

## Ghi chú quan trọng

### 1. CORS & Cross-Origin

API cho phép tất cả origins, methods, và headers. Có thể cấu hình lại tại `Program.cs`

### 2. JWT Authentication

- **Token Expiration:** 60 phút
- **Refresh:** Cần đăng nhập lại
- **Issuer:** "TgerCamera"
- **Audience:** "TgerCameraUsers"

### 3. Cart Management

- **Guest Cart:** Lưu trong cache, TTL 24h
- **User Cart:** Lưu trong database
- **Session Cookie:** HttpOnly, 30 ngày TTL
- **Merge:** Tự động khi user đăng nhập

### 4. Soft Delete

Tất cả các entity đều hỗ trợ soft delete (đánh dấu IsDeleted thay vì xóa thực):

- Product
- RentalProduct
- Brand
- Category
- Order

### 5. Pagination

- **Default pageSize:** 20
- **Max pageSize:** 200
- **Default page:** 1

### 6. Request Logging

Tất cả requests sẽ được log bởi `RequestLoggingMiddleware`

### 7. Database

- **Type:** SQL Server
- **Connection String:** Xem appsettings.json
- **Migrations:** Đã áp dụng soft delete migration

---

## Ví dụ sử dụng từ Frontend

### 1. Đăng ký & Đăng nhập

```javascript
// Đăng ký
const response = await fetch("http://localhost:5259/api/auth/register", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    email: "user@example.com",
    password: "password123",
    fullName: "User Name",
  }),
});
const data = await response.json();
localStorage.setItem("token", data.token);

// Đăng nhập
const response = await fetch("http://localhost:5259/api/auth/login", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    email: "user@example.com",
    password: "password123",
  }),
});
const data = await response.json();
localStorage.setItem("token", data.token);
```

### 2. Lấy danh sách sản phẩm

```javascript
const response = await fetch(
  "http://localhost:5259/api/product?categoryId=1&page=1&pageSize=20",
);
const data = await response.json();
console.log(data);
```

### 3. Thêm vào giỏ hàng

```javascript
const token = localStorage.getItem("token");
const response = await fetch("http://localhost:5259/api/cart/items", {
  method: "POST",
  headers: {
    "Content-Type": "application/json",
    Authorization: `Bearer ${token}`,
  },
  body: JSON.stringify({
    productId: 5,
    quantity: 1,
  }),
});
const data = await response.json();
console.log(data);
```

### 4. Checkout

```javascript
const token = localStorage.getItem("token");
const response = await fetch("http://localhost:5259/api/orders/checkout", {
  method: "POST",
  headers: {
    "Content-Type": "application/json",
    Authorization: `Bearer ${token}`,
  },
  body: JSON.stringify({
    shippingAddressId: 1,
    paymentMethod: "COD",
  }),
});
const data = await response.json();
console.log("Order ID:", data.orderId);
console.log("Total Price:", data.totalPrice);
```

---

## Liên hệ & Hỗ trợ

Nếu có bất kỳ câu hỏi hoặc cần hỗ trợ, vui lòng liên hệ với nhóm phát triển backend.

**Cập nhật lần cuối:** 16/04/2026
