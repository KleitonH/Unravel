using System.Globalization;
using System.Text;

namespace Unravel.Infrastructure.Knowledge;

/// <summary>
/// Normalizações textuais determinísticas usadas tanto pelo extractor de
/// keywords quanto pelo difficulty scorer. Sem dependência externa para
/// manter testes rápidos e reprodutíveis.
/// </summary>
internal static class TextNormalizer
{
    /// <summary>Lowercase + remoção de diacríticos (NFD → strip Mn).
    /// "Programação" → "programacao".</summary>
    public static string FoldDiacritics(string s)
    {
        var nfd = s.Normalize(NormalizationForm.FormD);
        var sb  = new StringBuilder(nfd.Length);
        foreach (var ch in nfd)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    /// <summary>Stem leve PT-BR (subset do algoritmo de Orengo). Cobre os
    /// sufixos mais comuns; não pretende ser linguisticamente correto, só
    /// reduzir variantes morfológicas ao mesmo radical para o RAKE casar
    /// "programação" / "programar" / "programando".</summary>
    public static string LightStem(string word)
    {
        if (word.Length <= 4) return word;

        // Ordem importa: do sufixo mais longo para o mais curto.
        string[] suffixes =
        {
            "amentos","amento","imentos","imento",
            "uciones","ciones","açoes","acoes",
            "izacao","izaçao","izacoes","izaçoes",
            "ssemos","ariam","arao","aram","aria","asse","arem","ares",
            "issem","irao","iram","iria","isse","irem",
            "eremos","ariamos","iriamos",
            "izar","ismo","ista","istas","ismos",
            "amos","aram","ariam","aria","ando","ado","ada","ados","adas",
            "endo","ido","ida","idos","idas",
            "indo","oso","osa","osos","osas",
            "ica","ico","icas","icos","ivo","iva","ivos","ivas",
            "vel","veis",
            "ção","cao","ções","coes",
            "mente","ente","ente","ais","eis","ois",
            "ar","er","ir","or","es","em","am","ou",
        };

        foreach (var suf in suffixes)
            if (word.Length - suf.Length >= 4 && word.EndsWith(suf, StringComparison.Ordinal))
                return word[..^suf.Length];

        // plural genérico
        if (word.EndsWith("s") && word.Length > 4) return word[..^1];

        return word;
    }

    /// <summary>Normalização padrão para casamento de keyword: dobra
    /// diacrítico + stem leve. Pipeline canônica do extractor.</summary>
    public static string CanonicalKey(string token)
        => LightStem(FoldDiacritics(token));
}
