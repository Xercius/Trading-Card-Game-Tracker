using api.Models;
using System.Linq.Expressions;

namespace api.Shared;

public static class CardAvailabilityHelper
{
    public static readonly Expression<Func<UserCard, int>> AvailabilityExpression =
        uc => Math.Max(0, uc.QuantityOwned);

    public static readonly Expression<Func<UserCard, int>> AvailabilityWithProxiesExpression =
        uc => Math.Max(0, uc.QuantityOwned + uc.QuantityProxyOwned);

    public static (int Available, int AvailableWithProxies) Calculate(int owned, int proxy, int assigned = 0)
    {
        var available = Math.Max(0, owned - assigned);
        var availableWithProxies = Math.Max(0, owned + proxy - assigned);
        return (available, availableWithProxies);
    }
}
