namespace Marketplace.Core.Common;

public abstract class Entity<TKey>
{
    public TKey Id { get; set; } = default!;
}
