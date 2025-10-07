namespace api.Shared;

public static class UserCardMath
{
    public static int AddClamped(int current, int delta)
    {
        var total = (long)current + delta;
        return total > int.MaxValue ? int.MaxValue : (int)total;
    }
}
