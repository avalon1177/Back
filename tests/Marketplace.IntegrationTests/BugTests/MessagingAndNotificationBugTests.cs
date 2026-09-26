using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Marketplace.Core.Entities;
using Marketplace.Core.Enums;
using Marketplace.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Marketplace.IntegrationTests.BugTests;

public sealed class MessagingAndNotificationBugTests : IntegrationTestBase
{
    public MessagingAndNotificationBugTests(MarketplaceApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ChatMessage_BuyerCanSendMessage()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "msg-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "msg-seller");
        var chat = await Scenario.SeedChatAsync(buyer, seller);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var response = await client.PostAsJsonAsync($"/api/chats/{chat.Id}/messages",
            new { Text = "Hello, I have a question about your product" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var messageExists = await Scenario.ExecuteDbAsync(async db =>
            await db.Messages.AnyAsync(m => m.ChatId == chat.Id && m.SenderId == buyer.UserId));
        Assert.True(messageExists);
    }

    [Fact]
    public async Task ChatMessage_SellerCanSendMessage()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "msg-seller-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "msg-seller");
        var chat = await Scenario.SeedChatAsync(buyer, seller);

        using var client = await Scenario.CreateAuthenticatedClientAsync(seller);

        var response = await client.PostAsJsonAsync($"/api/chats/{chat.Id}/messages",
            new { Text = "Sure, how can I help you?" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var messageExists = await Scenario.ExecuteDbAsync(async db =>
            await db.Messages.AnyAsync(m => m.ChatId == chat.Id && m.SenderId == seller.UserId));
        Assert.True(messageExists);
    }

    [Fact]
    public async Task ChatMessage_NonParticipant_ShouldReturnForbidden()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "msg-forbidden-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "msg-forbidden-seller");
        var otherBuyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "msg-forbidden-other");
        var chat = await Scenario.SeedChatAsync(buyer, seller);

        using var client = await Scenario.CreateAuthenticatedClientAsync(otherBuyer);

        var response = await client.PostAsJsonAsync($"/api/chats/{chat.Id}/messages",
            new { Text = "This should not work" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChatMessage_Read_MarksAsRead()
    {
        var buyer = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "read-buyer");
        var seller = await Scenario.CreateActorAsync(UserRole.Seller, withStore: true, emailPrefix: "read-seller");
        var chat = await Scenario.SeedChatAsync(buyer, seller);
        var message = await Scenario.SeedMessageAsync(chat, seller);

        using var client = await Scenario.CreateAuthenticatedClientAsync(buyer);

        var response = await client.PostAsync($"/api/chats/{chat.Id}/messages/{message.Id}/read", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var isRead = await Scenario.ExecuteDbAsync(async db =>
            (await db.Messages.FirstAsync(m => m.Id == message.Id)).IsRead);
        Assert.True(isRead);
    }

    [Fact]
    public async Task Notification_MarkAsRead_Works()
    {
        var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "notify-read-buyer");
        var notification = await Scenario.SeedNotificationAsync(actor, "Test Title", "Test Text");

        Assert.False(notification.IsRead);

        using var client = await Scenario.CreateAuthenticatedClientAsync(actor);

        var response = await client.PostAsync($"/api/notifications/{notification.Id}/read", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var isRead = await Scenario.ExecuteDbAsync(async db =>
            (await db.Notifications.FirstAsync(n => n.Id == notification.Id)).IsRead);
        Assert.True(isRead);
    }

    [Fact]
    public async Task Notification_OnlyOwnerCanAccess()
    {
        var actor = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "notify-owner-buyer");
        var otherActor = await Scenario.CreateActorAsync(UserRole.Buyer, emailPrefix: "notify-owner-other");
        var notification = await Scenario.SeedNotificationAsync(actor, "Private Title", "Private Text");

        using var client = await Scenario.CreateAuthenticatedClientAsync(otherActor);

        var response = await client.PostAsync($"/api/notifications/{notification.Id}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
