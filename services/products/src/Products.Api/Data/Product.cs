namespace Products.Api.Data;

/// <summary>
/// A product in this service's catalog.
/// </summary>
/// <remarks>
/// Deliberately minimal. This is the read surface the BFF's product-listing route proxies
/// (specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml — <c>Product</c>), not the
/// products service's first domain story: no catalog hierarchy, no pricing rules, no availability.
/// Those arrive with the story that owns them, and this type is expected to grow then.
/// </remarks>
public class Product
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public decimal Price { get; set; }
}
