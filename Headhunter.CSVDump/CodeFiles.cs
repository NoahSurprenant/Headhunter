using System.Globalization;
using System.IO.Compression;
using Headhunter.Database;
using Microsoft.EntityFrameworkCore;

namespace Headhunter.CSVDump;

/// <summary>
/// The state's code tables that come with the voter files (County Codes.csv, Jurisdiction Codes.csv, School Codes.csv,
/// Election Codes through YYYY.csv): a directory of them, or the "Informational Documents" zip. They are plain
/// comma-separated files with a header, nothing quoted, the name last.
/// </summary>
public static class CodeFiles
{
    public static async Task LoadAsync(HeadhunterContext context, string path, CancellationToken ct = default)
    {
        string? Read(string prefix) => ReadFile(path, prefix);

        var counties = Rows(Read("County Codes"), 2).Select(r => (Code: r[0], Name: r[1])).ToList();
        var jurisdictions = Rows(Read("Jurisdiction Codes"), 3).Select(r => (County: r[0], Code: r[1], Name: r[2])).ToList();
        // School Codes lists a school district once for every jurisdiction it covers.
        var schools = Rows(Read("School Codes"), 4).Select(r => (Code: r[2], Name: r[3])).DistinctBy(s => s.Code).ToList();
        var elections = Rows(Read("Election Codes"), 3)
            .Select(r => (Id: int.Parse(r[0], CultureInfo.InvariantCulture),
                Date: DateOnly.ParseExact(r[1], "M/d/yyyy", CultureInfo.InvariantCulture), Description: r[2]))
            .ToList();

        await Lookups.UpsertAsync(context,
            counties.Select(c => (c.Code, c.Name)),
            jurisdictions.Select(j => (j.County, j.Code, j.Name)),
            schools.Select(s => (s.Code, s.Name)),
            elections.Select(e => (e.Date, (int?)e.Id, e.Description)),
            overwrite: true, ct);
    }

    private static string? ReadFile(string path, string prefix)
    {
        if (Directory.Exists(path))
        {
            var file = Directory.EnumerateFiles(path, "*.csv", SearchOption.AllDirectories)
                .FirstOrDefault(f => Path.GetFileName(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            return file is null ? null : File.ReadAllText(file);
        }

        using var archive = ZipFile.OpenRead(path);
        var entry = archive.Entries.FirstOrDefault(e =>
            e.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase));
        if (entry is null) return null;
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static IEnumerable<string[]> Rows(string? text, int columns)
    {
        if (text is null) yield break;
        var lines = text.TrimStart('﻿').Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1);
        foreach (var line in lines)
        {
            var row = line.TrimEnd('\r').Split(',', columns).Select(f => f.Trim()).ToArray();
            if (row.Length == columns) yield return row;
        }
    }
}
