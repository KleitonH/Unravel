using Unravel.Domain.Entities;

namespace Unravel.Application.Forge.Ports;

/// <summary>
/// Leituras que o <c>GetChallengePoolUseCase</c> precisa do banco sem
/// vazar <c>ApplicationDbContext</c> para a Application (mesma motivação
/// do <c>IJourneyReadModel</c>).
/// </summary>
public interface IForgeReadModel
{
    /// <summary>Content ativo pelo Id. Retorna <c>null</c> se inexistente
    /// ou desativado.</summary>
    Task<Content?> GetContentAsync(int contentId, CancellationToken ct = default);
}
