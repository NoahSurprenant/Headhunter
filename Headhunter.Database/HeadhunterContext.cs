using Microsoft.EntityFrameworkCore;

namespace Headhunter.Database;

public class HeadhunterContext : DbContext
{
    public HeadhunterContext() : base()
    {
    }

    public HeadhunterContext(DbContextOptions<HeadhunterContext> options) : base(options)
    {
    }

    public DbSet<MichiganVoterRecord> MichiganVoterRecords { get; set; }
    public DbSet<Address> Addresses { get; set; }
    public DbSet<Voter> Voters { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HeadhunterContext).Assembly);
    }
}
