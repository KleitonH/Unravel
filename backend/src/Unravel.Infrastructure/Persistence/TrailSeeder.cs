using Microsoft.EntityFrameworkCore;
using Unravel.Domain.Entities;

namespace Unravel.Infrastructure.Persistence;

public static class TrailSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        if (await db.Trails.AnyAsync()) return;

        var trails = new List<Trail>
        {
            new()
            {
                Name        = "Desenvolvimento Web",
                Description = "HTML, CSS, JavaScript, Angular e APIs REST do zero ao avançado.",
                Icon        = "🖥",
                AccentColor = "#7038f2",
                Level       = DifficultyLevel.Beginner,
                Contents    = new List<Content>
                {
                    new() { Title = "Introdução ao HTML",      Body = "Aprenda a estrutura básica de uma página web.",  Order = 1, Level = DifficultyLevel.Beginner,     Type = ContentType.Article  },
                    new() { Title = "CSS Fundamentos",         Body = "Seletores, box model e flexbox.",                Order = 2, Level = DifficultyLevel.Beginner,     Type = ContentType.Article  },
                    new() { Title = "JavaScript Essencial",    Body = "Variáveis, funções, eventos e DOM.",             Order = 3, Level = DifficultyLevel.Beginner,     Type = ContentType.Article  },
                    new() { Title = "CSS Grid na prática",     Body = "Layouts complexos com CSS Grid.",               Order = 4, Level = DifficultyLevel.Intermediate, Type = ContentType.Exercise },
                    new() { Title = "TypeScript para Angular", Body = "Tipos, interfaces e generics.",                 Order = 5, Level = DifficultyLevel.Intermediate, Type = ContentType.Article  },
                    new() { Title = "Angular Components",      Body = "Criando e reutilizando componentes.",           Order = 6, Level = DifficultyLevel.Intermediate, Type = ContentType.Exercise },
                    new() { Title = "Consumindo APIs REST",    Body = "HttpClient, observables e tratamento de erro.", Order = 7, Level = DifficultyLevel.Advanced,     Type = ContentType.Exercise },
                    new() { Title = "Deploy de aplicações",    Body = "Build de produção e publicação.",               Order = 8, Level = DifficultyLevel.Advanced,     Type = ContentType.Article  },
                }
            },
            new()
            {
                Name        = "Banco de Dados",
                Description = "SQL, modelagem relacional, PostgreSQL e ORM com Entity Framework.",
                Icon        = "🗄",
                AccentColor = "#38db8c",
                Level       = DifficultyLevel.Beginner,
                Contents    = new List<Content>
                {
                    new() { Title = "Modelagem Relacional",      Body = "Entidades, atributos e relacionamentos.",    Order = 1, Level = DifficultyLevel.Beginner,     Type = ContentType.Article  },
                    new() { Title = "SQL Básico",                Body = "SELECT, INSERT, UPDATE, DELETE.",            Order = 2, Level = DifficultyLevel.Beginner,     Type = ContentType.Exercise },
                    new() { Title = "Joins e Subconsultas",      Body = "INNER, LEFT, RIGHT JOIN na prática.",        Order = 3, Level = DifficultyLevel.Intermediate, Type = ContentType.Exercise },
                    new() { Title = "Índices e Performance",     Body = "Como e quando indexar colunas.",            Order = 4, Level = DifficultyLevel.Intermediate, Type = ContentType.Article  },
                    new() { Title = "Entity Framework Core",     Body = "Code First, migrations e LINQ.",            Order = 5, Level = DifficultyLevel.Intermediate, Type = ContentType.Exercise },
                    new() { Title = "Transações e Concorrência", Body = "ACID, locks e controle de concorrência.",   Order = 6, Level = DifficultyLevel.Advanced,     Type = ContentType.Article  },
                }
            },
            new()
            {
                Name        = "Segurança da Informação",
                Description = "Fundamentos de cibersegurança, criptografia, JWT e boas práticas.",
                Icon        = "🔐",
                AccentColor = "#ed54f2",
                Level       = DifficultyLevel.Intermediate,
                Contents    = new List<Content>
                {
                    new() { Title = "Fundamentos de Segurança", Body = "CIA Triad, vetores de ataque e defesa.",     Order = 1, Level = DifficultyLevel.Beginner,     Type = ContentType.Article  },
                    new() { Title = "Criptografia Básica",      Body = "Simétrica, assimétrica e hashing.",         Order = 2, Level = DifficultyLevel.Intermediate, Type = ContentType.Article  },
                    new() { Title = "Autenticação com JWT",     Body = "Tokens, claims, assinatura e expiração.",   Order = 3, Level = DifficultyLevel.Intermediate, Type = ContentType.Exercise },
                    new() { Title = "OWASP Top 10",             Body = "Principais vulnerabilidades e mitigações.", Order = 4, Level = DifficultyLevel.Intermediate, Type = ContentType.Article  },
                    new() { Title = "Pentest Introdutório",     Body = "Reconhecimento, exploração e reporte.",     Order = 5, Level = DifficultyLevel.Advanced,     Type = ContentType.Exercise },
                }
            },
            new()
            {
                Name        = "Inteligência Artificial",
                Description = "Machine Learning, redes neurais, NLP e aplicações práticas com Python.",
                Icon        = "🤖",
                AccentColor = "#ffc700",
                Level       = DifficultyLevel.Advanced,
                Contents    = new List<Content>
                {
                    new() { Title = "Introdução ao ML",  Body = "Tipos de aprendizado e pipeline básico.",   Order = 1, Level = DifficultyLevel.Beginner,     Type = ContentType.Article  },
                    new() { Title = "Regressão Linear",  Body = "Conceito, custo e gradiente descendente.", Order = 2, Level = DifficultyLevel.Intermediate, Type = ContentType.Exercise },
                    new() { Title = "Classificação",     Body = "Logística, kNN, árvores de decisão.",      Order = 3, Level = DifficultyLevel.Intermediate, Type = ContentType.Exercise },
                    new() { Title = "Redes Neurais",     Body = "Perceptron, backpropagation e camadas.",   Order = 4, Level = DifficultyLevel.Advanced,     Type = ContentType.Article  },
                    new() { Title = "NLP Fundamentos",   Body = "Tokenização, embeddings e transformers.",  Order = 5, Level = DifficultyLevel.Advanced,     Type = ContentType.Article  },
                    new() { Title = "Projeto Prático",   Body = "Classificador de texto end-to-end.",       Order = 6, Level = DifficultyLevel.Advanced,     Type = ContentType.Exercise },
                }
            },
        };

        db.Trails.AddRange(trails);
        await db.SaveChangesAsync();
    }
}
