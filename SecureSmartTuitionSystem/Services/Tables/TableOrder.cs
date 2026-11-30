using System.Linq.Expressions;
namespace SecureSmartTuitionSystem.Services.Tables;

// Only application-owned expressions are accepted: query-string text never
// becomes a SQL identifier, property path or executable expression.
public static class TableOrder
{
    public static IOrderedQueryable<T> Apply<T>(IQueryable<T> source, IQueryCollection? request,
        string fallback, bool defaultDescending, params (string Key, Expression<Func<T, object?>> Value)[] columns)
    {
        string key = request?["tableSort"].ToString() ?? "";
        var column = columns.FirstOrDefault(x => x.Key == key);
        bool valid = column.Value != null;
        column = valid ? column : columns.Single(x => x.Key == fallback);
        bool descending = valid ? request?["tableDirection"].ToString() == "desc" : defaultDescending;
        var body = column.Value.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } convert) body = convert.Operand;
        var lambda = Expression.Lambda(body, column.Value.Parameters);
        return (IOrderedQueryable<T>)source.Provider.CreateQuery<T>(Expression.Call(typeof(Queryable),
            descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy),
            new[] { typeof(T), body.Type }, source.Expression, Expression.Quote(lambda)));
    }
    public static IOrderedEnumerable<T> Local<T>(IEnumerable<T> source, IQueryCollection? request,
        string fallback, bool defaultDescending, params (string Key, Func<T, IComparable?> Value)[] columns)
    {
        string key = request?["tableSort"].ToString() ?? "";
        var column = columns.FirstOrDefault(x => x.Key == key);
        bool valid = column.Value != null;
        column = valid ? column : columns.Single(x => x.Key == fallback);
        bool descending = valid ? request?["tableDirection"].ToString() == "desc" : defaultDescending;
        return descending ? source.OrderByDescending(column.Value) : source.OrderBy(column.Value);
    }
}
