using System.Text.Json;
using Application.Common.InternalServices.Notifications.Models;
using Application.Common.InternalServices.Premium.Models;
using Application.Common.InternalServices.Proposals.Models;

namespace Test_Gigbridge_Backend.Application.Common.InternalServices;

public sealed class ContractModelSerializationTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void PremiumBenefitsDto_KeepsExistingHttpJsonShape()
    {
        var premiumUntil = new DateTime(2026, 8, 15, 10, 30, 0, DateTimeKind.Utc);
        var model = new PremiumBenefitsDto(true, true, true, premiumUntil, "Pro");

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(model, WebJson));
        var root = document.RootElement;

        Assert.True(root.GetProperty("isPremium").GetBoolean());
        Assert.True(root.GetProperty("isIdentityVerified").GetBoolean());
        Assert.True(root.GetProperty("showProVerifiedBadge").GetBoolean());
        Assert.Equal(premiumUntil, root.GetProperty("premiumUntil").GetDateTime());
        Assert.Equal("Pro", root.GetProperty("planName").GetString());
        Assert.False(root.TryGetProperty("IsPremium", out _));
    }

    [Fact]
    public void QuestionTimerStateDto_KeepsExistingHttpJsonShape()
    {
        var proposalId = Guid.Parse("d75dad22-e1cf-4a41-b1a9-6d1456e05f34");
        var questionId = Guid.Parse("26b9ec30-3d03-4722-958b-b5d66ea90233");
        var startedAt = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);
        var expiresAt = startedAt.AddMinutes(5);
        var model = new QuestionTimerStateDto(
            proposalId,
            questionId,
            startedAt,
            expiresAt,
            300,
            false,
            null);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(model, WebJson));
        var root = document.RootElement;

        Assert.Equal(proposalId, root.GetProperty("proposalId").GetGuid());
        Assert.Equal(questionId, root.GetProperty("jobPostQuestionId").GetGuid());
        Assert.Equal(300, root.GetProperty("remainingSeconds").GetInt32());
        Assert.False(root.GetProperty("isLocked").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("lockedReason").ValueKind);
    }

    [Fact]
    public void NotificationDto_KeepsNumericEnumAndCamelCaseProperties()
    {
        var notificationId = Guid.Parse("fbe06cec-0b70-47ae-9085-f1a7851b7094");
        var model = new NotificationDto
        {
            Id = notificationId,
            ReadTargetId = notificationId,
            Title = "Contract updated",
            IsRead = true,
            CreatedAt = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc)
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(model, WebJson));
        var root = document.RootElement;

        Assert.Equal(notificationId, root.GetProperty("id").GetGuid());
        Assert.Equal("Personal", root.GetProperty("source").GetString());
        Assert.Equal("Contract updated", root.GetProperty("title").GetString());
        Assert.True(root.GetProperty("isRead").GetBoolean());
        Assert.Equal(JsonValueKind.Number, root.GetProperty("type").ValueKind);
        Assert.Equal(0, root.GetProperty("type").GetInt32());
    }
}
