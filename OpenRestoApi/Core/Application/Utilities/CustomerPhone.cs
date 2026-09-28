using System.Text.RegularExpressions;
using OpenRestoApi.Core.Application.Exceptions;

namespace OpenRestoApi.Core.Application.Utilities;

/// <summary>
/// The guest's phone number on a booking or waitlist entry, stored in E.164: a <c>+</c>, a
/// non-zero country code digit, and at most fifteen digits in all.
/// </summary>
public static partial class CustomerPhone
{
    /// <summary>The <c>+</c> plus the fifteen digits E.164 allows.</summary>
    public const int MaxLength = 16;

    /// <summary>Bound on the raw request value, which may still carry spaces and separators.</summary>
    public const int MaxInputLength = 32;

    private static readonly char[] Separators = ['-', '.', '(', ')'];

    [GeneratedRegex(@"^\+[1-9][0-9]{6,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex E164();

    /// <summary>
    /// The number to store: <paramref name="raw"/> with whitespace and <c>- . ( )</c> removed.
    /// </summary>
    /// <exception cref="ValidationException">
    /// <see cref="ErrorCodes.BookingPhoneRequired"/> when blank, <see cref="ErrorCodes.BookingPhoneInvalid"/>
    /// when what remains is not E.164.
    /// </exception>
    /// <seealso>CustomerPhoneTests.Normalize_AcceptsSevenDigits</seealso>
    /// <seealso>CustomerPhoneTests.Normalize_RejectsSixDigits</seealso>
    /// <seealso>CustomerPhoneTests.Normalize_AcceptsFifteenDigits</seealso>
    /// <seealso>CustomerPhoneTests.Normalize_RejectsSixteenDigits</seealso>
    /// <seealso>CustomerPhoneTests.Normalize_StripsSeparators</seealso>
    /// <seealso>CustomerPhoneTests.Normalize_RejectsMissingPlus</seealso>
    /// <seealso>CustomerPhoneTests.Normalize_RejectsLeadingZeroCountryCode</seealso>
    /// <seealso>CustomerPhoneTests.Normalize_BlankIsRequired</seealso>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new ValidationException("A phone number is required.") { Code = ErrorCodes.BookingPhoneRequired };
        }

        string compact = string.Concat(raw.Where(c => !char.IsWhiteSpace(c) && Array.IndexOf(Separators, c) < 0));
        if (!E164().IsMatch(compact))
        {
            throw new ValidationException(
                "Phone number must be in international format, e.g. +593991234567.")
            { Code = ErrorCodes.BookingPhoneInvalid };
        }

        return compact;
    }
}
