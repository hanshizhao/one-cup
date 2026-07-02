using Microsoft.EntityFrameworkCore;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence;

/// <summary>
/// 泛型仓储的 EF Core 实现。
/// </summary>
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    private readonly OneCupDbContext _context;
    private readonly DbSet<T> _dbSet;

    public Repository(OneCupDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _dbSet.FindAsync([id], cancellationToken);

    public Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default)
        => ListAsync(null, cancellationToken);

    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T>? spec, CancellationToken cancellationToken = default)
    {
        var query = ApplySpecification(spec);
        return await query.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(ISpecification<T>? spec, CancellationToken cancellationToken = default)
        => await ApplySpecification(spec).CountAsync(cancellationToken);

    public async Task<bool> AnyAsync(ISpecification<T>? spec, CancellationToken cancellationToken = default)
        => await ApplySpecification(spec).AnyAsync(cancellationToken);

    public async Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, CancellationToken cancellationToken = default)
        => await ApplySpecification(spec).FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        => await _dbSet.AddAsync(entity, cancellationToken);

    public void Update(T entity) => _dbSet.Update(entity);

    public void Remove(T entity) => _dbSet.Remove(entity);

    /// <summary>
    /// 将 Specification 翻译为 EF Core IQueryable:应用 Criteria / Includes / OrderBy / 分页。
    /// </summary>
    private IQueryable<T> ApplySpecification(ISpecification<T>? spec)
    {
        var query = _context.Set<T>().AsQueryable();
        if (spec is null) return query;
        if (spec.Criteria is not null) query = query.Where(spec.Criteria);
        foreach (var include in spec.Includes) query = query.Include(include);   // 字符串路径,EF Core 支持 "Roles.Permissions"
        if (spec.OrderBy is not null) query = query.OrderBy(spec.OrderBy);
        if (spec.OrderByDescending is not null) query = query.OrderByDescending(spec.OrderByDescending);
        if (spec.Skip is not null) query = query.Skip(spec.Skip.Value);
        if (spec.Take is not null) query = query.Take(spec.Take.Value);
        return query;
    }
}
