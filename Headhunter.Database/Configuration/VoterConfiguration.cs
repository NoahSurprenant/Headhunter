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
    }
}
