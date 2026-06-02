using Unravel.Application.Forge.Eval;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Unravel.Infrastructure.Forge.Eval;

/// <summary>
/// Lê o arquivo YAML do gold set (PR 33). Schema descrito em
/// <c>backend/knowledge/gold-set/angular-fundamentos.yaml</c>.
///
/// <para>Items com placeholder TODO (campos vazios) são silenciosamente
/// filtrados — assim o eval funciona mesmo com o gold parcialmente
/// preenchido (útil enquanto o curador está completando).</para>
/// </summary>
public static class GoldSetReader
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static GoldSet ReadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Gold set não encontrado em '{path}'.", path);

        var raw = File.ReadAllText(path);
        var parsed = YamlDeserializer.Deserialize<RawGoldSet>(raw);

        if (string.IsNullOrWhiteSpace(parsed.Trail))
            throw new InvalidOperationException($"'{path}' sem campo 'trail'.");

        // Filtra placeholders incompletos — assim o curador pode rodar
        // o eval antes de terminar todos os 50 itens.
        var completed = parsed.Items?.Where(i => i.IsCompleted()).ToList()
                        ?? new List<GoldItem>();

        return new GoldSet(parsed.Trail, completed);
    }

    private sealed class RawGoldSet
    {
        public string Trail { get; set; } = string.Empty;
        public List<GoldItem>? Items { get; set; }
    }
}
