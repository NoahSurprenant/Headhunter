using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Headhunter.Database.Configuration;
internal class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("Addresses")
        .HasKey(x => x.ID);

        builder.Property(x => x.Latitude).HasPrecision(15, 12);
        builder.Property(x => x.Longitude).HasPrecision(15, 12);

        builder.HasIndex(x => new { x.Latitude, x.Longitude }).HasDatabaseName("IX_Addresses_Latitude_Longitude");

        // Bounded so CitySuggestions' StartsWith (LIKE 'x%') can seek an index; nvarchar(max) can't be an index key.
        // The longest city in the data is 20 characters.
        builder.Property(x => x.CITY).HasMaxLength(100);
        builder.HasIndex(x => x.CITY);
    }
}
