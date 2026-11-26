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
    public DbSet<County> Counties { get; set; }
    public DbSet<Jurisdiction> Jurisdictions { get; set; }
    public DbSet<SchoolDistrict> SchoolDistricts { get; set; }
    public DbSet<StateHouseDistrict> StateHouseDistricts { get; set; }
    public DbSet<StateSenateDistrict> StateSenateDistricts { get; set; }
    public DbSet<UsCongressDistrict> UsCongressDistricts { get; set; }
    public DbSet<CountyCommissionerDistrict> CountyCommissionerDistricts { get; set; }
    public DbSet<Election> Elections { get; set; }
    public DbSet<VoteHistory> VoteHistory { get; set; }

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

    private static readonly HashSet<string> AddressCodeKeys =
    [
        "COUNTY_CODE", "JURISDICTION_CODE", "SCHOOL_DISTRICT_CODE", "STATE_HOUSE_DISTRICT_CODE", "STATE_SENATE_DISTRICT_CODE",
        "US_CONGRESS_DISTRICT_CODE", "COUNTY_COMMISSIONER_DISTRICT_CODE",
    ];

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ICU's root locale, ignoring case only (strength level 2). Nondeterministic, so LIKE on it needs PostgreSQL 18.
        if (Database.IsNpgsql())
            modelBuilder.HasCollation(CaseInsensitiveCollation, locale: "und-u-ks-level2", provider: "icu", deterministic: false);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HeadhunterContext).Assembly);

        // The code columns are keys and foreign keys: compared exactly, not through the case-insensitive collation
        // (which Postgres can not use for a foreign key or a hash join).
        if (Database.IsNpgsql())
            foreach (var property in modelBuilder.Model.GetEntityTypes()
                         .Where(t => t.ClrType != typeof(MichiganVoterRecord))
                         .SelectMany(t => t.GetProperties())
                         .Where(p => p.ClrType == typeof(string) && (p.Name.EndsWith("Code") || (p.DeclaringType.ClrType == typeof(Address) && AddressCodeKeys.Contains(p.Name)))))
                property.SetCollation("C");

        // VoterConfiguration's expression is SQL Server's T-SQL.
        if (Database.IsNpgsql())
            modelBuilder.Entity<Voter>().Property(x => x.HAS_MAILING_ADDRESS)
                .HasComputedColumnSql("""CASE WHEN length("MAILING_ADDRESS_LINE_ONE") > 0 THEN TRUE ELSE FALSE END""", stored: true);
    }
}
