namespace Unravel.Application.Knowledge.Ports;

/// <summary>
/// Encode determinístico de texto → vetor (embedding). Mesmo input
/// produz exatamente o mesmo vetor — vital para cache e reprodução
/// de testes.
///
/// <para>Implementação canônica (PR 18) carrega um modelo MiniLM multilíngue
/// via ONNX Runtime. A interface é mínima de propósito; consumidores
/// precisam apenas de similaridade por cosine, que é dot product entre
/// vetores L2-normalizados.</para>
///
/// <para>O port vive na Application para que <c>SemanticDistractorPicker</c>
/// (Infrastructure/Forge) possa injetá-lo sem depender da implementação
/// concreta — o teste pode trocar por um stub.</para>
/// </summary>
public interface IEmbedder
{
    /// <summary>Dimensionalidade dos vetores retornados (384 para MiniLM-L12).
    /// Constante — não muda em runtime.</summary>
    int Dimension { get; }

    /// <summary>Codifica o texto em um vetor L2-normalizado de
    /// <see cref="Dimension"/> floats. Implementações podem cachear
    /// internamente por texto, mas isso é detalhe — chamadores devem
    /// poder chamar à vontade.</summary>
    ReadOnlySpan<float> Encode(string text);

    /// <summary>Cosine similarity entre dois vetores. Para vetores
    /// L2-normalizados (caso desta interface), reduz-se a dot product.
    /// Retorna em [-1, 1]; quanto maior, mais similar.</summary>
    static double CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length) throw new ArgumentException("Dimensionalidade incompatível.");
        double dot = 0;
        for (var i = 0; i < a.Length; i++) dot += a[i] * b[i];
        return dot;
    }
}
