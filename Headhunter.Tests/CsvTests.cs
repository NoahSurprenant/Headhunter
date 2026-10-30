using Headhunter.API;

namespace Headhunter.Tests;

public class CsvTests
{
    [TestCase("Ada", "Ada")]
    [TestCase("", "")]
    [TestCase(null, "")]
    [TestCase("12 Oak St, Apt 3", "\"12 Oak St, Apt 3\"")]
    [TestCase("The \"Oaks\"", "\"The \"\"Oaks\"\"\"")]
    [TestCase("two\nlines", "\"two\nlines\"")]
    public void Field_QuotesOnlyWhenItMust(string? value, string expected)
    {
        Assert.That(Csv.Field(value), Is.EqualTo(expected));
    }

    // Spreadsheets run a cell that starts like a formula; the apostrophe makes it text.
    [TestCase("=1+1", "'=1+1")]
    [TestCase("+1", "'+1")]
    [TestCase("-1", "'-1")]
    [TestCase("@SUM(A1)", "'@SUM(A1)")]
    [TestCase("\tTab", "'\tTab")]
    [TestCase("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    public void Field_DefusesFormulas(string value, string expected)
    {
        Assert.That(Csv.Field(value), Is.EqualTo(expected));
    }

    [Test]
    public void Row_JoinsFieldsWithCommas()
    {
        Assert.That(Csv.Row("Ada Q Fakename", "1990", "100 Maple St, Unit 2"), Is.EqualTo("Ada Q Fakename,1990,\"100 Maple St, Unit 2\""));
    }
}
