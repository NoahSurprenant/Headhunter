using Headhunter.API.Pagination;

namespace Headhunter.Tests;

public class PaginationFilterTests
{
    [Test]
    public void DefaultsToFirstPageOfTen()
    {
        var filter = new PaginationFilter();

        Assert.Multiple(() =>
        {
            Assert.That(filter.PageNumber, Is.EqualTo(1));
            Assert.That(filter.PageSize, Is.EqualTo(10));
        });
    }

    [TestCase(0, 10, 1, 10)]
    [TestCase(-5, 10, 1, 10)]
    [TestCase(3, 500, 3, 250)]
    [TestCase(2, 250, 2, 250)]
    public void ClampsPageNumberAndSize(int pageNumber, int pageSize, int expectedNumber, int expectedSize)
    {
        var filter = new PaginationFilter(pageNumber, pageSize);

        Assert.Multiple(() =>
        {
            Assert.That(filter.PageNumber, Is.EqualTo(expectedNumber));
            Assert.That(filter.PageSize, Is.EqualTo(expectedSize));
        });
    }
}
