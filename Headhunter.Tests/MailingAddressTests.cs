using Headhunter.API;

namespace Headhunter.Tests;

public class MailingAddressTests
{
    [Test]
    public void NonEmptyLines_DropsBlankAndWhitespaceOnlyLines()
    {
        var lines = MailingAddress.NonEmptyLines("PO Box 1", "", "  ", "Chicago, IL 60601", "");

        Assert.That(lines, Is.EqualTo(new[] { "PO Box 1", "Chicago, IL 60601" }));
    }

    [Test]
    public void NonEmptyLines_AllBlank_ReturnsEmpty()
    {
        var lines = MailingAddress.NonEmptyLines("", "", "", "", "");

        Assert.That(lines, Is.Empty);
    }

    [Test]
    public void IsOutOfState_NoLines_IsFalse()
    {
        Assert.That(MailingAddress.IsOutOfState([], "MI"), Is.False);
    }

    [TestCase("Chicago, IL 60601", "MI", true)]
    [TestCase("Lansing, MI 48901", "MI", false)]
    [TestCase("Lansing, mi 48901", "MI", false)] // case-insensitive
    [TestCase("Chicago, IL 60601-1234", "MI", true)] // ZIP+4
    public void IsOutOfState_ComparesTheLastLinesStateToRegistered(string lastLine, string registeredState, bool expected)
    {
        var lines = new[] { "PO Box 1", lastLine };

        Assert.That(MailingAddress.IsOutOfState(lines, registeredState), Is.EqualTo(expected));
    }

    [Test]
    public void IsOutOfState_LastLineWithoutAStateAndZip_IsFalse()
    {
        // "If simple" per the feature request: free text that doesn't end in "ST 12345" isn't flagged either way.
        var lines = new[] { "PO Box 1", "Attn: Mail Room" };

        Assert.That(MailingAddress.IsOutOfState(lines, "MI"), Is.False);
    }

    [Test]
    public void IsOutOfState_OnlyLooksAtTheLastNonEmptyLine()
    {
        // The state/ZIP-looking text on an earlier line doesn't matter; only the last line does.
        var lines = new[] { "Chicago, IL 60601", "Lansing, MI 48901" };

        Assert.That(MailingAddress.IsOutOfState(lines, "MI"), Is.False);
    }
}
