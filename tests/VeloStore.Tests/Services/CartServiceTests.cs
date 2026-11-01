using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using VeloStore.Services;
using VeloStore.Tests.TestDoubles;
using VeloStore.ViewModels;

namespace VeloStore.Tests.Services;

public class CartServiceTests
{
    private readonly IDistributedCache _cache = TestData.NewDistributedCache();

    private CartService CreateService(HttpContext httpContext) =>
        new(_cache,
            new HttpContextAccessor { HttpContext = httpContext },
            TestData.CacheOptions,
            NullLogger<CartService>.Instance);

    private static DefaultHttpContext GuestContext(TestSession session) => new() { Session = session };

    private static DefaultHttpContext UserContext(string userId, TestSession? session = null)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "Test");
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity), Session = session ?? new TestSession() };
    }

    [Fact]
    public async Task Guest_first_cart_access_writes_to_the_session_so_the_cookie_is_issued()
    {
        var session = new TestSession();

        await CreateService(GuestContext(session)).GetCartAsync();

        Assert.Contains(CartService.GuestSessionMarkerKey, session.Keys);
    }

    [Fact]
    public async Task Guest_cart_survives_across_requests_in_the_same_session()
    {
        var session = new TestSession();

        await CreateService(GuestContext(session)).AddToCartAsync(1, "Trail Blazer 5000", 1299.99m, "/img.jpg");
        var cartOnNextRequest = await CreateService(GuestContext(session)).GetCartAsync();

        var item = Assert.Single(cartOnNextRequest.Items);
        Assert.Equal(1, item.ProductId);
    }

    [Fact]
    public async Task Adding_the_same_product_twice_increments_the_quantity()
    {
        var service = CreateService(GuestContext(new TestSession()));

        await service.AddToCartAsync(1, "Helmet", 149.99m, "/h.jpg");
        await service.AddToCartAsync(1, "Helmet", 149.99m, "/h.jpg");

        var item = Assert.Single((await service.GetCartAsync()).Items);
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public async Task Adding_a_product_with_a_negative_price_is_rejected()
    {
        var service = CreateService(GuestContext(new TestSession()));

        await Assert.ThrowsAsync<ArgumentException>(() => service.AddToCartAsync(1, "Broken", -1m, "/x.jpg"));
    }

    [Fact]
    public async Task Decreasing_to_zero_removes_the_item()
    {
        var service = CreateService(GuestContext(new TestSession()));
        await service.AddToCartAsync(1, "Gloves", 29.99m, "/g.jpg");

        await service.DecreaseAsync(1);

        Assert.Empty((await service.GetCartAsync()).Items);
    }

    [Fact]
    public async Task Increase_and_decrease_ignore_products_that_are_not_in_the_cart()
    {
        var service = CreateService(GuestContext(new TestSession()));

        await service.IncreaseAsync(42);
        await service.DecreaseAsync(42);

        Assert.Empty((await service.GetCartAsync()).Items);
    }

    [Fact]
    public async Task Item_count_is_the_sum_of_quantities()
    {
        var service = CreateService(GuestContext(new TestSession()));
        await service.AddToCartAsync(1, "Helmet", 149.99m, "/h.jpg");
        await service.AddToCartAsync(1, "Helmet", 149.99m, "/h.jpg");
        await service.AddToCartAsync(2, "Gloves", 29.99m, "/g.jpg");

        Assert.Equal(3, await service.GetCartItemCountAsync());
    }

    [Fact]
    public async Task Clear_empties_the_cart()
    {
        var service = CreateService(GuestContext(new TestSession()));
        await service.AddToCartAsync(1, "Helmet", 149.99m, "/h.jpg");

        await service.ClearCartAsync();

        Assert.Empty((await service.GetCartAsync()).Items);
    }

    [Fact]
    public async Task Signed_in_users_and_guests_have_separate_carts()
    {
        var session = new TestSession();
        await CreateService(GuestContext(session)).AddToCartAsync(1, "Helmet", 149.99m, "/h.jpg");

        var userCart = await CreateService(UserContext("user-1", session)).GetCartAsync();

        Assert.Empty(userCart.Items);
        Assert.Equal("user:user-1", userCart.UserId);
    }

    [Fact]
    public async Task Merge_moves_guest_items_into_the_user_cart_while_the_request_is_still_anonymous()
    {
        var session = new TestSession("guest-abc");
        var guestService = CreateService(GuestContext(session));
        await guestService.AddToCartAsync(1, "Helmet", 149.99m, "/h.jpg");
        await guestService.AddToCartAsync(2, "Gloves", 29.99m, "/g.jpg");

        // The user already had one helmet in their saved cart.
        await CreateService(UserContext("user-1")).AddToCartAsync(1, "Helmet", 149.99m, "/h.jpg");

        // Login request: the user has just signed in, but HttpContext.User is still anonymous.
        await guestService.MergeGuestCartIntoUserCartAsync("guest-abc", "user-1");

        var userCart = await CreateService(UserContext("user-1")).GetCartAsync();
        Assert.Equal(2, userCart.Items.Single(i => i.ProductId == 1).Quantity);
        Assert.Equal(1, userCart.Items.Single(i => i.ProductId == 2).Quantity);
        Assert.Null(await _cache.GetStringAsync(CartService.GuestCartKey("guest-abc")));
    }

    [Fact]
    public async Task Merge_without_a_guest_cart_leaves_the_user_cart_unchanged()
    {
        await CreateService(UserContext("user-1")).AddToCartAsync(1, "Helmet", 149.99m, "/h.jpg");

        await CreateService(GuestContext(new TestSession())).MergeGuestCartIntoUserCartAsync("no-such-session", "user-1");

        var json = await _cache.GetStringAsync(CartService.UserCartKey("user-1"));
        var cart = JsonSerializer.Deserialize<RedisCartVM>(json!, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(1, Assert.Single(cart!.Items).Quantity);
    }
}
