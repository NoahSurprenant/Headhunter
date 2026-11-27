using Headhunter.Database;
using Microsoft.EntityFrameworkCore;

namespace Headhunter.API;

/// <summary>
/// The autocomplete queries: distinct, ordered prefix matches, at most 50. On Postgres the names and cities have a
/// nondeterministic (case-insensitive) collation, which cannot seek a btree for LIKE 'x%', so there they match
/// upper(column) COLLATE "C" (the expression indexes of the Postgres migration "PrefixIndexes") and come back upper-cased.
/// </summary>
public static class Suggestions
{
    private const int Limit = 50;

    private static string Fold(string value) => value.ToUpperInvariant();

    public static IQueryable<string> FirstNames(HeadhunterContext context, string query)
    {
        if (!context.Database.IsNpgsql())
            return context.Voters.Where(x => x.FIRST_NAME.StartsWith(query)).Select(x => x.FIRST_NAME)
                .Distinct().OrderBy(x => x).Take(Limit);
        var term = Fold(query);
        return context.Voters.Where(x => EF.Functions.Collate(x.FIRST_NAME.ToUpper(), "C").StartsWith(term))
            .Select(x => EF.Functions.Collate(x.FIRST_NAME.ToUpper(), "C"))
            .Distinct().OrderBy(x => x).Take(Limit);
    }

    public static IQueryable<string> MiddleNames(HeadhunterContext context, string query)
    {
        if (!context.Database.IsNpgsql())
            return context.Voters.Where(x => x.MIDDLE_NAME.StartsWith(query)).Select(x => x.MIDDLE_NAME)
                .Distinct().OrderBy(x => x).Take(Limit);
        var term = Fold(query);
        return context.Voters.Where(x => EF.Functions.Collate(x.MIDDLE_NAME.ToUpper(), "C").StartsWith(term))
            .Select(x => EF.Functions.Collate(x.MIDDLE_NAME.ToUpper(), "C"))
            .Distinct().OrderBy(x => x).Take(Limit);
    }

    public static IQueryable<string> LastNames(HeadhunterContext context, string query)
    {
        if (!context.Database.IsNpgsql())
            return context.Voters.Where(x => x.LAST_NAME.StartsWith(query)).Select(x => x.LAST_NAME)
                .Distinct().OrderBy(x => x).Take(Limit);
        var term = Fold(query);
        return context.Voters.Where(x => EF.Functions.Collate(x.LAST_NAME.ToUpper(), "C").StartsWith(term))
            .Select(x => EF.Functions.Collate(x.LAST_NAME.ToUpper(), "C"))
            .Distinct().OrderBy(x => x).Take(Limit);
    }

    public static IQueryable<string> Cities(HeadhunterContext context, string query)
    {
        if (!context.Database.IsNpgsql())
            return context.Addresses.Where(x => x.CITY.StartsWith(query)).Select(x => x.CITY)
                .Distinct().OrderBy(x => x).Take(Limit);
        var term = Fold(query);
        return context.Addresses.Where(x => EF.Functions.Collate(x.CITY.ToUpper(), "C").StartsWith(term))
            .Select(x => EF.Functions.Collate(x.CITY.ToUpper(), "C"))
            .Distinct().OrderBy(x => x).Take(Limit);
    }

    // Addresses that start with the query ("2078 W WYN") or whose street name does ("WYNDHAM"): two prefix
    // searches, each an index seek, rather than LIKE '%query%', which reads all 4M addresses.
    public static IQueryable<string> Streets(HeadhunterContext context, string query)
    {
        if (!context.Database.IsNpgsql())
        {
            var byAddress = context.Addresses.Where(x => x.FullStreetAddress.StartsWith(query)).Select(x => x.FullStreetAddress);
            var byStreetName = context.Addresses.Where(x => x.STREET_NAME.StartsWith(query)).Select(x => x.FullStreetAddress);
            return byAddress.Union(byStreetName).OrderBy(x => x).Take(Limit);
        }
        var term = Fold(query);
        var byFullAddress = context.Addresses
            .Where(x => EF.Functions.Collate(x.FullStreetAddress.ToUpper(), "C").StartsWith(term))
            .Select(x => EF.Functions.Collate(x.FullStreetAddress.ToUpper(), "C"));
        var byName = context.Addresses
            .Where(x => EF.Functions.Collate(x.STREET_NAME.ToUpper(), "C").StartsWith(term))
            .Select(x => EF.Functions.Collate(x.FullStreetAddress.ToUpper(), "C"));
        return byFullAddress.Union(byName).OrderBy(x => x).Take(Limit);
    }
}
