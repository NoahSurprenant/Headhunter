using Headhunter.Database;
using Microsoft.EntityFrameworkCore;

namespace Headhunter.CSVDump;

public static class Lookups
{
    /// <summary>
    /// Adds the counties, jurisdictions, school districts and elections that aren't in the database yet. With
    /// overwrite the given names replace the stored ones (the state's code files are the authority); without it
    /// only missing rows are added (names read off a data file fill gaps and never change a code file's).
    /// </summary>
    public static async Task UpsertAsync(HeadhunterContext context,
        IEnumerable<(string Code, string Name)> counties,
        IEnumerable<(string County, string Code, string Name)> jurisdictions,
        IEnumerable<(string Code, string Name)> schools,
        IEnumerable<(DateOnly Date, int? CodeId, string Description)> elections,
        bool overwrite, CancellationToken ct = default)
    {
        var countyRows = await context.Counties.ToDictionaryAsync(c => c.Code, ct);
        foreach (var (code, name) in counties)
        {
            if (countyRows.TryGetValue(code, out var row)) { if (overwrite) row.Name = name; }
            else context.Counties.Add(countyRows[code] = new County { Code = code, Name = name });
        }
        // Counties first: a jurisdiction's foreign key needs its county.
        await context.SaveChangesAsync(ct);

        var jurisdictionRows = await context.Jurisdictions.ToDictionaryAsync(j => (j.CountyCode, j.Code), ct);
        foreach (var (county, code, name) in jurisdictions)
        {
            if (jurisdictionRows.TryGetValue((county, code), out var row)) { if (overwrite) row.Name = name; }
            else
            {
                if (!countyRows.ContainsKey(county))
                    context.Counties.Add(countyRows[county] = new County { Code = county, Name = "" });
                context.Jurisdictions.Add(jurisdictionRows[(county, code)] = new Jurisdiction { CountyCode = county, Code = code, Name = name });
            }
        }

        var schoolRows = await context.SchoolDistricts.ToDictionaryAsync(s => s.Code, ct);
        foreach (var (code, name) in schools)
        {
            if (schoolRows.TryGetValue(code, out var row)) { if (overwrite) row.Name = name; }
            else context.SchoolDistricts.Add(schoolRows[code] = new SchoolDistrict { Code = code, Name = name });
        }

        var electionRows = await context.Elections.ToDictionaryAsync(e => e.Date, ct);
        foreach (var (date, codeId, description) in elections)
        {
            if (electionRows.TryGetValue(date, out var row))
            {
                if (overwrite || row.Description.Length == 0) { row.Description = description; row.CodeId = codeId ?? row.CodeId; }
            }
            else context.Elections.Add(electionRows[date] = new Election { Date = date, CodeId = codeId, Description = description });
        }

        await context.SaveChangesAsync(ct);
    }
}
