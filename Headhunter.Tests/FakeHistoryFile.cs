using System.Globalization;
using System.IO.Compression;
using System.Text;
using Headhunter.Database;

namespace Headhunter.Tests;

/// <summary>
/// A fake voter history file in the shape of Michigan's (eight quoted columns and a trailing comma, ISO datetimes
/// at midnight, unpadded voter numbers, CRLF) for the voters of a <see cref="FakeVoterFile"/>, plus the few rows the
/// real file has that must not import: voters who aren't registered, and the same voter and election twice. The
/// same seed gives the same file.
/// </summary>
public sealed class FakeHistoryFile
{
    public const string Header =
        "\"VOTER_IDENTIFICATION_NUMBER\",\"COUNTY_CODE\",\"COUNTY_NAME\",\"JURISDICTION_CODE\",\"JURISDICTION_NAME\"," +
        "\"SCHOOL_DISTRICT_CODE\",\"SCHOOL_DISTRICT_NAME\",\"ELECTION_DATE\",";

    public static readonly (DateOnly Date, int Id, string Description)[] Elections =
    [
        (new(2012, 5, 8), 647, "MAY CONSOLIDATED"),
        (new(2020, 11, 3), 672, "PRESIDENTIAL GENERAL"),
        (new(2022, 8, 2), 685, "STATE PRIMARY"),
        (new(2022, 11, 8), 686, "STATE GENERAL"),
        (new(2024, 2, 27), 697, "PRESIDENTIAL PRIMARY"),
        (new(2024, 11, 5), 699, "STATE GENERAL"),
    ];

    public string Csv { get; }
    public IReadOnlyList<(Raw Voter, DateOnly Date, Raw Where)> Votes { get; }
    public int UnregisteredRows { get; }
    public int DuplicateRows { get; }

    /// <summary>The history rows that must end up in the database: one per registered voter and election.</summary>
    public int ExpectedRows => Votes.Count;

    private FakeHistoryFile(IReadOnlyList<(Raw, DateOnly, Raw)> votes, string csv, int unregistered, int duplicates)
    {
        Votes = votes;
        Csv = csv;
        UnregisteredRows = unregistered;
        DuplicateRows = duplicates;
    }

    public static FakeHistoryFile Create(FakeVoterFile voters, int seed = 1)
    {
        var random = new Random(seed);
        var votes = new List<(Raw Voter, DateOnly Date, Raw Where)>();
        var csv = new StringBuilder(Header).Append("\r\n");

        void Row(string voterNumber, DateOnly date, Raw where) => csv
            .Append($"\"{voterNumber}\",\"{where.COUNTY_CODE}\",\"{where.COUNTY_NAME}\",\"{where.JURISDICTION_CODE}\",\"{where.JURISDICTION_NAME}\",")
            .Append($"\"{where.SCHOOL_DISTRICT_CODE}\",\"{where.SCHOOL_DISTRICT_NAME}\",")
            .Append($"\"{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} 00:00:00\",\r\n");

        foreach (var voter in voters.Records)
        {
            foreach (var (date, _, _) in Elections)
            {
                if (random.Next(3) == 0) continue;
                // About one vote in twenty was cast from somewhere else than the voter is registered now.
                var where = random.Next(20) == 0 ? voters.Records[random.Next(voters.Records.Count)] : voter;
                votes.Add((voter, date, where));
                Row(voter.VOTER_IDENTIFICATION_NUMBER, date, where);
            }
        }

        var duplicates = 0;
        foreach (var (voter, date, where) in votes.Take(5).ToList())
        {
            Row(voter.VOTER_IDENTIFICATION_NUMBER, date, where);
            duplicates++;
        }

        var unregistered = 0;
        for (var i = 0; i < 25; i++)
        {
            Row((50_000_000 + i).ToString(CultureInfo.InvariantCulture), Elections[i % Elections.Length].Date, voters.Records[i]);
            unregistered++;
        }

        return new FakeHistoryFile(votes, csv.ToString(), unregistered, duplicates);
    }

    /// <summary>Writes the code tables the state sends with the files (County, Jurisdiction, School, Election Codes) for the voters' places.</summary>
    public static void WriteCodeFiles(FakeVoterFile voters, string directory)
    {
        Directory.CreateDirectory(directory);
        var records = voters.Records;
        var crlf = "\r\n";

        File.WriteAllText(Path.Combine(directory, "County Codes.csv"),
            "﻿County Code,County Name" + crlf + string.Concat(records
                .Select(r => (r.COUNTY_CODE, r.COUNTY_NAME)).Distinct().Select(c => $"{c.COUNTY_CODE},{c.COUNTY_NAME}{crlf}")));
        File.WriteAllText(Path.Combine(directory, "Jurisdiction Codes.csv"),
            "﻿COUNTY CODE,JURISDICTION CODE,JURISDICTION NAME" + crlf + string.Concat(records
                .Select(r => (r.COUNTY_CODE, r.JURISDICTION_CODE, r.JURISDICTION_NAME)).Distinct()
                .Select(j => $"{j.COUNTY_CODE},{j.JURISDICTION_CODE},{j.JURISDICTION_NAME}{crlf}")));
        File.WriteAllText(Path.Combine(directory, "School Codes.csv"),
            "﻿COUNTY CODE,JURISDICTION CODE,SCHOOL DISTRICT CODE,SCHOOL DISTRICT NAME" + crlf + string.Concat(records
                .Select(r => (r.COUNTY_CODE, r.JURISDICTION_CODE, r.SCHOOL_DISTRICT_CODE, r.SCHOOL_DISTRICT_NAME)).Distinct()
                .Select(s => $"{s.COUNTY_CODE},{s.JURISDICTION_CODE},{s.SCHOOL_DISTRICT_CODE},{s.SCHOOL_DISTRICT_NAME}{crlf}")));
        // The real file is "through 2025" and has no row for the oldest elections in the history.
        File.WriteAllText(Path.Combine(directory, "Election Codes through 2025.csv"),
            "﻿ELECTION DATE ID,ELECTION DATE,ELECTION DESCRIPTION" + crlf + string.Concat(Elections.Skip(1)
                .Select(e => $"{e.Id},{e.Date.Month}/{e.Date.Day}/{e.Date.Year},{e.Description}{crlf}")));
    }

    public static void WriteZip(string zipPath, string entryName, string content)
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        var entry = archive.CreateEntry(entryName);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
