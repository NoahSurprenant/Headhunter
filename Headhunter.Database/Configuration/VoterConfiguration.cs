using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Headhunter.Database.Configuration;
public class VoterConfiguration : IEntityTypeConfiguration<Voter>
{
    public void Configure(EntityTypeBuilder<Voter> builder)
    {
        builder.ToTable("Voters")
            .HasKey(x => x.ID);

        builder.HasOne(x => x.Address)
            .WithMany(x => x.Voters)
            .HasForeignKey(x => x.AddressID);

        builder.Property(x => x.ID)
            .ValueGeneratedNever();

        // Bounded so the name suggestions' StartsWith (LIKE 'x%') and the voter search's equality filters can seek
        // an index; nvarchar(max) can't be an index key. The longest name in the data is 50 characters.
        builder.Property(x => x.FIRST_NAME).HasMaxLength(100);
        builder.Property(x => x.MIDDLE_NAME).HasMaxLength(100);
        builder.Property(x => x.LAST_NAME).HasMaxLength(100);
        builder.HasIndex(x => x.FIRST_NAME);
        builder.HasIndex(x => x.MIDDLE_NAME);
        builder.HasIndex(x => x.LAST_NAME);
    }
}
