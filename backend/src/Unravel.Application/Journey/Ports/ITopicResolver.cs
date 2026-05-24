using Unravel.Domain.Knowledge;

namespace Unravel.Application.Journey.Ports;

/// <summary>
/// Dado o texto de um Challenge (título + descrição) e o
/// <see cref="KnowledgeGraph"/> da trilha, decide quais
/// <see cref="Topic"/>s foram exercidos e com que peso. Soma dos pesos é 1
/// (atualização de mastery distribuída entre topics relevantes).
///
/// <para>Existe porque o modelo atual de <c>Challenge</c> não carrega
/// <c>ContentId</c> — sem isso, não dá pra saber exatamente qual tópico
/// foi testado. O resolver é o "best guess" determinístico: similaridade
/// lexical do enunciado com as keywords de cada tópico. Quando um futuro
/// PR adicionar <c>ContentId</c> ao Challenge, este resolver tem uma
/// rota direta (peso 1.0 no topic correspondente).</para>
/// </summary>
public interface ITopicResolver
{
    /// <summary>Retorna até <paramref name="topK"/> pares
    /// (TopicId, Weight) ordenados por peso decrescente. Weights somam ~1.
    /// Lista vazia se o grafo não tem nenhum tópico relevante.</summary>
    IReadOnlyList<TopicWeight> Resolve(string challengeText, KnowledgeGraph graph, int topK = 3);
}

public readonly record struct TopicWeight(int TopicId, double Weight);
