namespace Unravel.Domain.Forge;

/// <summary>
/// Pergunta gerada (pelo Forge ou por LLM no futuro) e persistida para
/// reúso. Existe em paralelo ao <c>Challenge</c> pré-cadastrado por
/// moderadores: o use case do pool híbrido mescla os dois.
///
/// <para><b>Por que persistir</b>: geração é barata mas não trivial
/// (regex + lookup de distratores), e <i>servir a mesma pergunta repetidas
/// vezes</i> é o que permite calcular <c>CorrectRate</c> — métrica chave
/// pra detectar pergunta ruim e desativar.</para>
///
/// <para><b>Por que NÃO virar <c>Challenge</c></b>: <c>Challenge</c> é
/// modelo de moderador, com edição manual e fluxos de aprovação implícitos.
/// Misturar gerado e curado na mesma tabela bagunçaria queries de
/// administração. Tabelas separadas, união no use case.</para>
/// </summary>
public sealed class GeneratedChallenge
{
    public int           Id              { get; set; }
    public int           ContentId       { get; set; }
    public int           TopicId         { get; set; }
    public int           TrailId         { get; set; }
    public ForgeStrategy Strategy        { get; set; }

    public string        Prompt          { get; set; } = string.Empty;
    /// <summary>JSON serializado: <c>{"options":[...], "correctIndex":N,
    /// "explanation":"..."}</c>. Mantemos como JSON pra não criar tabela
    /// filha por uma estrutura que nunca é consultada parcialmente.</summary>
    public string        BodyJson        { get; set; } = string.Empty;

    public double        EstimatedDifficulty { get; set; }

    /// <summary>Quantas vezes esta pergunta foi servida a algum usuário.
    /// Incrementado pelo use case ao montar o pool.</summary>
    public int           ServedCount     { get; set; }

    /// <summary>Razão acumulada de acerto (média móvel simples sobre
    /// <c>ServedCount</c>). Atualizada quando o ChallengeService aprende
    /// que aquela pergunta foi respondida. Próximo PR conecta esse loop;
    /// por enquanto o campo existe e é zero.</summary>
    public double        CorrectRate     { get; set; }

    /// <summary>Flag pra desativação automática: pergunta que todo mundo
    /// erra (ou todo mundo acerta) com <c>ServedCount</c> alto é sinal
    /// de pergunta ambígua ou trivial — não vale a pena continuar
    /// servindo. Atualizado por job ou na demanda.</summary>
    public bool          IsActive        { get; set; } = true;

    public DateTime      CreatedAt       { get; set; } = DateTime.UtcNow;
}
