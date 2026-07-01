namespace Adeni.Application.Tests.Security;

using Adeni.Application.Security;

public sealed class PiiMaskerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("not-an-email", "[REDACTED]")]
    [InlineData("user@example.com", "u***@example.com")]
    public void MaskEmail_masks_as_expected(string? input, string expected)
    {
        Assert.Equal(expected, PiiMasker.MaskEmail(input));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("123", "[REDACTED]")]
    [InlineData("+2348012345678", "[REDACTED]5678")]
    public void MaskPhone_masks_as_expected(string? input, string expected)
    {
        Assert.Equal(expected, PiiMasker.MaskPhone(input));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("hello world", "[REDACTED]")]
    public void MaskMessageBody_redacts_content(string? input, string expected)
    {
        Assert.Equal(expected, PiiMasker.MaskMessageBody(input));
    }

    [Theory]
    [InlineData(PiiKind.Email, "user@b.co", "u***@b.co")]
    [InlineData(PiiKind.Phone, "08012345678", "[REDACTED]5678")]
    [InlineData(PiiKind.MessageBody, "secret", "[REDACTED]")]
    [InlineData(PiiKind.None, "visible", "visible")]
    public void SanitizeLogValue_applies_kind(PiiKind kind, string input, string expected)
    {
        Assert.Equal(expected, PiiMasker.SanitizeLogValue(input, kind));
    }

    [Fact]
    public void SanitizeLogValue_unknown_kind_redacts()
    {
        var value = PiiMasker.SanitizeLogValue("secret", (PiiKind)999);

        Assert.Equal("[REDACTED]", value);
    }
}
