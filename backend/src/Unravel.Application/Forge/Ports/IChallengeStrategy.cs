using Unravel.Domain.Entities;
using Unravel.Domain.Forge;
using Unravel.Domain.Knowledge;

namespace Unravel.Application.Forge.Ports;

/// <summary>
/// Uma estratégia de geração de pergunta. Recebe um <see cref="Content"/>,
/// o <see cref="Topic"/> correspondente já analisado (keywords, dificuldade)
/// e o grafo completo da trilha (necessário pro DistractorPicker pegar
/// distratores plausíveis em tópicos vizinhos). Devolve até
/// <paramref name="maxDrafts"/> rascunhos.
///
/// <para>Determinístico: mesmo input → mesma saída. Sem clock interno.</para>
///
/// <para>Não roda QualityGate — quem chama (o <c>ChallengeForge</c>)
/// decide o que faz com cada draft (filtra, ranqueia, persiste).</para>
///
/// <para>Pronto pra LLM: uma <c>LlmChallengeStrategy</c> futura implementa
/// a mesma interface — basta registrar no DI e ela entra no pool.</para>
/// </summary>
public interface IChallengeStrategy
{
    ForgeStrategy Kind { get; }

    IReadOnlyList<GeneratedChallengeDraft> Generate(
        Content content,
        Topic topic,
        KnowledgeGraph graph,
        int maxDrafts);
}
