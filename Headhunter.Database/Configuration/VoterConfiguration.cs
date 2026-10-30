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
        // The voter search by birth year, age or star sign (all filter YEAR_OF_BIRTH): 15 s on 8.5M voters without it.
        builder.HasIndex(x => x.YEAR_OF_BIRTH);

        // MAILING_ADDRESS_LINE_ONE is nvarchar(max) (the CSV import leaves it "" rather than NULL when a voter has
        // no mailing address - see Headhunter.CSVDump/Worker.cs), and SQL Server can't key an index on a LOB
        // column. This persisted bit is what the "Mailing address differs" filter seeks instead of comparing
        // MAILING_ADDRESS_LINE_ONE to '' on all 8.5M rows.
        builder.Property(x => x.HAS_MAILING_ADDRESS)
            .HasComputedColumnSql("(CASE WHEN [MAILING_ADDRESS_LINE_ONE] <> N'' THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END)", stored: true);
        builder.HasIndex(x => x.HAS_MAILING_ADDRESS);
    }
}
