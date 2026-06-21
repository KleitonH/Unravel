namespace Unravel.Domain.Entities;

/// <summary>
/// PR 64 — Mecânicas sociais (Amigos/Parcerias). Status do vínculo entre
/// dois usuários. Um vínculo é armazenado UMA vez na direção
/// requester→addressee; a camada de serviço trata o par como simétrico
/// (A↔B) checando ambas as direções.
/// </summary>
public enum FriendshipStatus
{
    Pending  = 0, // pedido enviado, aguardando resposta do destinatário
    Accepted = 1, // amizade ativa
    Blocked  = 2, // bloqueado (reservado pra moderação futura)
}

/// <summary>
/// Vínculo de amizade entre dois usuários (grafo social do PR 64).
/// </summary>
public class Friendship
{
    public int  Id          { get; set; }
    public Guid RequesterId { get; set; } // quem enviou o pedido
    public Guid AddresseeId { get; set; } // quem recebe o pedido

    public FriendshipStatus Status      { get; set; } = FriendshipStatus.Pending;
    public DateTime         CreatedAt   { get; set; } = DateTime.UtcNow;
    public DateTime?        RespondedAt { get; set; }

    public User? Requester { get; set; }
    public User? Addressee { get; set; }
}
