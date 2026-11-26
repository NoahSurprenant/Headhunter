using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Headhunter.Database.Conversion;

/// <summary>A database: its server's kind and the connection string to it.</summary>
public sealed record DatabaseEndpoint(DatabaseProvider Provider, string ConnectionString);

/// <summary>A table the conversion copied, and how many rows.</summary>
public sealed record ConvertedTable(string Name, long Rows);

/// <summary>Why a conversion refused to run or stopped; the message says what to do.</summary>
public sealed class ConversionException(string message) : Exception(message);

/// <summary>
/// Copies a Headhunter database into a new one, SQL Server to PostgreSQL or the other way round (or the same
/// kind). The target is made by the app's own migrations, so it has the schema the app expects on its server;
/// then every table's rows are copied into it, keys and all.
///
/// It refuses unless the source has every migration of this build, and unless the target has no rows yet.
/// All of the copy is one transaction on the target, checked before it commits: each table's row count read
/// back from the target must be the source's. If anything fails, nothing is committed and the target stays
/// as it was (migrated, empty), so the next run takes it. The source is only read; nothing should write to
/// it while this runs.
/// </summary>
public sealed class DatabaseConverter(ILogger<DatabaseConverter> logger)
{
    private const int BatchSize = 5000;

    public async Task<IReadOnlyList<ConvertedTable>> ConvertAsync(DatabaseEndpoint source, DatabaseEndpoint target,
        CancellationToken cancellationToken = default)
    {
        await using var sourceContext = NewContext(source);
        var pending = (await sourceContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
            throw new ConversionException($"The source database is missing migrations of this build ({string.Join(", ", pending)}): migrate it first.");

        await using var targetContext = NewContext(target);
        await targetContext.Database.MigrateAsync(cancellationToken);
        if (await targetContext.Addresses.AnyAsync(cancellationToken) || await targetContext.MichiganVoterRecords.AnyAsync(cancellationToken)
            || await targetContext.Voters.AnyAsync(cancellationToken) || await targetContext.VoteHistory.AnyAsync(cancellationToken))
            throw new ConversionException("The target database already has rows: convert into an empty one.");

        // Inserts need no change tracking beyond the batch in hand.
        targetContext.ChangeTracker.AutoDetectChangesEnabled = false;
        await using var transaction = await targetContext.Database.BeginTransactionAsync(cancellationToken);

        // The code tables are small, and the addresses point at them.
        await CopyWholeAsync("lookup", sourceContext, targetContext, c => c.Counties, cancellationToken);
        await CopyWholeAsync("lookup", sourceContext, targetContext, c => c.Jurisdictions, cancellationToken);
        await CopyWholeAsync("lookup", sourceContext, targetContext, c => c.SchoolDistricts, cancellationToken);
        await CopyWholeAsync("lookup", sourceContext, targetContext, c => c.StateHouseDistricts, cancellationToken);
        await CopyWholeAsync("lookup", sourceContext, targetContext, c => c.StateSenateDistricts, cancellationToken);
        await CopyWholeAsync("lookup", sourceContext, targetContext, c => c.UsCongressDistricts, cancellationToken);
        await CopyWholeAsync("lookup", sourceContext, targetContext, c => c.CountyCommissionerDistricts, cancellationToken);

        // Parents first: Voters point at Addresses and MichiganVoterRecords.
        var tables = new List<ConvertedTable>
        {
            await CopyAsync(nameof(HeadhunterContext.Addresses), sourceContext, targetContext, c => c.Addresses, a => a.ID, cancellationToken),
            await CopyAsync(nameof(HeadhunterContext.MichiganVoterRecords), sourceContext, targetContext, c => c.MichiganVoterRecords, r => r.ID, cancellationToken),
            await CopyAsync(nameof(HeadhunterContext.Voters), sourceContext, targetContext, c => c.Voters, v => v.ID, cancellationToken),
            await CopyWholeAsync(nameof(HeadhunterContext.Elections), sourceContext, targetContext, c => c.Elections, cancellationToken),
            await CopyHistoryAsync(sourceContext, targetContext, cancellationToken),
        };

        await CheckAsync(tables, targetContext, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return tables;
    }

    private static async Task<ConvertedTable> CopyWholeAsync<T>(string name, HeadhunterContext source, HeadhunterContext target,
        Func<HeadhunterContext, DbSet<T>> set, CancellationToken cancellationToken) where T : class
    {
        var rows = await set(source).AsNoTracking().ToArrayAsync(cancellationToken);
        set(target).AddRange(rows);
        await target.SaveChangesAsync(cancellationToken);
        target.ChangeTracker.Clear();
        return new ConvertedTable(name, rows.Length);
    }

    // The vote history is the biggest table (tens of millions of rows): keyset pages on its two-part key.
    private async Task<ConvertedTable> CopyHistoryAsync(HeadhunterContext source, HeadhunterContext target, CancellationToken cancellationToken)
    {
        long rows = 0;
        VoteHistory[] batch;
        VoteHistory? last = null;
        do
        {
            var query = source.VoteHistory.AsNoTracking();
            if (last is not null)
            {
                var voter = last.VoterID;
                var date = last.ElectionDate;
                query = query.Where(h => h.VoterID.CompareTo(voter) > 0 || (h.VoterID == voter && h.ElectionDate > date));
            }
            batch = await query.OrderBy(h => h.VoterID).ThenBy(h => h.ElectionDate).Take(BatchSize).ToArrayAsync(cancellationToken);
            if (batch.Length == 0) break;

            target.VoteHistory.AddRange(batch);
            await target.SaveChangesAsync(cancellationToken);
            target.ChangeTracker.Clear();

            last = batch[^1];
            rows += batch.Length;
            if (rows % (BatchSize * 20) == 0) logger.LogInformation("VoteHistory: {Rows} rows", rows);
        } while (batch.Length == BatchSize);

        logger.LogInformation("VoteHistory: {Rows} rows copied", rows);
        return new ConvertedTable(nameof(HeadhunterContext.VoteHistory), rows);
    }

    private async Task<ConvertedTable> CopyAsync<T>(string name, HeadhunterContext source, HeadhunterContext target,
        Func<HeadhunterContext, DbSet<T>> set, Func<T, Guid> key, CancellationToken cancellationToken) where T : class
    {
        long rows = 0;
        T[] batch;
        Guid? last = null;
        var keyName = source.Model.FindEntityType(typeof(T))!.FindPrimaryKey()!.Properties.Single().Name;
        do
        {
            // Keyset paging on the primary key, in the source's own order for it.
            var query = set(source).AsNoTracking();
            if (last is { } after)
                query = query.Where(e => EF.Property<Guid>(e, keyName).CompareTo(after) > 0);
            batch = await query.OrderBy(e => EF.Property<Guid>(e, keyName)).Take(BatchSize).ToArrayAsync(cancellationToken);
            if (batch.Length == 0) break;

            set(target).AddRange(batch);
            await target.SaveChangesAsync(cancellationToken);
            target.ChangeTracker.Clear();

            last = key(batch[^1]);
            rows += batch.Length;
            if (rows % (BatchSize * 20) == 0) logger.LogInformation("{Table}: {Rows} rows", name, rows);
        } while (batch.Length == BatchSize);

        logger.LogInformation("{Table}: {Rows} rows copied", name, rows);
        return new ConvertedTable(name, rows);
    }

    private static async Task CheckAsync(IReadOnlyList<ConvertedTable> tables, HeadhunterContext target, CancellationToken cancellationToken)
    {
        foreach (var table in tables)
        {
            var count = table.Name switch
            {
                nameof(HeadhunterContext.Addresses) => await target.Addresses.LongCountAsync(cancellationToken),
                nameof(HeadhunterContext.MichiganVoterRecords) => await target.MichiganVoterRecords.LongCountAsync(cancellationToken),
                nameof(HeadhunterContext.Elections) => await target.Elections.LongCountAsync(cancellationToken),
                nameof(HeadhunterContext.VoteHistory) => await target.VoteHistory.LongCountAsync(cancellationToken),
                _ => await target.Voters.LongCountAsync(cancellationToken),
            };
            if (count != table.Rows)
                throw new ConversionException($"{table.Name}: copied {table.Rows} rows but the target has {count}; nothing was committed.");
        }
    }

    public static HeadhunterContext NewContext(DatabaseEndpoint endpoint)
    {
        var builder = new DbContextOptionsBuilder<HeadhunterContext>();
        builder.UseHeadhunterDatabase(endpoint.Provider, endpoint.ConnectionString, TimeSpan.FromHours(1));
        return new HeadhunterContext(builder.Options);
    }
}
