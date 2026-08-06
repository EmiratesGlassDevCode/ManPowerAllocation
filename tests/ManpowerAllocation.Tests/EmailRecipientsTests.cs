using ManpowerAllocation.Application.Email;
using Xunit;

namespace ManpowerAllocation.Tests;

/// <summary>Tests for the recipient-list parser used by the SMTP sender and settings validation.</summary>
public sealed class EmailRecipientsTests
{
    [Fact]
    public void Parse_null_or_blank_returns_empty()
    {
        Assert.Empty(EmailRecipients.Parse(null));
        Assert.Empty(EmailRecipients.Parse("   "));
    }

    [Theory]
    [InlineData("a@x.com, b@y.com; c@z.com")]
    [InlineData("a@x.com\r\nb@y.com\nc@z.com")]
    [InlineData(" a@x.com ,, b@y.com ;; c@z.com ")]
    public void Parse_splits_on_comma_semicolon_and_newlines(string raw)
    {
        var result = EmailRecipients.Parse(raw);

        Assert.Equal(new[] { "a@x.com", "b@y.com", "c@z.com" }, result);
    }

    [Fact]
    public void Parse_is_case_insensitively_distinct()
    {
        var result = EmailRecipients.Parse("a@x.com, A@X.com, b@y.com");

        Assert.Equal(2, result.Count);
        Assert.Contains("a@x.com", result);
        Assert.Contains("b@y.com", result);
    }
}
