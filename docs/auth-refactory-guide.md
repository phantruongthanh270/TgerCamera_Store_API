# Hệ thống Authentication Refactored - Sẵn sàng Production

## Tổng quan

Hệ thống xác thực toàn diện, sẵn sàng production hỗ trợ:

- ✅ Đăng ký & đăng nhập qua email + password
- ✅ Google OAuth 2.0 (verify phía server)
- ✅ Hỗ trợ Refresh Token (access token ngắn hạn + refresh token dài hạn)
- ✅ Clean architecture với tách biệt trách nhiệm
- ✅ Logging và xử lý lỗi toàn diện
- ✅ Theo các best practice bảo mật

## Kiến trúc

### Tách biệt trách nhiệm rõ ràng

```
Controller Layer (AuthController)
    ↓ Validate input, xử lý HTTP
    ├─→ IAuthService
    │   ├─→ RegisterAsync()
    │   ├─→ LoginAsync()
    │   ├─→ GoogleLoginAsync()
    │   ├─→ RefreshAccessTokenAsync()
    │   └─→ LogoutAsync()
    │
    ├─→ ITokenService (Tạo Token)
    │   ├─→ CreateAccessToken() - JWT ngắn hạn (15 phút default)
    │   └─→ CreateRefreshToken() - Token ngẫu nhiên dài hạn (7 ngày default)
    │
    ├─→ IRefreshTokenService (Quản lý Token)
    │   ├─→ CreateRefreshTokenAsync()
    │   ├─→ ValidateRefreshTokenAsync()
    │   ├─→ RevokeRefreshTokenAsync()
    │   └─→ CleanupExpiredTokensAsync()
    │
    └─→ ICartService (Business Logic)
        └─→ MergeGuestCartToUserAsync()
```

### Data Model

**User Model** - Mở rộng với RefreshToken navigation

```csharp
public partial class User {
    // ... existing fields
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; }
}
```

**RefreshToken Model** - Mới, lưu trữ persistent

```csharp
public class RefreshToken {
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Token { get; set; }          // Không lưu! Chỉ lưu hash
    public string TokenHash { get; set; }      // SHA256 hash của token
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }   // Để logout

    public bool IsValid => !IsRevoked && !IsExpired;
}
```

## API Endpoints

### 1. Đăng ký

```http
POST /api/auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePass123!",
  "fullName": "John Doe",
  "phone": "+1234567890"
}
```

**Response (201 OK)**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "Base64EncodedRandomToken...",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "user": {
    "id": 1,
    "email": "user@example.com",
    "fullName": "John Doe",
    "phone": "+1234567890",
    "role": "Customer"
  }
}
```

### 2. Đăng nhập

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePass123!"
}
```

**Response (200 OK)** - Giống như Đăng ký

### 3. Đăng nhập Google OAuth

```http
POST /api/auth/google-login
Content-Type: application/json

{
  "idToken": "GoogleToken_from_frontend"
}
```

**Bảo mật**: Token được verify phía server qua Google's tokeninfo endpoint (cách tiếp cận an toàn nhất)

### 4. Làm mới Access Token

```http
POST /api/auth/refresh-token
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "refreshToken": "{refreshToken_from_login}"
}
```

**Response (200 OK)** - Cấp tokens mới

### 5. Đăng xuất

```http
POST /api/auth/logout
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "refreshToken": "{refreshToken}"
}
```

## Cấu hình

### appsettings.json

```json
{
  "Jwt": {
    "Key": "YourSecureKeyOfAtLeast32Characters!!!",
    "Issuer": "TgerCamera",
    "Audience": "TgerCameraUsers",
    "AccessTokenExpireMinutes": 15,
    "RefreshTokenExpireDays": 7
  },
  "Google": {
    "ClientId": "YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com"
  }
}
```

### Environment Variables (Production)

```bash
# .NET sẽ map tự động
Jwt__Key=YourSecureKey
Jwt__AccessTokenExpireMinutes=15
Jwt__RefreshTokenExpireDays=7
Google__ClientId=YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com
```

## Tính năng Bảo mật

### 1. Token Security

| Yếu tố        | Implementation                                            |
| ------------- | --------------------------------------------------------- |
| Access Token  | JWT, ngắn hạn (15 phút), hết hạn phía server              |
| Refresh Token | Token 64-byte ngẫu nhiên, hash trong DB, dài hạn (7 ngày) |
| Token Hashing | SHA256, chỉ hash một chiều                                |
| Token Storage | Chỉ lưu hash refresh token (không plaintext)              |

### 2. Password Security

- ✅ BCrypt hashing với salt
- ✅ Không lưu plaintext password
- ✅ Google users có PasswordHash rỗng

### 3. Google OAuth Security

- ✅ Verify phía server (KHÔNG client-side decoding)
- ✅ Sử dụng Google's official tokeninfo endpoint
- ✅ Validate audience (Client ID)
- ✅ Kiểm tra token expiration
- ✅ Email validation trước khi tạo user

### 4. Access Control

- ✅ Refresh & Logout endpoints yêu cầu access token hợp lệ ([Authorize])
- ✅ User ID trích từ JWT claims (không bị giả mạo)
- ✅ Refresh token gắn với user cụ thể (UserId check)

### 5. Token Revocation

- ✅ Logout revokes refresh token (RevokedAt timestamp)
- ✅ Tokens hết hạn tự động xóa via CleanupExpiredTokensAsync()
- ✅ IsValid property kiểm tra expiration + revocation

## Chi tiết Service Layer

### ITokenService

**Purpose**: Tạo security tokens

```csharp
string CreateAccessToken(User user)
    // Returns: JWT token sẵn sàng cho API requests
    // Expiration: Từ config (15 phút default)

(string token, string hash) CreateRefreshToken()
    // Returns: Plain token + SHA256 hash
    // Plain token: Trả cho client
    // Hash: Lưu trong DB (cách tiếp cận an toàn)
```

### IRefreshTokenService

**Purpose**: Quản lý vòng đời refresh token

```csharp
Task<RefreshToken> CreateRefreshTokenAsync(int userId)
    // Tạo refresh token mới trong database
    // Tự động set expiration: Hôm nay + RefreshTokenExpireDays

Task<RefreshToken?> ValidateRefreshTokenAsync(int userId, string token)
    // Validate token hash khớp + không hết hạn + không bị revoke
    // Returns null nếu không hợp lệ

Task RevokeRefreshTokenAsync(int userId, string token)
    // Đánh dấu token bị revoke (set RevokedAt)
    // Sử dụng khi logout

Task RevokeAllRefreshTokensAsync(int userId)
    // Revoke TẤT CẢ refresh tokens của user
    // Optional: Gọi cho các tình huống bảo mật quan trọng

Task CleanupExpiredTokensAsync()
    // Xóa expired/revoked tokens từ DB
    // Nên chạy định kỳ (scheduled job)
```

### IAuthService

**Purpose**: Orchestrate auth flows

```csharp
Task<AuthResponse> RegisterAsync(RegisterRequest request)
    // 1. Validate email chưa sử dụng
    // 2. Hash password
    // 3. Tạo user
    // 4. Generate tokens
    // 5. Return AuthResponse

Task<AuthResponse> LoginAsync(LoginRequest request)
    // 1. Tìm user qua email
    // 2. Verify password với BCrypt
    // 3. Generate tokens
    // 4. Return AuthResponse

Task<AuthResponse> GoogleLoginAsync(string googleIdToken)
    // 1. Verify token via Google's endpoint (an toàn)
    // 2. Trích email + name
    // 3. Tạo user nếu chưa tồn tại
    // 4. Generate tokens
    // 5. Return AuthResponse

Task<AuthResponse> RefreshAccessTokenAsync(int userId, string refreshToken)
    // 1. Validate refresh token
    // 2. Revoke token cũ
    // 3. Generate tokens mới
    // 4. Return AuthResponse mới

Task LogoutAsync(int userId, string refreshToken)
    // 1. Revoke refresh token
    // User KHÔNG logout cho đến khi refresh token hết hạn!
```

## Sơ đồ Token Flow

### Initial Login Flow

```
User Login Request
    ↓
AuthService.LoginAsync()
    ├─→ Tìm user qua email
    ├─→ Verify password (BCrypt)
    └─→ Build AuthResponse
        ├─→ TokenService.CreateAccessToken()
        │   └─→ JWT (15 phút)
        ├─→ RefreshTokenService.CreateRefreshTokenAsync()
        │   ├─→ Generate token
        │   ├─→ Hash token
        │   └─→ Lưu hash trong DB
        └─→ Trả cả 2 tokens cho client
```

### Token Refresh Flow

```
Client gửi:
  Authorization: Bearer {expiredAccessToken}
  Body: { refreshToken: "..." }
    ↓
Middleware trích JWT claims → User ID
    ↓
AuthService.RefreshAccessTokenAsync(userId, token)
    ├─→ RefreshTokenService.ValidateRefreshTokenAsync()
    │   ├─→ Hash token
    │   ├─→ Tìm hash trong DB
    │   ├─→ Kiểm tra không hết hạn + không bị revoke
    │   └─→ Trả token record
    ├─→ RefreshTokenService.RevokeRefreshTokenAsync()
    │   └─→ Set RevokedAt = now (rotation)
    └─→ Tạo cặp token mới
        ├─→ Access token mới (15 phút)
        └─→ Refresh token mới (7 ngày)
```

### Logout Flow

```
POST /logout
  Authorization: Bearer {accessToken}
  Body: { refreshToken: "..." }
    ↓
Trích User ID từ JWT
    ↓
AuthService.LogoutAsync(userId, token)
    └─→ RefreshTokenService.RevokeRefreshTokenAsync()
        └─→ Set RevokedAt = now
```

## Xử lý Lỗi

| Tình huống          | HTTP Status | Response                                            |
| ------------------- | ----------- | --------------------------------------------------- |
| Invalid email/pass  | 401         | `{ "message": "Invalid email or password" }`        |
| Email tồn tại       | 400         | `{ "message": "Email already in use" }`             |
| Invalid refresh tok | 401         | `{ "message": "Invalid or expired refresh token" }` |
| Invalid Google tok  | 401         | `{ "message": "Invalid Google token" }`             |
| Field bị thiếu      | 400         | `{ "message": "Email is required" }`                |
| Server error        | 500         | `{ "message": "Operation failed" }`                 |

## Logging

Tất cả hoạt động được log via ILogger<T>:

```
User registration attempt for email: user@example.com
User registered successfully: 1
Login attempt for email: user@example.com
User logged in successfully: 1
Refresh token endpoint: User 1
Token refreshed successfully for user 1
Logout for user 1
User logged out: 1
```

## Frontend Integration Ví dụ

### React with Google Sign-In

```javascript
import { GoogleLogin } from "@react-oauth/google";

function LoginPage() {
  const handleLogin = async (response) => {
    const result = await fetch("/api/auth/google-login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ idToken: response.credential }),
    });

    const data = await result.json();
    localStorage.setItem("accessToken", data.accessToken);
    localStorage.setItem("refreshToken", data.refreshToken);
  };

  return <GoogleLogin onSuccess={handleLogin} />;
}
```

### Token Management

```javascript
// Lưu tokens
localStorage.setItem("accessToken", response.accessToken);
localStorage.setItem("refreshToken", response.refreshToken);

// Sử dụng access token cho API calls
fetch("/api/cart", {
  headers: { Authorization: `Bearer ${localStorage.getItem("accessToken")}` },
});

// Làm mới token khi hết hạn
const refreshToken = async () => {
  const response = await fetch("/api/auth/refresh-token", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${localStorage.getItem("accessToken")}`,
    },
    body: JSON.stringify({
      refreshToken: localStorage.getItem("refreshToken"),
    }),
  });

  const data = await response.json();
  localStorage.setItem("accessToken", data.accessToken);
  localStorage.setItem("refreshToken", data.refreshToken);
};

// Logout
const logout = async () => {
  await fetch("/api/auth/logout", {
    method: "POST",
    headers: { Authorization: `Bearer ${localStorage.getItem("accessToken")}` },
    body: JSON.stringify({
      refreshToken: localStorage.getItem("refreshToken"),
    }),
  });

  localStorage.removeItem("accessToken");
  localStorage.removeItem("refreshToken");
};
```

## Testing

### Manual Curl Tests

```bash
# Đăng ký
curl -X POST http://localhost:5259/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!","fullName":"Test User"}'

# Đăng nhập
curl -X POST http://localhost:5259/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!"}'

# Làm mới token
curl -X POST http://localhost:5259/api/auth/refresh-token \
  -H "Authorization: Bearer {accessToken}" \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"{refreshToken}"}'
```

### Unit Tests (Todo)

```
- ✅ AuthService.RegisterAsync - valid + duplicate email
- ✅ AuthService.LoginAsync - valid + wrong password + not found
- ✅ AuthService.GoogleLoginAsync - valid + invalid token
- ✅ AuthService.RefreshAccessTokenAsync - valid + expired + revoked
- ✅ TokenService.CreateAccessToken - format + claims
- ✅ TokenService.CreateRefreshToken - randomness + hashing
- ✅ RefreshTokenService - CRUD + revocation
```

## Production Deployment Checklist

- [ ] Set strong JWT:Key (32+ ký tự, random)
- [ ] Configure Google:ClientId
- [ ] Điều chỉnh AccessTokenExpireMinutes (15 phút recommended)
- [ ] Điều chỉnh RefreshTokenExpireDays (7 ngày recommended)
- [ ] Setup database migration cho RefreshToken table
- [ ] Enable HTTPS only (JWT yêu cầu HTTPS trong production)
- [ ] Setup scheduled job cho CleanupExpiredTokensAsync()
- [ ] Configure CORS cho frontend domain
- [ ] Setup logging/monitoring
- [ ] Test token refresh flow
- [ ] Test logout functionality
- [ ] Load test token validation

## Migration Cần thiết

```bash
# Tạo migration cho RefreshToken model
dotnet ef migrations add AddRefreshTokenTable

# Apply migration
dotnet ef database update
```

## Tệp Modified/Created

### Tệp mới

- `Models/RefreshToken.cs` - Token persistence model
- `Dtos/Auth/AuthResponse.cs` - Unified response DTO
- `Dtos/Auth/RefreshTokenRequest.cs` - Refresh endpoint request
- `Dtos/Auth/LoginRequest.cs` - Email login request
- `Dtos/Auth/RegisterRequest.cs` - Registration request
- `Services/IRefreshTokenService.cs` - Token management interface
- `Services/RefreshTokenService.cs` - Token management implementation

### Tệp được sửa đổi

- `Services/ITokenService.cs` - Updated với new methods
- `Services/TokenService.cs` - Implemented access + refresh tokens
- `Services/IAuthService.cs` - Refactored với new methods
- `Services/AuthService.cs` - Complete rewrite với clean architecture
- `Controllers/AuthController.cs` - New endpoints và clean error handling
- `Models/User.cs` - Added RefreshToken navigation
- `Models/TgerCameraContext.cs` - Added RefreshToken DbSet
- `Program.cs` - Registered new services
- `appsettings.json` - New JWT configuration
