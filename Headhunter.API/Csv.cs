namespace Headhunter.API;

/// <summary>Writing CSV rows (RFC 4180) that spreadsheets open safely.</summary>
public static class Csv
{
    public static string Row(params string[] fields) => string.Join(',', fields.Select(Field));

    /// <summary>
    /// One field: quoted when it holds a comma, quote or line break, quotes doubled. A field that starts like a
    /// formula (= + - @, tab, carriage return) gets a leading apostrophe, so Excel shows it rather than runs it.
    /// </summary>
    public static string Field(string? value)
    {
        value ??= "";
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;
        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
