using Unravel.Domain.Knowledge;

namespace Unravel.Application.Forge.Ports;

/// <summary>
/// Escolhe alternativas erradas plausíveis para uma pergunta com resposta
/// correta <paramref name="correctTerm"/>. "Plausível" significa: parecido
/// o suficiente para confundir um usuário pouco preparado, distinto o
/// suficiente para um preparado distinguir.
///
/// <para>Implementação atual: escolhe termos de outros tópicos da trilha
/// (vizinhos no grafo) com peso similar. Uma futura implementação via
/// BERTimbau retornaria termos semanticamente próximos — mesma interface,
/// upgrade transparente.</para>
/// </summary>
public interface IDistractorPicker
{
    /// <summary>Devolve até <paramref name="count"/> distratores. Pode
    /// retornar menos se o grafo não oferecer candidatos suficientes;
    /// o <c>QualityGate</c> rejeita a pergunta nesse caso.</summary>
    IReadOnlyList<string> Pick(
        string correctTerm,
        Topic sourceTopic,
        KnowledgeGraph graph,
        int count);
}
