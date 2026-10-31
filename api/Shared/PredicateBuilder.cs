using System.Linq.Expressions;

namespace api.Shared;

internal static class PredicateBuilder
{
    public static Expression<Func<T, bool>> Or<T>(
        this Expression<Func<T, bool>> expr1,
        Expression<Func<T, bool>> expr2)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var left = new ReplaceParameterVisitor(expr1.Parameters[0], parameter).Visit(expr1.Body);
        var right = new ReplaceParameterVisitor(expr2.Parameters[0], parameter).Visit(expr2.Body);
        return Expression.Lambda<Func<T, bool>>(Expression.OrElse(left, right), parameter);
    }

    private sealed class ReplaceParameterVisitor(
        ParameterExpression source,
        ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == source ? target : base.VisitParameter(node);
    }
}

