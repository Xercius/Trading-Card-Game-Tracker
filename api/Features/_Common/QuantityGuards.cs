using api.Shared;

namespace api.Features._Common;

/// <summary>
/// Provides helper guards for normalizing card quantity values across features.
/// </summary>
internal static class QuantityGuards
{
    internal const int MinimumQuantity = 0;
    internal const int MaximumQuantity = int.MaxValue;

    /// <summary>
    /// Clamps the provided quantity to the non-negative range.
    /// </summary>
    internal static int Clamp(int value) => value < MinimumQuantity
        ? MinimumQuantity
        : value > MaximumQuantity
            ? MaximumQuantity
            : value;

    /// <summary>
    /// Clamps the provided quantity, accepting long inputs to prevent overflow.
    /// </summary>
    internal static int Clamp(long value) => value < MinimumQuantity
        ? MinimumQuantity
        : value > MaximumQuantity
            ? MaximumQuantity
            : (int)value;

    /// <summary>
    /// Adds a delta to a quantity and clamps the result within the supported range.
    /// </summary>
    internal static int ClampDelta(int current, int delta) => UserCardMath.AddClamped(current, delta);
}
