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
        // The history file only has a date, so two elections on one day are one election here: the lower id, both descriptions.
        var elections = Rows(Read("Election Codes"), 3)
            .Select(r => (Id: int.Parse(r[0], CultureInfo.InvariantCulture),
                Date: DateOnly.ParseExact(r[1], "M/d/yyyy", CultureInfo.InvariantCulture), Description: r[2]))
            .GroupBy(e => e.Date)
            .Select(g => (Id: g.Min(e => e.Id), Date: g.Key,
                Description: Truncate(string.Join(" / ", g.OrderBy(e => e.Id).Select(e => e.Description).Distinct()), 100)))
            .ToList();

        await Lookups.UpsertAsync(context,
            counties.Select(c => (c.Code, c.Name)),
            jurisdictions.Select(j => (j.County, j.Code, j.Name)),
            schools.Select(s => (s.Code, s.Name)),
            elections.Select(e => (e.Date, (int?)e.Id, e.Description)),
            overwrite: true, ct);
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];

    private static string? ReadFile(string path, string prefix)
    {
        if (Directory.Exists(path))
        {
            var files = Directory.EnumerateFiles(path, "*.csv", SearchOption.AllDirectories)
                .Where(f => Path.GetFileName(f).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
            var file = Choose(prefix, files, Path.GetFileName);
            return file is null ? null : File.ReadAllText(file);
        }

        using var archive = ZipFile.OpenRead(path);
        var entry = Choose(prefix, archive.Entries.Where(e =>
            e.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)).ToList(),
            e => e.Name);
        if (entry is null) return null;
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    // Election Codes comes as "through YYYY": the latest year has every earlier election. Any other table twice is a mistake to fix.
    private static T? Choose<T>(string prefix, List<T> matches, Func<T, string> name) where T : class
    {
        if (matches.Count <= 1) return matches.SingleOrDefault();
        if (prefix == "Election Codes") return matches.OrderByDescending(name, StringComparer.OrdinalIgnoreCase).First();
        throw new InvalidOperationException($"More than one \"{prefix}\" file in the codes: {string.Join(", ", matches.Select(name))}.");
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
