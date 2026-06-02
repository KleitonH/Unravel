using System.Collections.Concurrent;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Tokenizers.DotNet;
using Unravel.Application.Knowledge.Ports;

namespace Unravel.Infrastructure.Knowledge;

/// <summary>
/// Implementação de <see cref="IEmbedder"/> com MiniLM multilíngue
/// (<c>sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2</c>)
/// via ONNX Runtime. Modelo de 120 MB no disco, ~250 MB residente em RAM,
/// 5-15 ms por frase em CPU.
///
/// <para><b>Pipeline</b>:</para>
/// <list type="number">
///   <item>Texto → tokenização (Hugging Face tokenizer.json, BPE-like).</item>
///   <item>Tokens → tensores ONNX (input_ids, attention_mask).</item>
///   <item><c>InferenceSession.Run</c> → last_hidden_state [1, seq, 384].</item>
///   <item>Mean pooling sobre tokens válidos (mask-aware) → vetor 384.</item>
///   <item>L2-normalize → cosine similarity vira dot product.</item>
/// </list>
///
/// <para><b>Singleton</b>: o construtor carrega modelo + tokenizer (custoso,
/// ~1 segundo). Após isso, <see cref="Encode"/> é barata e thread-safe
/// (<c>InferenceSession</c> da MSFT é thread-safe).</para>
///
/// <para><b>Cache interno</b>: keywords se repetem muito (mesmo termo em
/// vários topics) — cache reduz milhares de inferências a uma só.</para>
/// </summary>
public sealed class MiniLmEmbedder : IEmbedder, IDisposable
{
    public const int ModelDimension = 384;
    private const int MaxSeqLen = 128;          // suficiente para keywords/frases curtas

    private readonly InferenceSession _session;
    private readonly Tokenizer        _tokenizer;
    private readonly ConcurrentDictionary<string, float[]> _cache = new();

    public int Dimension => ModelDimension;

    /// <summary><paramref name="modelPath"/> aponta para o arquivo .onnx;
    /// <paramref name="tokenizerJsonPath"/> para o tokenizer.json
    /// (formato Hugging Face Fast Tokenizer).</summary>
    public MiniLmEmbedder(string modelPath, string tokenizerJsonPath)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException(
                $"Modelo ONNX não encontrado em {modelPath}. " +
                "Execute scripts/download-minilm.sh ou desligue Embedding:Enabled.",
                modelPath);
        if (!File.Exists(tokenizerJsonPath))
            throw new FileNotFoundException(
                $"Tokenizer não encontrado em {tokenizerJsonPath}.",
                tokenizerJsonPath);

        _session   = new InferenceSession(modelPath);
        _tokenizer = new Tokenizer(vocabPath: tokenizerJsonPath);
    }

    public ReadOnlySpan<float> Encode(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new float[ModelDimension];   // vetor zero; cosine sim sempre 0

        return _cache.GetOrAdd(text, EncodeInternal);
    }

    private float[] EncodeInternal(string text)
    {
        // 1. tokeniza — Tokenizers.DotNet retorna uint[] de ids.
        var ids = _tokenizer.Encode(text);
        if (ids.Length == 0) return new float[ModelDimension];

        var seqLen = Math.Min(ids.Length, MaxSeqLen);
        var inputIds      = new long[1 * seqLen];
        var attentionMask = new long[1 * seqLen];
        var tokenTypeIds  = new long[1 * seqLen]; // zeros — single-segment input (BERT convention)
        for (var i = 0; i < seqLen; i++)
        {
            inputIds[i]      = ids[i];
            attentionMask[i] = 1;
            // tokenTypeIds[i] = 0 já é o default do array novo
        }

        var inputTensor    = new DenseTensor<long>(inputIds,      new[] { 1, seqLen });
        var maskTensor     = new DenseTensor<long>(attentionMask, new[] { 1, seqLen });
        var typeIdsTensor  = new DenseTensor<long>(tokenTypeIds,  new[] { 1, seqLen });

        // PR 33b — modelos BERT-like (incl. paraphrase-multilingual-MiniLM)
        // exigem 3º input token_type_ids. Vetor de zeros = single-segment;
        // variantes só-EN ignoram esse input mas aceitar não atrapalha.
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids",      inputTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", maskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", typeIdsTensor),
        };

        // 2. inferência — saída "last_hidden_state" [1, seq, 384].
        using var results = _session.Run(inputs);
        var hidden = results.First().AsTensor<float>();

        // 3. mean pool mascarado: média dos token vectors onde mask=1.
        var pooled = new float[ModelDimension];
        var validTokens = 0;
        for (var t = 0; t < seqLen; t++)
        {
            if (attentionMask[t] == 0) continue;
            validTokens++;
            for (var d = 0; d < ModelDimension; d++)
                pooled[d] += hidden[0, t, d];
        }
        if (validTokens > 0)
            for (var d = 0; d < ModelDimension; d++)
                pooled[d] /= validTokens;

        // 4. L2-normalize — torna cosine sim = dot product (mais barato).
        double norm = 0;
        for (var d = 0; d < ModelDimension; d++) norm += pooled[d] * pooled[d];
        norm = Math.Sqrt(norm);
        if (norm > 1e-9)
            for (var d = 0; d < ModelDimension; d++) pooled[d] = (float)(pooled[d] / norm);

        return pooled;
    }

    public void Dispose() => _session.Dispose();
}
