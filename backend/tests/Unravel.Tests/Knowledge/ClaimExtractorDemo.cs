using Unravel.Infrastructure.Knowledge;
using Xunit.Abstractions;

namespace Unravel.Tests.Knowledge;

/// <summary>
/// Não é teste de assertion — é um "demo" rodável que loga as claims
/// extraídas dos MDs Angular reais. Útil pra inspeção visual da
/// qualidade da extração (a calibrar via gold set no PR 33).
///
/// <para>Rodar com:
/// <code>dotnet test --filter "FullyQualifiedName~Demo" --logger "console;verbosity=detailed"</code>
/// </para>
/// </summary>
public class ClaimExtractorDemo
{
    private readonly ITestOutputHelper _out;
    public ClaimExtractorDemo(ITestOutputHelper @out) => _out = @out;

    [Fact]
    public void Dump_AllAngularContents_ClaimsByChunk()
    {
        var sut = new ClaimExtractor();
        var dir = FindKnowledgeDir();
        if (dir is null) { _out.WriteLine("Diretório knowledge não encontrado — skip."); return; }

        var files = Directory.EnumerateFiles(dir, "*.md").OrderBy(p => p).ToList();
        var grandTotal = 0;
        var chunkTotal = 0;

        foreach (var f in files)
        {
            var name = Path.GetFileNameWithoutExtension(f);
            var raw  = File.ReadAllText(f);
            var body = raw[(raw.IndexOf("---\n", 4, StringComparison.Ordinal) + 5)..];
            var claims = sut.Extract(body);
            grandTotal += claims.Count;
            chunkTotal += claims.Select(c => c.ChunkIndex).Distinct().Count();

            _out.WriteLine($"\n===== {name} — {claims.Count} claims em {claims.Select(c => c.ChunkIndex).Distinct().Count()} chunks =====");
            foreach (var grp in claims.GroupBy(c => c.ChunkIndex).OrderBy(g => g.Key))
            {
                _out.WriteLine($"  [chunk {grp.Key}]");
                foreach (var c in grp)
                    _out.WriteLine($"    {c.Score:F2}  {c.ClaimText}");
            }
        }

        _out.WriteLine($"\n\nTOTAL: {grandTotal} claims em {chunkTotal} chunks distintos, de {files.Count} arquivos.");
        Assert.True(grandTotal >= 80, $"Esperava ≥80 claims totais dos 12 MDs, obteve {grandTotal}");
    }

    private static string? FindKnowledgeDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
        {
            var c = Path.Combine(dir.FullName, "knowledge", "angular-fundamentos");
            if (Directory.Exists(c)) return c;
        }
        return null;
    }
}
