using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Unravel.Application.Journey.Ports;
using Unravel.Domain.Knowledge;
using Unravel.Infrastructure.Persistence;

namespace Unravel.Infrastructure.Knowledge;

/// <summary>
/// Cache in-memory de <see cref="KnowledgeGraph"/> por trilha. Coalesce
/// builds concorrentes pra mesma trailId via <see cref="Lazy{T}"/> +
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>: chamadas paralelas
/// resultam em uma única execução do builder.
///
/// <para>É <c>Singleton</c> no DI — manter o cache vivo entre requisições é
/// o ponto inteiro. O DbContext é resolvido por escopo dentro do builder
/// (usa <see cref="IServiceScopeFactory"/>) para não capturar o DbContext
/// do request original.</para>
/// </summary>
public sealed class MemoryKnowledgeGraphCache : IKnowledgeGraphCache
{
    private readonly ConcurrentDictionary<int, Lazy<Task<KnowledgeGraph>>> _cache = new();
    private readonly IServiceScopeFactory _scopeFactory;

    public MemoryKnowledgeGraphCache(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    public Task<KnowledgeGraph> GetOrBuildAsync(int trailId, CancellationToken ct = default)
    {
        var lazy = _cache.GetOrAdd(trailId,
            id => new Lazy<Task<KnowledgeGraph>>(() => BuildAsync(id, ct)));

        return lazy.Value;
    }

    public void Invalidate(int trailId) => _cache.TryRemove(trailId, out _);

    private async Task<KnowledgeGraph> BuildAsync(int trailId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var builder = scope.ServiceProvider.GetRequiredService<IKnowledgeGraphBuilder>();

        var contents = await db.Content
            .Where(c => c.TrailId == trailId && c.IsActive)
            .AsNoTracking()
            .ToListAsync(ct);

        return builder.Build(trailId, contents);
    }
}
