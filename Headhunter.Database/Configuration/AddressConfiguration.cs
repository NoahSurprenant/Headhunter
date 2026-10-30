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

        // "Not on the map" list: addresses where Matched isn't true (false, or still null) are a small minority
        // of the ~4M rows once geocoding has run, so this lets that filter seek instead of scanning everything.
        builder.HasIndex(x => x.Matched).HasDatabaseName("IX_Addresses_Matched");

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

        // The districts (Headhunter.API/Districts.cs): their suggestions' prefix searches, and the voter search, the
        // globe and the district stats filtering on one. Precincts and wards are numbered within a jurisdiction and
        // commissioner districts within a county, so those indexes lead with the jurisdiction or county, which also
        // serves a search on that alone. Latitude and longitude are included so a district's extent and the globe's
        // view of it come from the index alone (a whole county can be hundreds of thousands of addresses). The
        // values are names and numbers like the city's, so the same 100-character cap.
        builder.Property(x => x.COUNTY_NAME).HasMaxLength(100);
        builder.Property(x => x.COUNTY_COMMISSIONER_DISTRICT_NAME).HasMaxLength(100);
        builder.Property(x => x.JURISDICTION_NAME).HasMaxLength(100);
        builder.Property(x => x.PRECINCT).HasMaxLength(100);
        builder.Property(x => x.WARD).HasMaxLength(100);
        builder.Property(x => x.SCHOOL_DISTRICT_NAME).HasMaxLength(100);
        builder.Property(x => x.STATE_HOUSE_DISTRICT_NAME).HasMaxLength(100);
        builder.Property(x => x.STATE_SENATE_DISTRICT_NAME).HasMaxLength(100);
        builder.Property(x => x.US_CONGRESS_DISTRICT_NAME).HasMaxLength(100);
        builder.HasIndex(x => new { x.COUNTY_NAME, x.COUNTY_COMMISSIONER_DISTRICT_NAME }).IncludeProperties(x => new { x.Latitude, x.Longitude });
        builder.HasIndex(x => new { x.JURISDICTION_NAME, x.PRECINCT }).IncludeProperties(x => new { x.Latitude, x.Longitude });
        builder.HasIndex(x => new { x.JURISDICTION_NAME, x.WARD }).IncludeProperties(x => new { x.Latitude, x.Longitude });
        builder.HasIndex(x => x.SCHOOL_DISTRICT_NAME).IncludeProperties(x => new { x.Latitude, x.Longitude });
        builder.HasIndex(x => x.STATE_HOUSE_DISTRICT_NAME).IncludeProperties(x => new { x.Latitude, x.Longitude });
        builder.HasIndex(x => x.STATE_SENATE_DISTRICT_NAME).IncludeProperties(x => new { x.Latitude, x.Longitude });
        builder.HasIndex(x => x.US_CONGRESS_DISTRICT_NAME).IncludeProperties(x => new { x.Latitude, x.Longitude });
    }
}
