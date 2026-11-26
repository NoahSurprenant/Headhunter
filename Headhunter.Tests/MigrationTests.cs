using Headhunter.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Headhunter.Tests;

public class MigrationTests
{
    private sealed class AddressNames
    {
        public string County { get; set; } = "";
        public string Jurisdiction { get; set; } = "";
        public string School { get; set; } = "";
        public string CountyCode { get; set; } = "";
    }

    private static Address NewAddress(Guid id, string? county, string? jurisdiction, string? school) => new()
    {
        ID = id, STREET_NUMBER_PREFIX = "", STREET_NUMBER = "1", STREET_NUMBER_SUFFIX = "", DIRECTION_PREFIX = "",
        STREET_NAME = "Main", STREET_TYPE = "St", DIRECTION_SUFFIX = "", EXTENSION = "", FullStreetAddress = "1 Main St",
        CITY = "Town", STATE = "MI", ZIP_CODE = "48000", PRECINCT = "1", WARD = "", VILLAGE_DISTRICT_CODE = "",
        VILLAGE_DISTRICT_NAME = "", VILLAGE_PRECINCT = "", SCHOOL_PRECINCT = "",
        COUNTY_CODE = county, JURISDICTION_CODE = jurisdiction, SCHOOL_DISTRICT_CODE = school,
    };

    [Test]
    public async Task Down_PutsTheNamesBackOnTheAddresses_AndUpTakesThemBackOut()
    {
        var create = await DatabaseFixture.CreateScratchDatabaseAsync($"migration_{Guid.NewGuid():N}");
        var named = Guid.NewGuid();
        var unnamed = Guid.NewGuid();
        await using (var db = create())
        {
            db.Counties.Add(new County { Code = "01", Name = "ALPHA" });
            await db.SaveChangesAsync();
            db.Jurisdictions.Add(new Jurisdiction { CountyCode = "01", Code = "100", Name = "CITY OF ALPHA" });
            db.SchoolDistricts.Add(new SchoolDistrict { Code = "S1", Name = "ALPHA SCHOOLS" });
            await db.SaveChangesAsync();
            db.Addresses.AddRange(NewAddress(named, "01", "100", "S1"), NewAddress(unnamed, null, null, null));
            await db.SaveChangesAsync();
        }

        string previous;
        await using (var db = create())
        {
            var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
            previous = applied[^2];
            await db.GetService<IMigrator>().MigrateAsync(previous);

            async Task<AddressNames> Names(Guid id) => (await db.Database.SqlQueryRaw<AddressNames>(
                "SELECT \"COUNTY_NAME\" AS \"County\", \"JURISDICTION_NAME\" AS \"Jurisdiction\", \"SCHOOL_DISTRICT_NAME\" AS \"School\", " +
                "\"COUNTY_CODE\" AS \"CountyCode\" FROM \"Addresses\" WHERE \"ID\" = {0}", id).ToListAsync()).Single();

            var down = await Names(named);
            Assert.Multiple(() =>
            {
                Assert.That(down.County, Is.EqualTo("ALPHA"));
                Assert.That(down.Jurisdiction, Is.EqualTo("CITY OF ALPHA"));
                Assert.That(down.School, Is.EqualTo("ALPHA SCHOOLS"));
            });
            var none = await Names(unnamed);
            Assert.That((none.County, none.CountyCode), Is.EqualTo(("", "")));

            await db.GetService<IMigrator>().MigrateAsync();
        }

        await using var after = create();
        var counties = await after.Counties.ToListAsync();
        Assert.That(counties.Select(c => (c.Code, c.Name)), Is.EqualTo(new[] { ("01", "ALPHA") }));
        Assert.That((await after.Jurisdictions.ToListAsync()).Select(j => (j.CountyCode, j.Code, j.Name)),
            Is.EqualTo(new[] { ("01", "100", "CITY OF ALPHA") }));
        var back = await after.Addresses.Where(a => a.ID == named)
            .Select(a => new { County = a.County!.Name, Jurisdiction = a.Jurisdiction!.Name, School = a.SchoolDistrict!.Name }).SingleAsync();
        Assert.That((back.County, back.Jurisdiction, back.School), Is.EqualTo(("ALPHA", "CITY OF ALPHA", "ALPHA SCHOOLS")));
        var blank = await after.Addresses.Where(a => a.ID == unnamed).SingleAsync();
        Assert.That((blank.COUNTY_CODE, blank.JURISDICTION_CODE, blank.SCHOOL_DISTRICT_CODE), Is.EqualTo(((string?)null, (string?)null, (string?)null)));
    }

    [Test]
    public async Task OnPostgres_OnlyTheKeyColumnsAreCaseSensitive()
    {
        if (DatabaseFixture.Provider != DatabaseProvider.Postgres) Assert.Ignore("Collations are a Postgres matter.");
        await using var db = DatabaseFixture.CreateContext();

        async Task<string> Collation(string column) => (await db.Database.SqlQueryRaw<string>(
            "SELECT c.collname AS \"Value\" FROM pg_attribute a JOIN pg_collation c ON c.oid = a.attcollation " +
            "WHERE a.attrelid = '\"Addresses\"'::regclass AND a.attname = {0}", column).ToListAsync()).Single();

        Assert.Multiple(async () =>
        {
            Assert.That(await Collation("COUNTY_CODE"), Is.EqualTo("C"));
            Assert.That(await Collation("ZIP_CODE"), Is.EqualTo("case_insensitive"));
            Assert.That(await Collation("VILLAGE_DISTRICT_CODE"), Is.EqualTo("case_insensitive"));
        });
    }
}
