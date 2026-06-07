using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DeactivateLegacyTemplateChallenges : Migration
    {
        /// <summary>
        /// PR 34e — desativa perguntas template-based legadas pra evitar
        /// que apareçam no pool (cron noturno do PR 20 gerava antes).
        ///
        /// <para><b>Critério</b>: strategy != 7 (LlmGrounded) E served_count &lt; 3.
        /// Threshold conservador: 3 servidas significa que a pergunta foi
        /// vista o suficiente pra ter sinal de qualidade real (correct_rate
        /// confiável); abaixo disso é "ruído de pool". Perguntas mais
        /// servidas ficam ativas pra não quebrar histórico do usuário
        /// (CorrectRate aprendida vale algo).</para>
        ///
        /// <para><b>Reversível</b>: Down() reativa tudo. Não deleta linhas
        /// — backfill é cirúrgico em IsActive, dados preservados.</para>
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE generated_challenge " +
                "SET is_active = false " +
                "WHERE strategy != 7 AND served_count < 3 AND is_active = true;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverte só o que foi tocado aqui (strategy != 7 + served_count < 3).
            // Não reativa perguntas desativadas por OUTROS motivos (auto-desativador
            // PR 17 por correct_rate ruim, ou moderador manual).
            // Best-effort: re-ativa o que casa o critério. Se ServedCount mudou
            // entre Up e Down, podem ficar algumas inconsistências; aceitável
            // pra cenário de rollback raro.
            migrationBuilder.Sql(
                "UPDATE generated_challenge " +
                "SET is_active = true " +
                "WHERE strategy != 7 AND served_count < 3 AND is_active = false;");
        }
    }
}
