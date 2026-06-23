namespace Unravel.Application.Gamification.Ports;

/// <summary>
/// UC34 — recarga temporal de vidas (+1/h até o teto). Aplicada de forma
/// "lazy" na leitura do perfil (o usuário sempre vê o valor atual) e em lote
/// por um cron (BackgroundService) que adianta o estado no banco.
/// </summary>
public interface ILifeRefillService
{
    /// <summary>Recarrega as vidas de um usuário até "agora". Persiste e
    /// retorna quantas vidas foram concedidas (0 se nada mudou).</summary>
    Task<int> RefillUserAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Cron: recarrega todos os usuários abaixo do teto. Retorna
    /// quantos usuários receberam ao menos 1 vida.</summary>
    Task<int> RefillAllAsync(DateTime nowUtc, CancellationToken ct = default);
}
