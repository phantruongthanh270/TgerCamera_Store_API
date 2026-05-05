using Microsoft.Extensions.Caching.Distributed;
using Moq;
using TgerCamera.Dtos;
using TgerCamera.Services;

namespace TgerCamera.Tests.Services;

public class CartServiceTests
{
    [Fact]
    public async Task GetGuestCartAsync_WithEmptySessionId_ShouldReturnNull()
    {
        var service = CreateService();

        var result = await service.GetGuestCartAsync(string.Empty);

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveGuestCartAsync_ThenGetGuestCartAsync_ShouldRoundTripCart()
    {
        var service = CreateService();
        var cart = new CartDto
        {
            SessionId = "guest-session-123",
            Items =
            [
                new CartItemDto { Id = -1, ProductId = 10, Quantity = 2 }
            ]
        };

        await service.SaveGuestCartAsync(cart.SessionId!, cart);
        var result = await service.GetGuestCartAsync(cart.SessionId!);

        Assert.NotNull(result);
        Assert.Equal("guest-session-123", result!.SessionId);
        Assert.Single(result.Items);
        Assert.Equal(10, result.Items[0].ProductId);
    }

    [Fact]
    public async Task ClearGuestCartAsync_ShouldRemoveCachedCart()
    {
        var service = CreateService();
        var cart = new CartDto
        {
            SessionId = "guest-session-456"
        };

        await service.SaveGuestCartAsync(cart.SessionId!, cart);
        await service.ClearGuestCartAsync(cart.SessionId!);

        var result = await service.GetGuestCartAsync(cart.SessionId!);
        Assert.Null(result);
    }

    private static CartService CreateService()
    {
        return new CartService(new Mock<TgerCamera.Models.TgerCameraContext>().Object, new InMemoryDistributedCacheStub());
    }

    private sealed class InMemoryDistributedCacheStub : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _store = new();

        public byte[]? Get(string key) => _store.TryGetValue(key, out var value) ? value : null;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            Task.FromResult(Get(key));

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default) =>
            Task.CompletedTask;

        public void Remove(string key) => _store.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            _store[key] = value;

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }
    }
}
