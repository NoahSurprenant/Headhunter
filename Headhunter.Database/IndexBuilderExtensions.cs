using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Headhunter.Database;

internal static class IndexBuilderExtensions
{
    // Both providers' IncludeProperties are in scope, so the plain call is ambiguous; each provider reads only its own annotation.
    public static IndexBuilder<TEntity> IncludeColumns<TEntity>(this IndexBuilder<TEntity> builder, Expression<Func<TEntity, object?>> columns)
    {
        SqlServerIndexBuilderExtensions.IncludeProperties(builder, columns);
        NpgsqlIndexBuilderExtensions.IncludeProperties(builder, columns);
        return builder;
    }
}
