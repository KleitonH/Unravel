using Unravel.Infrastructure.Forge.Llm;

namespace Unravel.Tests.Forge;

public class LlmJsonParserTests
{
    [Fact]
    public void TryParse_WellFormedJson_ReturnsDraft()
    {
        const string raw = """
{
  "prompt": "Qual é o protocolo principal da web?",
  "options": ["HTTP", "FTP", "SMTP", "SSH"],
  "correctIndex": 0,
  "explanation": "HTTP é a base do tráfego web."
}
""";
        var draft = LlmJsonParser.TryParse(raw, sourceTopicId: 1, sourceContentId: 1, estimatedDifficulty: 0.3);
        Assert.NotNull(draft);
        Assert.Equal("Qual é o protocolo principal da web?", draft.Prompt);
        Assert.Equal(4, draft.Options.Count);
        Assert.Equal(0, draft.CorrectIndex);
        Assert.Equal("HTTP é a base do tráfego web.", draft.Explanation);
    }

    [Fact]
    public void TryParse_WrappedInMarkdownFence_StillExtracts()
    {
        const string raw = """
Aqui está a sua pergunta:

```json
{
  "prompt": "X?",
  "options": ["a", "b", "c"],
  "correctIndex": 1,
  "explanation": "..."
}
```

Espero ter ajudado!
""";
        var draft = LlmJsonParser.TryParse(raw, 1, 1, 0.5);
        Assert.NotNull(draft);
        Assert.Equal(1, draft.CorrectIndex);
    }

    [Fact]
    public void TryParse_MalformedJson_ReturnsNull()
    {
        Assert.Null(LlmJsonParser.TryParse("not json at all", 1, 1, 0.5));
        Assert.Null(LlmJsonParser.TryParse("{not closed", 1, 1, 0.5));
        Assert.Null(LlmJsonParser.TryParse("", 1, 1, 0.5));
        Assert.Null(LlmJsonParser.TryParse(null!, 1, 1, 0.5));
    }

    [Fact]
    public void TryParse_MissingOptions_ReturnsNull()
    {
        const string raw = """
{ "prompt": "P?", "correctIndex": 0 }
""";
        Assert.Null(LlmJsonParser.TryParse(raw, 1, 1, 0.5));
    }

    [Fact]
    public void TryParse_TooFewOptions_ReturnsNull()
    {
        const string raw = """
{ "prompt": "P?", "options": ["a", "b"], "correctIndex": 0 }
""";
        Assert.Null(LlmJsonParser.TryParse(raw, 1, 1, 0.5));
    }

    [Fact]
    public void TryParse_CorrectIndexOutOfRange_ReturnsNull()
    {
        const string raw = """
{ "prompt": "P?", "options": ["a", "b", "c"], "correctIndex": 99 }
""";
        Assert.Null(LlmJsonParser.TryParse(raw, 1, 1, 0.5));
    }

    [Fact]
    public void TryParse_EmptyOption_ReturnsNull()
    {
        const string raw = """
{ "prompt": "P?", "options": ["a", "", "c"], "correctIndex": 0 }
""";
        Assert.Null(LlmJsonParser.TryParse(raw, 1, 1, 0.5));
    }

    [Fact]
    public void TryParse_PreservesSourceIdsAndDifficulty()
    {
        const string raw = """
{ "prompt": "P?", "options": ["a", "b", "c"], "correctIndex": 0 }
""";
        var draft = LlmJsonParser.TryParse(raw, sourceTopicId: 42, sourceContentId: 99, estimatedDifficulty: 0.77);
        Assert.NotNull(draft);
        Assert.Equal(42, draft.SourceTopicId);
        Assert.Equal(99, draft.SourceContentId);
        Assert.Equal(0.77, draft.EstimatedDifficulty);
    }
}
