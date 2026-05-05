using System;

namespace TgerCamera.Models;

public class RefreshToken
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Token { get; set; } = null!;

    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public bool IsRevoked => RevokedAt != null;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public bool IsValid => !IsRevoked && !IsExpired;

    public virtual User User { get; set; } = null!;
}
