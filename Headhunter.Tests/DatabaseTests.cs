using Headhunter.Database;
using Microsoft.EntityFrameworkCore;

namespace Headhunter.Tests;

public class DatabaseTests
{
    [Test]
    public async Task Migrations_AreAllAppliedAndMatchTheModel()
    {
        await using var context = SqlServerFixture.CreateContext();

        var pending = await context.Database.GetPendingMigrationsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(pending, Is.Empty);
            Assert.That(context.Database.HasPendingModelChanges(), Is.False,
                "The EF model differs from the last migration snapshot");
        });
    }

    [Test]
    public async Task StartsWith_IsTranslatedToSqlAndCaseInsensitiveUnderDefaultCollation()
    {
        await using var context = SqlServerFixture.CreateContext();

        // SQL_Latin1_General_CP1_CI_AS (the SQL Server default) is case-insensitive; the API relies on that.
        var names = await context.Voters
            .Where(v => v.FIRST_NAME.StartsWith("ada"))
            .Select(v => v.FIRST_NAME)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();

        Assert.That(names, Is.EqualTo(new[] { "Ada", "Adaline" }));
    }

    [Test]
    public async Task VoterToAddress_NavigationLoadsWithSplitQuery()
    {
        await using var context = SqlServerFixture.CreateContext();

        var address = await context.Addresses
            .Include(a => a.Voters)
            .AsSplitQuery()
            .SingleAsync(a => a.ID == TestData.OakAddressId);

        Assert.That(address.Voters.Select(v => v.ID), Is.EquivalentTo(new[] { TestData.AdaOakId, TestData.BrunoId }));
    }

    [Test]
    public async Task Voter_IsLinkedToItsRawMichiganVoterRecord()
    {
        await using var context = SqlServerFixture.CreateContext();

        var voter = await context.Voters
            .Include(v => v.MichiganVoterRecord)
            .SingleAsync(v => v.ID == TestData.CleoId);

        Assert.That(voter.MichiganVoterRecord.FIRST_NAME, Is.EqualTo("Cleo"));
    }
}
