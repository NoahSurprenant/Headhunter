using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Headhunter.Database.Configuration;
public class MichiganVoterRecordConfiguration : IEntityTypeConfiguration<MichiganVoterRecord>
{
    public void Configure(EntityTypeBuilder<MichiganVoterRecord> builder)
    {
        builder.ToTable("MichiganVoterRecords")
            .HasKey(x => x.ID);

        builder.Property(x => x.ID)
            .ValueGeneratedNever();

        builder.HasOne(x => x.Voter)
            .WithOne(x => x.MichiganVoterRecord)
            .HasForeignKey<Voter>(x => x.ID)
            .IsRequired(false);
    }
}
