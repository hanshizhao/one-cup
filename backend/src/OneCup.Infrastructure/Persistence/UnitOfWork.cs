using OneCup.Application.Interfaces;

namespace OneCup.Infrastructure.Persistence;

/// <summary>
/// 工作单元实现,封装 DbContext.SaveChangesAsync。
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly OneCupDbContext _context;

    public UnitOfWork(OneCupDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
