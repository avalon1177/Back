using Marketplace.Core.Common;

namespace Marketplace.Core.Entities;

public class CompanySchedule : Entity<Guid>
{
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = default!;
    public DayOfWeek Day { get; set; }
    public TimeSpan? OpenTime { get; set; }
    public TimeSpan? CloseTime { get; set; }
    public bool IsClosed { get; set; }
}
