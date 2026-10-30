using System.Globalization;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Headhunter.Database;

namespace Headhunter.API;

/// <summary>
/// One row of the voters page's advanced search: a field, an operator and its value (two for "between"), e.g.
/// birthYear between 1990 and 1999. Everything is a string as typed; <see cref="SearchConditions"/> checks it.
/// </summary>
public class SearchConditionDto
{
    public string Field { get; set; } = "";
    public string Operator { get; set; } = "";
    public string Value { get; set; } = "";
    /// <summary>The upper end, for "between" only.</summary>
    public string? Value2 { get; set; }
}

/// <summary>The voters list's search (configuration section "Search").</summary>
public class SearchOptions
{
    /// <summary>How long one search may run before the API gives up and asks for a narrower one. Conditions that
    /// can't use an index (contains, ends with, is not) scan millions of rows.</summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>The most advanced conditions one search takes.</summary>
    public int MaxConditions { get; set; } = 10;
}

/// <summary>A condition that names an unknown field or operator, or has a value of the wrong type: a 400.</summary>
public class SearchConditionException(string message) : Exception(message);

/// <summary>
/// Turns <see cref="SearchConditionDto"/>s into typed LINQ filters on whitelisted columns. No SQL is built from
/// strings: each field is a property expression, each value a parameter. Equals, starts with and ranges on the
/// indexed columns (names, city, street, birth year) stay sargable (=, LIKE 'x%', &gt;= and &lt;=).
/// </summary>
public static class SearchConditions
{
    private enum Kind { Text, Number, Date, Choice }

    private static readonly string[] TextOperators = ["equals", "notEquals", "startsWith", "endsWith", "contains"];
    private static readonly string[] RangeOperators = ["eq", "ne", "lt", "le", "gt", "ge", "between"];
    private static readonly string[] ChoiceOperators = ["is", "isNot"];

    // The longest name or address column is nvarchar(100); longer can't match anything.
    private const int MaxTextLength = 100;

    private sealed record Field(Kind Kind, LambdaExpression Selector, Func<string, string, object>? ParseChoice = null);

    private static Field Text(Expression<Func<Voter, string>> selector) => new(Kind.Text, selector);

    private static readonly Regex StatusCode = new("^[A-Za-z0-9]{1,10}$", RegexOptions.CultureInvariant);

    // By the name the client sends, any case. Age has no column of its own: it becomes a birth year range.
    private static readonly Dictionary<string, Field> Fields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["firstName"] = Text(v => v.FIRST_NAME),
        ["middleName"] = Text(v => v.MIDDLE_NAME),
        ["lastName"] = Text(v => v.LAST_NAME),
        ["street"] = Text(v => v.Address.FullStreetAddress),
        ["city"] = Text(v => v.Address.CITY),
        ["zipCode"] = Text(v => v.Address.ZIP_CODE),
        ["birthYear"] = new(Kind.Number, (Expression<Func<Voter, int>>)(v => v.YEAR_OF_BIRTH)),
        ["age"] = new(Kind.Number, (Expression<Func<Voter, int>>)(v => v.YEAR_OF_BIRTH)),
        ["registrationDate"] = new(Kind.Date, (Expression<Func<Voter, DateTime>>)(v => v.REGISTRATION_DATE)),
        ["gender"] = new(Kind.Choice, (Expression<Func<Voter, string>>)(v => v.GENDER), (field, value) =>
            value.ToUpperInvariant() is "M" or "F" or "X" ? value.ToUpperInvariant() : throw Invalid(field, value, "M, F or X")),
        ["permanentAbsentee"] = new(Kind.Choice, (Expression<Func<Voter, bool>>)(v => v.IS_PERM_AV_BALLOT_VOTER), (field, value) =>
            value.ToLowerInvariant() switch
            {
                "yes" or "true" => true,
                "no" or "false" => false,
                _ => throw Invalid(field, value, "yes or no"),
            }),
        ["voterStatus"] = new(Kind.Choice, (Expression<Func<Voter, string>>)(v => v.VOTER_STATUS_TYPE_CODE), (field, value) =>
            StatusCode.IsMatch(value) ? value.ToUpperInvariant() : throw Invalid(field, value, "a status code such as A")),
    };

    /// <summary>Every condition, ANDed onto <paramref name="voters"/>. Throws <see cref="SearchConditionException"/>
    /// for anything not on the whitelist or not of the field's type, before anything is queried.</summary>
    /// <param name="today">For age: this year minus the age is the birth year (or the one before).</param>
    public static IQueryable<Voter> Apply(IQueryable<Voter> voters, IReadOnlyList<SearchConditionDto> conditions, int maxConditions, DateTime today)
    {
        if (conditions.Count > maxConditions)
            throw new SearchConditionException($"At most {maxConditions} conditions, not {conditions.Count}.");

        foreach (var condition in conditions)
            voters = voters.Where(Build(condition, today));
        return voters;
    }

    public static Expression<Func<Voter, bool>> Build(SearchConditionDto condition, DateTime today)
    {
        var name = condition.Field?.Trim() ?? "";
        if (!Fields.TryGetValue(name, out var field))
            throw new SearchConditionException($"Unknown field \"{name}\". Use one of: {string.Join(", ", Fields.Keys)}.");

        var operators = field.Kind switch
        {
            Kind.Text => TextOperators,
            Kind.Choice => ChoiceOperators,
            _ => RangeOperators,
        };
        var op = operators.FirstOrDefault(o => string.Equals(o, condition.Operator?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new SearchConditionException(
                $"Unknown operator \"{condition.Operator}\" for {name}. Use one of: {string.Join(", ", operators)}.");

        var value = condition.Value?.Trim() ?? "";
        if (value.Length == 0)
            throw new SearchConditionException($"{name} {op} needs a value.");
        var value2 = condition.Value2?.Trim() ?? "";
        if (op == "between" && value2.Length == 0)
            throw new SearchConditionException($"{name} between needs two values.");

        var column = field.Selector.Body;
        var body = field.Kind switch
        {
            Kind.Text => TextCondition(column, op, value.Length <= MaxTextLength ? value
                : throw new SearchConditionException($"{name} is at most {MaxTextLength} characters.")),
            Kind.Choice => ChoiceCondition(column, op, field.ParseChoice!(name, value)),
            Kind.Number when name.Equals("age", StringComparison.OrdinalIgnoreCase) =>
                AgeCondition(column, op, ParseInt(name, value, 0, 150), op == "between" ? ParseInt(name, value2, 0, 150) : 0, today.Year),
            Kind.Number => RangeCondition(column, op,
                ParseInt(name, value, 1800, 2200), op == "between" ? ParseInt(name, value2, 1800, 2200) : 0, next: y => y + 1, exact: true),
            Kind.Date => RangeCondition(column, op,
                ParseDate(name, value), op == "between" ? ParseDate(name, value2) : default, next: d => d.AddDays(1), exact: false),
            _ => throw new InvalidOperationException(),
        };
        return Expression.Lambda<Func<Voter, bool>>(body, field.Selector.Parameters);
    }

    private static readonly System.Reflection.MethodInfo StartsWith = typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;
    private static readonly System.Reflection.MethodInfo EndsWith = typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!;
    private static readonly System.Reflection.MethodInfo Contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    // Starts with is LIKE @p ESCAPE '\' with the parameter 'x%' (EF escapes % and _ in it): an index seek.
    private static Expression TextCondition(Expression column, string op, string value)
    {
        var p = Parameter(value);
        return op switch
        {
            "equals" => Expression.Equal(column, p),
            "notEquals" => Expression.NotEqual(column, p),
            "startsWith" => Expression.Call(column, StartsWith, p),
            "endsWith" => Expression.Call(column, EndsWith, p),
            "contains" => Expression.Call(column, Contains, p),
            _ => throw new InvalidOperationException(op),
        };
    }

    private static Expression ChoiceCondition(Expression column, string op, object value)
    {
        var p = Parameter(value, column.Type);
        return op == "is" ? Expression.Equal(column, p) : Expression.NotEqual(column, p);
    }

    /// <summary>
    /// Numbers and dates. <paramref name="exact"/> (whole numbers): equals is =. Otherwise (dates, which are datetime
    /// columns) a day is the range from its midnight to the next one, so a time of day can't hide a match.
    /// </summary>
    private static Expression RangeCondition<T>(Expression column, string op, T value, T value2, Func<T, T> next, bool exact)
    {
        Expression Ge(T v) => Expression.GreaterThanOrEqual(column, Parameter(v));
        Expression Lt(T v) => Expression.LessThan(column, Parameter(v));
        // On or before the day/number: before the next one.
        Expression Le(T v) => exact ? Expression.LessThanOrEqual(column, Parameter(v)) : Lt(next(v));

        if (op == "between" && Comparer<T>.Default.Compare(value, value2) > 0)
            (value, value2) = (value2, value);

        return op switch
        {
            "eq" => exact ? Expression.Equal(column, Parameter(value)) : Expression.AndAlso(Ge(value), Lt(next(value))),
            "ne" => exact ? Expression.NotEqual(column, Parameter(value)) : Expression.OrElse(Lt(value), Ge(next(value))),
            "lt" => Lt(value),
            "le" => Le(value),
            "gt" => exact ? Expression.GreaterThan(column, Parameter(value)) : Ge(next(value)),
            "ge" => Ge(value),
            "between" => Expression.AndAlso(Ge(value), Le(value2)),
            _ => throw new InvalidOperationException(op),
        };
    }

    /// <summary>
    /// Age as birth years, the way the simple form's Age is: someone aged A was born this year minus A, or the year
    /// before if their birthday hasn't come yet. So age A is the birth years [Y-A-1, Y-A], and the others follow:
    /// younger than A is born after Y-A-1, older than A born before Y-A, and so on (could be, rather than surely).
    /// </summary>
    private static Expression AgeCondition(Expression column, string op, int age, int age2, int year)
    {
        if (op == "between" && age > age2)
            (age, age2) = (age2, age);

        // The oldest and the youngest birth year for an age.
        int Earliest(int a) => year - a - 1;
        int Latest(int a) => year - a;
        Expression Ge(int y) => Expression.GreaterThanOrEqual(column, Parameter(y));
        Expression Le(int y) => Expression.LessThanOrEqual(column, Parameter(y));

        return op switch
        {
            "eq" => Expression.AndAlso(Ge(Earliest(age)), Le(Latest(age))),
            "ne" => Expression.OrElse(Expression.LessThan(column, Parameter(Earliest(age))), Expression.GreaterThan(column, Parameter(Latest(age)))),
            // Ages A-1 and younger: born from Y-A on.
            "lt" => Ge(Latest(age)),
            "le" => Ge(Earliest(age)),
            // Ages A+1 and older: born up to Y-A-1.
            "gt" => Le(Earliest(age)),
            "ge" => Le(Latest(age)),
            "between" => Expression.AndAlso(Ge(Earliest(age2)), Le(Latest(age))),
            _ => throw new InvalidOperationException(op),
        };
    }

    // A property of a constant object, as a closure is: EF sends it as a SQL parameter, not as literal SQL.
    private static Expression Parameter<T>(T value) => Parameter(value!, typeof(T));

    private static Expression Parameter(object value, Type type)
    {
        var holder = Activator.CreateInstance(typeof(Holder<>).MakeGenericType(type), value)!;
        return Expression.Property(Expression.Constant(holder), "Value");
    }

    private sealed class Holder<T>(T value)
    {
        public T Value { get; } = value;
    }

    private static int ParseInt(string field, string value, int min, int max) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= min && n <= max
            ? n
            : throw Invalid(field, value, $"a whole number from {min} to {max}");

    private static DateTime ParseDate(string field, string value) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) && d.Year is >= 1800 and <= 2200
            ? d
            : throw Invalid(field, value, "a date as yyyy-MM-dd");

    private static SearchConditionException Invalid(string field, string value, string expected) =>
        new($"\"{value}\" is not a valid {field}: expected {expected}.");
}
