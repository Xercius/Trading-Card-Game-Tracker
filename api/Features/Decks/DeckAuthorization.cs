using api.Authentication;

namespace api.Features.Decks;

public static class DeckAuthorization
{
    public static bool OwnsDeckOrAdmin(CurrentUser user, int deckOwnerId)
        => user.IsAdmin || user.Id == deckOwnerId;
}
