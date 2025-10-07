namespace api.Shared;

public static class UserCardMath
{
    public static int AddClamped(int current, int delta)
    {
        var total = (long)current + delta;
        if (total > int.MaxValue) return int.MaxValue;
        if (total < 0) return 0;
        return (int)total;
    }
}
