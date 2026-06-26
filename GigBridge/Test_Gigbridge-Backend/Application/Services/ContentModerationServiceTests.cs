using Infrastructure.Services;

namespace Test_Gigbridge_Backend.Application.Services;

public class ContentModerationServiceTests
{
    private readonly ContentModerationService _service = new();

    [Fact]
    public void ValidateJobPostContent_AllowsSafeJobPost()
    {
        var result = _service.ValidateJobPostContent(
            "Build a booking module",
            "Create booking workflow and notification logic for remote, hybrid, and onsite jobs.");

        Assert.True(result.IsAllowed);
        Assert.Equal(0, result.RiskScore);
    }

    [Theory]
    [InlineData("Can nguoi van chuyen ma tuy", "Illegal drugs / narcotics")]
    [InlineData("Tuyen nhan vien ca do bong da", "Gambling / betting / casino")]
    [InlineData("Massage kich duc cho khach", "Prostitution / sexual services / adult escort")]
    [InlineData("Cho thue tai khoan ngan hang nhan tien ho", "Money laundering / mule account / suspicious payment transfer")]
    [InlineData("Lay OTP va thong tin ngan hang", "Fraud / scam / phishing / identity theft")]
    [InlineData("Viet malware va ddos website", "Cybercrime / malware / hacking")]
    [InlineData("Lam bang gia va cccd gia", "Fake documents / fake certificates")]
    public void ValidateJobPostContent_BlocksIllegalCategories(string description, string expectedCategory)
    {
        var result = _service.ValidateJobPostContent("Urgent job", description);

        Assert.False(result.IsAllowed);
        Assert.True(result.RiskScore >= 100);
        Assert.Contains(expectedCategory, result.MatchedCategories);
        Assert.NotEmpty(result.Violations);
    }

    [Fact]
    public void ValidateJobPostContent_CatchesObfuscatedVietnameseDrugTerm()
    {
        var result = _service.ValidateJobPostContent(
            "Nhan giao viec gap",
            "Can nguoi van chuyen m.a-t_u/y trong ngay.");

        Assert.False(result.IsAllowed);
        Assert.Contains("Illegal drugs / narcotics", result.MatchedCategories);
    }

    [Fact]
    public void ValidateJobPostContent_BlocksCombinedSuspiciousRecruitmentSignals()
    {
        var result = _service.ValidateJobPostContent(
            "Viec nhe luong cao",
            "Khong can kinh nghiem, nhan tien nhanh, lien he Telegram.");

        Assert.False(result.IsAllowed);
        Assert.Contains("Suspicious illegal recruitment patterns", result.MatchedCategories);
    }
}
