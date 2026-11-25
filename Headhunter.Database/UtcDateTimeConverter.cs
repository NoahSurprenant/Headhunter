using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Headhunter.Database;

/// <summary>
/// Postgres's timestamptz takes only UTC DateTimes. The voter file's dates are dates, with no zone, so they are
/// taken as UTC; SQL Server's datetime2 keeps them as they are and isn't given this converter.
/// </summary>
public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
