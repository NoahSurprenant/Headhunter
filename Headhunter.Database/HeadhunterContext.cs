using Microsoft.EntityFrameworkCore;

namespace Headhunter.Database;

public class HeadhunterContext : DbContext
{
    public const string CaseInsensitiveCollation = "case_insensitive";

    public HeadhunterContext() : base()
    {
    }

    public HeadhunterContext(DbContextOptions<HeadhunterContext> options) : base(options)
    {
    }

    public DbSet<MichiganVoterRecord> MichiganVoterRecords { get; set; }
    public DbSet<Address> Addresses { get; set; }
    public DbSet<Voter> Voters { get; set; }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Postgres: text compared as SQL Server's default collation does, case-insensitive and accent-sensitive, so
        // the name and address searches match whatever the case ("smith" finds SMITH). Created in OnModelCreating.
        if (Database.IsNpgsql())
        {
            configurationBuilder.Properties<string>().UseCollation(CaseInsensitiveCollation);
            configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ICU's root locale, ignoring case only (strength level 2). Nondeterministic, so LIKE on it needs PostgreSQL 18.
        if (Database.IsNpgsql())
            modelBuilder.HasCollation(CaseInsensitiveCollation, locale: "und-u-ks-level2", provider: "icu", deterministic: false);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HeadhunterContext).Assembly);

        // VoterConfiguration's expression is SQL Server's T-SQL.
        if (Database.IsNpgsql())
            modelBuilder.Entity<Voter>().Property(x => x.HAS_MAILING_ADDRESS)
                .HasComputedColumnSql("""CASE WHEN length("MAILING_ADDRESS_LINE_ONE") > 0 THEN TRUE ELSE FALSE END""", stored: true);
    }
}
