using System.Text.RegularExpressions;

namespace Headhunter.API;

/// <summary>
/// A voter's mailing address (MAILING_ADDRESS_LINE_ONE..FIVE) is only ever filled in when mail doesn't go to the
/// residence - most voters have none, which is a run of five empty strings (the column is nvarchar(max), NOT NULL;
/// see Headhunter.CSVDump/Worker.cs). This formats the non-empty lines and, from the last one, makes a best effort
/// at the state the mail goes to, so the client can flag an out-of-state mailing address.
/// </summary>
public static class MailingAddress
{
    // The last line of a US mailing address ends "CITY, ST 12345" or "CITY, ST 12345-6789". Good enough for
    // flagging - if it doesn't match, the address just isn't shown as in or out of state.
    private static readonly Regex StateAndZip = new(@"\b(?<state>[A-Z]{2})\s+\d{5}(-\d{4})?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The address lines with blanks dropped, in order.</summary>
    public static string[] NonEmptyLines(string lineOne, string lineTwo, string lineThree, string lineFour, string lineFive) =>
        new[] { lineOne, lineTwo, lineThree, lineFour, lineFive }
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

    /// <summary>
    /// True if the mailing address's last line names a state other than <paramref name="registeredState"/>.
    /// False (not just unknown) when there's no mailing address or the last line doesn't end in a recognizable
    /// state/ZIP.
    /// </summary>
    public static bool IsOutOfState(string[] mailingAddressLines, string registeredState)
    {
        if (mailingAddressLines.Length == 0)
            return false;

        var match = StateAndZip.Match(mailingAddressLines[^1]);
        if (!match.Success)
            return false;

        return !string.Equals(match.Groups["state"].Value, registeredState, StringComparison.OrdinalIgnoreCase);
    }
}
