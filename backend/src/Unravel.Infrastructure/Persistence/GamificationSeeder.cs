using Microsoft.EntityFrameworkCore;
using Unravel.Domain.Entities;

namespace Unravel.Infrastructure.Persistence;

public static class GamificationSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        await SeedBadgesAsync(db);
        await SeedChallengesAsync(db);
        await SeedCosmeticsAsync(db);
    }

    // PR 63 — catálogo da Loja (Toca do NAVI). AssetSlug = valor consumido
    // pelo componente NAVI no slot (Type). Itens em stars têm StarPrice; o
    // item de evento fica com LockedReason (não comprável).
    private static async Task SeedCosmeticsAsync(ApplicationDbContext db)
    {
        if (await db.NaviCosmetic.AnyAsync()) return;

        NaviCosmetic Coin(string slug, string name, CosmeticType type, CosmeticRarity rarity, int coins, string desc) =>
            new() { AssetSlug = slug, Name = name, Type = type, Rarity = rarity, CoinPrice = coins, Description = desc };
        NaviCosmetic Star(string slug, string name, CosmeticType type, CosmeticRarity rarity, int stars, string desc) =>
            new() { AssetSlug = slug, Name = name, Type = type, Rarity = rarity, StarPrice = stars, Description = desc };

        db.NaviCosmetic.AddRange(
            // ── Chapéus (Hat) ──
            Coin("bone",    "Boné Dev",        CosmeticType.Hat, CosmeticRarity.Common,    150, "Boné de programador com </> estampado. Pra quem vive no terminal."),
            Coin("cartola", "Cartola Premium", CosmeticType.Hat, CosmeticRarity.Rare,      320, "Elegância felina. Faixa roxa Unravel no aro."),
            Coin("headset", "Headset Gamer",   CosmeticType.Hat, CosmeticRarity.Epic,      600, "Headset RGB com microfone. GG, NAVI."),
            Star("coroa",   "Coroa Real",      CosmeticType.Hat, CosmeticRarity.Legendary, 12,  "Realeza do código. Só pra lendas com Estrelas."),
            new() { AssetSlug = "antenas", Name = "Antenas Alien", Type = CosmeticType.Hat, Rarity = CosmeticRarity.Exclusive, IsExclusive = true, LockedReason = "Recompensa do evento Caixinha de Gatos", Description = "Item de evento. Obtido na Caixinha de Gatos." },

            // ── Acessórios (Accessory) ──
            Coin("gravata", "Gravata-Borboleta", CosmeticType.Accessory, CosmeticRarity.Common, 120, "Charme instantâneo pra apresentações."),
            Coin("jaleco",  "Jaleco Branco",     CosmeticType.Accessory, CosmeticRarity.Rare,   280, "Modo cientista de dados ativado."),
            Coin("mochila", "Mochila Tech",      CosmeticType.Accessory, CosmeticRarity.Rare,   350, "Sempre pronto pra próxima trilha."),
            Star("capa",    "Capa de Herói",     CosmeticType.Accessory, CosmeticRarity.Epic,   5,   "Salva builds em produção numa sexta 18h."),

            // ── Pelagens (Fur) ──
            Coin("cinza",   "Pelagem Cinza",   CosmeticType.Fur, CosmeticRarity.Common,    100, "Visual sóbrio e versátil."),
            Coin("laranja", "Pelagem Laranja", CosmeticType.Fur, CosmeticRarity.Rare,      240, "Energia de gato de fliperama."),
            Coin("branco",  "Pelagem Branca",  CosmeticType.Fur, CosmeticRarity.Epic,      500, "Pelagem rara e imponente."),
            Star("dourado", "Pelagem Dourada", CosmeticType.Fur, CosmeticRarity.Legendary, 20,  "O pelo dos campeões. Brilha de verdade."),

            // ── Expressões (Expression; AssetSlug = mood do NAVI) ──
            Coin("happy",   "Sorriso Feliz",  CosmeticType.Expression, CosmeticRarity.Common, 90,  "NAVI sempre de bom humor."),
            Coin("excited", "Modo Empolgado", CosmeticType.Expression, CosmeticRarity.Rare,   220, "Olhos brilhando de empolgação.")
        );

        await db.SaveChangesAsync();
    }

    private static async Task SeedBadgesAsync(ApplicationDbContext db)
    {
        if (await db.Badge.AnyAsync()) return;

        db.Badge.AddRange(
            // Streak (Ideia 2)
            new Badge { Name = "🔥 Ofensiva de 7 dias",  Description = "7 dias consecutivos de estudo",    Icon = "🔥", Category = BadgeCategory.Streak,      IsExclusive = false },
            new Badge { Name = "⚡ Ofensiva de 14 dias", Description = "14 dias consecutivos de estudo",   Icon = "⚡", Category = BadgeCategory.Streak,      IsExclusive = false },
            new Badge { Name = "🌙 Ofensiva de 30 dias", Description = "30 dias consecutivos de estudo",   Icon = "🌙", Category = BadgeCategory.Streak,      IsExclusive = true  },
            new Badge { Name = "🏆 Ofensiva de 60 dias", Description = "60 dias consecutivos de estudo",   Icon = "🏆", Category = BadgeCategory.Streak,      IsExclusive = true  },
            new Badge { Name = "💎 Pelagem de Platina",  Description = "100 dias consecutivos — lendário", Icon = "💎", Category = BadgeCategory.Streak,      IsExclusive = true  },
            // Conhecimento (Ideia 5)
            new Badge { Name = "CSSiamês Profissional",   Description = "Alta taxa de acerto em Dev Web",        Icon = "🥇", Category = BadgeCategory.Knowledge, IsExclusive = false },
            new Badge { Name = "Mestre dos Gatilhos SQL", Description = "Alta taxa de acerto em Banco de Dados",  Icon = "🗄️", Category = BadgeCategory.Knowledge, IsExclusive = false },
            new Badge { Name = "Felino Full-Stack",       Description = "Conclusão de 2+ trilhas",               Icon = "🐱", Category = BadgeCategory.Knowledge, IsExclusive = false },
            // Social
            new Badge { Name = "Ninja do Novelo",         Description = "Primeiro novelo de lã completado",      Icon = "🧶", Category = BadgeCategory.Social,    IsExclusive = true  },
            // Pré-registro
            new Badge { Name = "🐾 Fundador",             Description = "Membro fundador da plataforma",         Icon = "🐾", Category = BadgeCategory.PreRegister, IsExclusive = true }
        );

        await db.SaveChangesAsync();
    }

    private static async Task SeedChallengesAsync(ApplicationDbContext db)
    {
        if (await db.Challenge.AnyAsync()) return;

        var trails = await db.Trail.ToListAsync();
        if (!trails.Any()) return;

        var devWeb = trails.FirstOrDefault(t => t.Name.Contains("Web"));
        var banco  = trails.FirstOrDefault(t => t.Name.Contains("Banco"));
        var sec    = trails.FirstOrDefault(t => t.Name.Contains("Seguran"));
        var ai     = trails.FirstOrDefault(t => t.Name.Contains("Intelig"));

        var challenges = new List<Challenge>();

        if (devWeb is not null)
        {
            challenges.Add(new Challenge
            {
                TrailId = devWeb.Id, Level = DifficultyLevel.Beginner,
                Title = "CSS Grid: Layout Avançado", Description = "Teste seus conhecimentos sobre CSS Grid",
                XpReward = 150, CoinsReward = 10, IsDailyChallenge = true,
                Options =
                [
                    new() { Text = "grid-template-columns", IsCorrect = true,  Order = 0 },
                    new() { Text = "grid-columns",          IsCorrect = false, Order = 1 },
                    new() { Text = "column-template",       IsCorrect = false, Order = 2 },
                    new() { Text = "grid-column-count",     IsCorrect = false, Order = 3 },
                ]
            });
            challenges.Add(new Challenge
            {
                TrailId = devWeb.Id, Level = DifficultyLevel.Intermediate,
                Title = "JavaScript Assíncrono", Description = "Promises, async/await e callbacks",
                XpReward = 200, CoinsReward = 15,
                Options =
                [
                    new() { Text = "async/await é açúcar sintático sobre Promises",  IsCorrect = true,  Order = 0 },
                    new() { Text = "async/await substitui completamente Promises",    IsCorrect = false, Order = 1 },
                    new() { Text = "callbacks são mais modernos que Promises",        IsCorrect = false, Order = 2 },
                    new() { Text = "Promises não podem ser encadeadas",               IsCorrect = false, Order = 3 },
                ]
            });
        }

        if (banco is not null)
        {
            challenges.Add(new Challenge
            {
                TrailId = banco.Id, Level = DifficultyLevel.Beginner,
                Title = "SQL: JOINs na prática", Description = "Dominando relacionamentos entre tabelas",
                XpReward = 150, CoinsReward = 10,
                Options =
                [
                    new() { Text = "INNER JOIN retorna apenas registros com correspondência em ambas as tabelas", IsCorrect = true,  Order = 0 },
                    new() { Text = "LEFT JOIN retorna apenas registros da tabela direita",                        IsCorrect = false, Order = 1 },
                    new() { Text = "FULL JOIN não existe no SQL padrão",                                          IsCorrect = false, Order = 2 },
                    new() { Text = "RIGHT JOIN e LEFT JOIN são idênticos",                                        IsCorrect = false, Order = 3 },
                ]
            });
        }

        if (sec is not null)
        {
            challenges.Add(new Challenge
            {
                TrailId = sec.Id, Level = DifficultyLevel.Intermediate,
                Title = "JWT na prática", Description = "Autenticação com JSON Web Tokens",
                XpReward = 180, CoinsReward = 12,
                Options =
                [
                    new() { Text = "O payload do JWT é codificado em Base64 mas não é criptografado por padrão", IsCorrect = true,  Order = 0 },
                    new() { Text = "O payload do JWT é sempre criptografado",                                     IsCorrect = false, Order = 1 },
                    new() { Text = "JWT não suporta claims customizados",                                          IsCorrect = false, Order = 2 },
                    new() { Text = "O header do JWT contém as permissões do usuário",                             IsCorrect = false, Order = 3 },
                ]
            });
        }

        if (ai is not null)
        {
            challenges.Add(new Challenge
            {
                TrailId = ai.Id, Level = DifficultyLevel.Advanced,
                Title = "Machine Learning: Conceitos", Description = "Fundamentos de ML e tipos de aprendizado",
                XpReward = 220, CoinsReward = 18,
                Options =
                [
                    new() { Text = "Aprendizado supervisionado usa dados rotulados para treinar o modelo", IsCorrect = true,  Order = 0 },
                    new() { Text = "Aprendizado não-supervisionado requer rótulos em todos os dados",      IsCorrect = false, Order = 1 },
                    new() { Text = "Regressão é usada apenas para classificação binária",                  IsCorrect = false, Order = 2 },
                    new() { Text = "Redes neurais são sempre superiores a outros algoritmos",              IsCorrect = false, Order = 3 },
                ]
            });
        }

        db.Challenge.AddRange(challenges);
        await db.SaveChangesAsync();
    }
}
