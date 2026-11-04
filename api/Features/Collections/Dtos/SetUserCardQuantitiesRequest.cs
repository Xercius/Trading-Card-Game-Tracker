namespace api.Features.Collections.Dtos;

public sealed record SetUserCardQuantitiesRequest
{
    private int? _quantityOwned;
    public int? QuantityOwned
    {
        get => _quantityOwned;
        init => _quantityOwned = value;
    }
    public int? OwnedQty
    {
        get => _quantityOwned;
        init => _quantityOwned = value;
    }

    private int? _quantityWanted;
    public int? QuantityWanted
    {
        get => _quantityWanted;
        init => _quantityWanted = value;
    }
    public int? WantedQty
    {
        get => _quantityWanted;
        init => _quantityWanted = value;
    }

    private int? _quantityProxyOwned;
    public int? QuantityProxyOwned
    {
        get => _quantityProxyOwned;
        init => _quantityProxyOwned = value;
    }
    public int? ProxyQty
    {
        get => _quantityProxyOwned;
        init => _quantityProxyOwned = value;
    }

    public SetUserCardQuantitiesRequest() { }

    public SetUserCardQuantitiesRequest(int quantityOwned, int quantityWanted, int quantityProxyOwned)
    {
        _quantityOwned = quantityOwned;
        _quantityWanted = quantityWanted;
        _quantityProxyOwned = quantityProxyOwned;
    }
}
