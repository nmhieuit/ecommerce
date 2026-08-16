namespace Products.Api.Data;

/// <summary>
/// The catalog every environment starts with (004-minimal-shopping-spa spec FR-018: the walkthrough
/// must be reachable "without manual data setup").
/// </summary>
/// <remarks>
/// <para>
/// Applied through EF Core's <c>HasData</c> in <see cref="ProductsDbContext.OnModelCreating"/>, so
/// the rows are part of the migration history: inserted exactly once, removed on rollback, and
/// never dependent on a startup code path that has to work out whether it already ran
/// (004 research.md Decision 10).
/// </para>
/// <para>
/// The identifiers are literals rather than generated values on purpose. quickstart.md quotes these
/// prices, the end-to-end walkthrough selects products by these names, and the seed tests assert
/// these identifiers — a value that changed per environment would make all three unusable.
/// </para>
/// <para>
/// Three rather than one so that a basket with more than a single line, and a total worth reading,
/// are both demonstrable.
/// </para>
/// </remarks>
public static class CatalogSeed
{
    public static IReadOnlyList<Product> Products { get; } =
    [
        new Product
        {
            Id = new Guid("9f8d6b1e-0001-4000-8000-000000000001"),
            Name = "Field Notes Notebook",
            Price = 12.50m,
        },
        new Product
        {
            Id = new Guid("9f8d6b1e-0001-4000-8000-000000000002"),
            Name = "Ceramic Pour-Over Set",
            Price = 48.00m,
        },
        new Product
        {
            Id = new Guid("9f8d6b1e-0001-4000-8000-000000000003"),
            Name = "Linen Apron",
            Price = 34.25m,
        },
    ];
}
