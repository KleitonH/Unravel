using Unravel.Domain.Tokens;

namespace Unravel.Application.Tokens.Ports;

/// <summary>
/// PR 52 — orquestra crédito/débito de "centímetros de lã" do moderador.
/// Cada movimentação é atômica (saldo + transaction log na mesma TX).
/// Bloqueia débito que deixaria saldo negativo — caller verifica antes
/// ou trata <see cref="InsufficientTokensException"/>.
/// </summary>
public interface IModeratorTokenService
{
    /// <summary>Saldo atual. Cria registro com 0 se não existir.</summary>
    Task<int> GetBalanceAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Adiciona delta ao saldo + grava transaction. Idempotente
    /// se chamado com mesmo metadata (caller deve garantir unicidade do
    /// motivo+contexto).</summary>
    Task<int> CreditAsync(
        Guid userId, int amountCm, TokenTransactionReason reason,
        string? metadata = null, CancellationToken ct = default);

    /// <summary>Subtrai do saldo. Lança <see cref="InsufficientTokensException"/>
    /// se saldo &lt; amount.</summary>
    Task<int> DebitAsync(
        Guid userId, int amountCm, TokenTransactionReason reason,
        string? metadata = null, CancellationToken ct = default);

    /// <summary>Garante que moderador tem welcome bonus inicial. Idempotente:
    /// chamar 2x não dobra o bônus (checa via TransactionReason).</summary>
    Task EnsureWelcomeBonusAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Histórico de transações ordenado por data desc.
    /// Paginação simples via take/skip.</summary>
    Task<IReadOnlyList<ModeratorTokenTransaction>> GetHistoryAsync(
        Guid userId, int take = 30, int skip = 0, CancellationToken ct = default);
}

/// <summary>Saldo insuficiente pra débito. Caller (controllers de geração)
/// deve traduzir pra HTTP 402 Payment Required ou 422 com mensagem clara.</summary>
public sealed class InsufficientTokensException : Exception
{
    public int BalanceCm { get; }
    public int RequiredCm { get; }
    public InsufficientTokensException(int balance, int required)
        : base($"Saldo insuficiente: {balance} cm. Necessário: {required} cm.")
    { BalanceCm = balance; RequiredCm = required; }
}
