using OpenRestoApi.Core.Application.Exceptions;
using OpenRestoApi.Core.Application.Utilities;

namespace OpenRestoApi.Tests.Utilities;

public class CustomerPhoneTests
{
    [Fact]
    public void Normalize_AcceptsAnE164Number()
        => Assert.Equal("+593991234567", CustomerPhone.Normalize("+593991234567"));

    [Theory]
    [InlineData("+593 99 123 4567")]
    [InlineData("+593-99-123-4567")]
    [InlineData("+593 (99) 123.4567")]
    [InlineData("  +593991234567\t")]
    public void Normalize_StripsSeparators(string raw)
        => Assert.Equal("+593991234567", CustomerPhone.Normalize(raw));

    [Fact]
    public void Normalize_RejectsMissingPlus()
        => AssertInvalid("593991234567");

    [Fact]
    public void Normalize_RejectsLeadingZeroCountryCode()
        => AssertInvalid("+0593991234567");

    [Fact]
    public void Normalize_AcceptsSevenDigits()
        => Assert.Equal("+1234567", CustomerPhone.Normalize("+1234567"));

    [Fact]
    public void Normalize_RejectsSixDigits()
        => AssertInvalid("+123456");

    [Fact]
    public void Normalize_AcceptsFifteenDigits()
    {
        string fifteen = "+" + new string('9', 15);
        Assert.Equal(fifteen, CustomerPhone.Normalize(fifteen));
        Assert.Equal(CustomerPhone.MaxLength, fifteen.Length);
    }

    [Fact]
    public void Normalize_RejectsSixteenDigits()
        => AssertInvalid("+" + new string('9', 16));

    [Theory]
    [InlineData("+59399123456a")]
    [InlineData("+593٩٩1234567")]
    [InlineData("+593/991234567")]
    public void Normalize_RejectsAnythingButAsciiDigits(string raw)
        => AssertInvalid(raw);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_BlankIsRequired(string? raw)
    {
        ValidationException ex = Assert.Throws<ValidationException>(() => CustomerPhone.Normalize(raw));
        Assert.Equal(ErrorCodes.BookingPhoneRequired, ex.Code);
    }

    private static void AssertInvalid(string raw)
    {
        ValidationException ex = Assert.Throws<ValidationException>(() => CustomerPhone.Normalize(raw));
        Assert.Equal(ErrorCodes.BookingPhoneInvalid, ex.Code);
    }
}
