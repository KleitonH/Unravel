namespace Unravel.Infrastructure.Knowledge;

/// <summary>
/// Stopwords PT-BR + uma seleção pequena de EN técnico (the/of/and/with…)
/// porque muito conteúdo de TI mistura inglês ("the React component is…").
/// Lista enxuta de propósito: stopwords agressivas demais comem termos
/// técnicos úteis ("dado", "estado", "número"); usamos um corte conservador
/// e deixamos o ranking RAKE filtrar o ruído restante.
/// </summary>
internal static class StopwordsPt
{
    public static readonly HashSet<string> Set = new(StringComparer.OrdinalIgnoreCase)
    {
        // PT — artigos, pronomes, preposições, conjunções, verbos auxiliares comuns
        "a","o","as","os","um","uma","uns","umas",
        "de","do","da","dos","das","em","no","na","nos","nas",
        "por","pelo","pela","pelos","pelas","para","pra","com","sem","sob","sobre","entre","ate","até",
        "e","ou","mas","porque","pois","como","se","quando","onde","que","qual","quais","quem","cujo","cuja",
        "eu","tu","ele","ela","nos","nós","vos","vós","eles","elas","me","te","lhe","nos","vos","lhes","se","si",
        "meu","minha","seu","sua","nosso","nossa","seus","suas","teu","tua","meus","minhas",
        "este","esta","isto","esse","essa","isso","aquele","aquela","aquilo","tal","tais",
        "ser","estar","ter","haver","fazer","ir","vir","poder","dever","saber","ver","dar","dizer","falar",
        "é","são","foi","foram","era","eram","sera","será","sendo","sido","tem","têm","tinha","tinham","tenha",
        "estou","está","estão","esteve","estavam","esteja",
        "muito","muita","muitos","muitas","pouco","pouca","mais","menos","também","tambem","já","ja","ainda",
        "todo","toda","todos","todas","cada","outro","outra","outros","outras","mesmo","mesma","mesmos","mesmas",
        "aqui","ali","lá","la","cá","ca","então","entao","assim","apenas","só","so","bem","bom","boa",
        "não","nao","sim","talvez","nunca","sempre","jamais",
        "etc","ex","ie","ou seja",

        // EN — só o essencial que aparece colado em conteúdo técnico
        "the","a","an","of","and","or","but","is","are","was","were","be","been","being",
        "to","in","on","at","by","for","with","without","from","into","over","under",
        "this","that","these","those","it","its","as","if","then","else","so","such",
    };

    /// <summary>Pontuação que delimita "frases candidatas" no RAKE.</summary>
    public static readonly char[] PhraseDelimiters = { '.', ',', ';', ':', '?', '!', '(', ')', '[', ']', '\n', '\r', '\t', '–', '—', '/', '\\', '|' };
}
