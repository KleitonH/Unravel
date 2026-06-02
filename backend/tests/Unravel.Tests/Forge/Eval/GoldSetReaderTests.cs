using Unravel.Application.Forge.Eval;
using Unravel.Infrastructure.Forge.Eval;

namespace Unravel.Tests.Forge.Eval;

/// <summary>
/// Cobre o <see cref="GoldSetReader"/>: parse de YAML, filtro
/// silencioso de placeholders TODO, erro pra arquivo malformado.
/// </summary>
public class GoldSetReaderTests : IDisposable
{
    private readonly string _tempDir;

    public GoldSetReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"unravel-gold-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    private string WriteYaml(string content)
    {
        var path = Path.Combine(_tempDir, "gold.yaml");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Read_HappyPath_ParsesAllItems()
    {
        var path = WriteYaml("""
        trail: angular-fundamentos
        items:
          - topicSlug: angular-componentes
            sourceClaim: "X é Y."
            prompt: "Pergunta?"
            correctAnswer: "Resposta correta"
            distractors: ["D1", "D2", "D3"]
            explanation: "Por isso"
            difficultyHint: 0.4
        """);
        var gs = GoldSetReader.ReadFromFile(path);

        Assert.Equal("angular-fundamentos", gs.Trail);
        Assert.Single(gs.Items);
        var item = gs.Items[0];
        Assert.Equal("angular-componentes", item.TopicSlug);
        Assert.Equal("X é Y.", item.SourceClaim);
        Assert.Equal(3, item.Distractors.Count);
        Assert.Equal(0.4, item.DifficultyHint);
    }

    [Fact]
    public void Read_FiltersPlaceholderTodos()
    {
        // 1 completo + 2 TODOs (campos vazios) → só 1 sobrevive
        var path = WriteYaml("""
        trail: angular-fundamentos
        items:
          - topicSlug: angular-componentes
            sourceClaim: "X é Y."
            prompt: "P?"
            correctAnswer: "C"
            distractors: ["D1", "D2", "D3"]
            explanation: "E"
          - topicSlug: angular-componentes
            sourceClaim: ""
            prompt: ""
            correctAnswer: ""
            distractors: []
            explanation: ""
          - topicSlug: angular-templates
            sourceClaim: ""
            prompt: ""
            correctAnswer: ""
            distractors: []
            explanation: ""
        """);
        var gs = GoldSetReader.ReadFromFile(path);
        Assert.Single(gs.Items);
    }

    [Fact]
    public void Read_FewerThan3Distractors_TreatedAsIncomplete()
    {
        var path = WriteYaml("""
        trail: x
        items:
          - topicSlug: foo
            sourceClaim: "S"
            prompt: "P?"
            correctAnswer: "C"
            distractors: ["D1", "D2"]
            explanation: "E"
        """);
        var gs = GoldSetReader.ReadFromFile(path);
        Assert.Empty(gs.Items); // 2 distratores ≠ 3 → incompleto
    }

    [Fact]
    public void Read_DistractorWithBlankString_TreatedAsIncomplete()
    {
        var path = WriteYaml("""
        trail: x
        items:
          - topicSlug: foo
            sourceClaim: "S"
            prompt: "P?"
            correctAnswer: "C"
            distractors: ["D1", "", "D3"]
            explanation: "E"
        """);
        var gs = GoldSetReader.ReadFromFile(path);
        Assert.Empty(gs.Items);
    }

    [Fact]
    public void Read_NonexistentFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            GoldSetReader.ReadFromFile(Path.Combine(_tempDir, "nope.yaml")));
    }

    [Fact]
    public void Read_MissingTrailField_Throws()
    {
        var path = WriteYaml("items: []");
        Assert.Throws<InvalidOperationException>(() => GoldSetReader.ReadFromFile(path));
    }

    [Fact]
    public void Read_RealAngularGoldFile_LoadsAtLeast10Items()
    {
        // Soft skip se arquivo não está acessível do dir de teste
        var path = FindAngularGold();
        if (path is null) return;

        var gs = GoldSetReader.ReadFromFile(path);
        Assert.Equal("angular-fundamentos", gs.Trail);
        // Esperamos pelo menos 10 itens completos (1 por tópico que eu escrevi).
        // À medida que o curador preenche os TODOs, esse número sobe pra 50.
        Assert.True(gs.Items.Count >= 10,
            $"Esperava ≥10 itens completos do gold real, obteve {gs.Items.Count}");
    }

    [Fact]
    public void GoldItem_IsCompleted_AllFieldsPresent_True()
    {
        var item = new GoldItem
        {
            TopicSlug = "x", SourceClaim = "s", Prompt = "p?", CorrectAnswer = "c",
            Distractors = new() { "a", "b", "c" }, Explanation = "e"
        };
        Assert.True(item.IsCompleted());
    }

    [Fact]
    public void GoldItem_IsCompleted_MissingField_False()
    {
        var baseItem = new GoldItem
        {
            TopicSlug = "x", SourceClaim = "s", Prompt = "p?", CorrectAnswer = "c",
            Distractors = new() { "a", "b", "c" }, Explanation = "e"
        };
        Assert.True(baseItem.IsCompleted());

        var noClaim = new GoldItem
        {
            TopicSlug = "x", SourceClaim = "", Prompt = "p?", CorrectAnswer = "c",
            Distractors = new() { "a", "b", "c" }, Explanation = "e"
        };
        Assert.False(noClaim.IsCompleted());
    }

    private static string? FindAngularGold()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
        {
            var c = Path.Combine(dir.FullName, "knowledge", "gold-set", "angular-fundamentos.yaml");
            if (File.Exists(c)) return c;
        }
        return null;
    }
}
