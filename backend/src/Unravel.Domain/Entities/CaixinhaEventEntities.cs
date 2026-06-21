namespace Unravel.Domain.Entities;

/// <summary>
/// PR 65c — Eventos entre caixinhas (competições temporárias). Conceito:
/// arquivos_unravel "Ideia 6", seção 5. Caixinhas competem acumulando pontos
/// coletivos dentro de um período (3–7 dias). O status (Upcoming/Active/
/// Finished) é derivado das datas, não armazenado.
/// </summary>
public class CaixinhaEvent
{
    public int      Id              { get; set; }
    public string   Name            { get; set; } = string.Empty;
    public string?  Theme           { get; set; } // ex: "Semana de Backend"
    public DateTime StartsAt        { get; set; }
    public DateTime EndsAt          { get; set; }
    public Guid     CreatedByUserId { get; set; }
    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;

    public ICollection<CaixinhaEventScore> Scores { get; set; } = new List<CaixinhaEventScore>();
}

/// <summary>
/// Participação de uma caixinha num evento. Pontos do evento = pontos coletivos
/// atuais − <see cref="BaselinePoints"/> (capturado na entrada). Quando o evento
/// encerra, <see cref="FinalPoints"/> congela o resultado.
/// </summary>
public class CaixinhaEventScore
{
    public int       Id             { get; set; }
    public int       EventId        { get; set; }
    public int       CaixinhaId     { get; set; }
    public int       BaselinePoints { get; set; }
    public int?      FinalPoints    { get; set; }
    public DateTime  JoinedAt       { get; set; } = DateTime.UtcNow;

    public CaixinhaEvent? Event    { get; set; }
    public Caixinha?      Caixinha { get; set; }
}
