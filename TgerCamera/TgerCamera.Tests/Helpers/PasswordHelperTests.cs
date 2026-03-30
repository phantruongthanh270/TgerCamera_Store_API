using System.Security.Cryptography;
using TgerCamera.Helpers;
using Xunit;

namespace TgerCamera.Tests.Helpers;

public class PasswordHelperTests
{
    [Fact]
    public void HashPassword_ShouldReturnHashWithSalt()
    {
        // Arrange
        string password = "TestPassword123!";

        // Act
        string hash = PasswordHelper.HashPassword(password);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        Assert.Contains(".", hash); // Should contain salt.hash format
    }

    [Fact]
    public void HashPassword_ShouldGenerateDifferentHashesForSamePassword()
    {
        // Arrange
        string password = "TestPassword123!";

        // Act
        string hash1 = PasswordHelper.HashPassword(password);
        string hash2 = PasswordHelper.HashPassword(password);

        // Assert
        Assert.NotEqual(hash1, hash2); // Different salts should produce different hashes
    }

    [Fact]
    public void VerifyPassword_ShouldReturnTrueForCorrectPassword()
    {
        // Arrange
        string password = "TestPassword123!";
        string hash = PasswordHelper.HashPassword(password);

        // Act
        bool result = PasswordHelper.VerifyPassword(hash, password);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifyPassword_ShouldReturnFalseForIncorrectPassword()
    {
        // Arrange
        string password = "TestPassword123!";
        string wrongPassword = "WrongPassword123!";
        string hash = PasswordHelper.HashPassword(password);

        // Act
        bool result = PasswordHelper.VerifyPassword(hash, wrongPassword);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_ShouldReturnFalseForInvalidHashFormat()
    {
        // Arrange
        string invalidHash = "invalid_format_without_dot";
        string password = "TestPassword123!";

        // Act
        bool result = PasswordHelper.VerifyPassword(invalidHash, password);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    [InlineData(null)]
    public void HashPassword_ShouldHandleEdgeCases(string password)
    {
        // Act & Assert
        if (string.IsNullOrEmpty(password))
        {
            Assert.Throws<ArgumentException>(() => PasswordHelper.HashPassword(password ?? ""));
        }
        else
        {
            var hash = PasswordHelper.HashPassword(password);
            Assert.NotNull(hash);
        }
    }
}
