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

        // StreetSuggestions' two prefix searches, and the voter search's equality filter on the address. The
        // longest values in the data are 46 (full address) and 27 (street name) characters.
        builder.Property(x => x.FullStreetAddress).HasMaxLength(100);
        builder.Property(x => x.STREET_NAME).HasMaxLength(100);
        builder.HasIndex(x => x.FullStreetAddress);
        builder.HasIndex(x => x.STREET_NAME).IncludeProperties(x => x.FullStreetAddress);

        // Possible relatives' "same street, same city" branch (ApiController.Relatives): both columns are an
        // equality match, so either order seeks equally well - STREET_NAME leads so this index doesn't overlap
        // with the CITY index above and start competing with CitySuggestions' city-only prefix seek.
        builder.HasIndex(x => new { x.STREET_NAME, x.CITY }).HasDatabaseName("IX_Addresses_STREET_NAME_CITY");
    }
}
