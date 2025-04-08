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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MichiganVoterRecord>()
            .ToTable("MichiganVoterRecords")
            .HasKey(x => x.ID);

        modelBuilder.Entity<MichiganVoterRecord>()
            .Property(x => x.ID)
            .ValueGeneratedNever();
    }
}
