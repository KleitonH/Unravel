using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrailProgression : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "challenges_completed",
                table: "user_content",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "user_content",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "challenges_required",
                table: "content",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            // PR 40 — data migration. Dois passos:
            //
            // (1) UserContents já marcados como is_completed=true (legacy)
            //     ficam coerentes: status=3 (Completed), challenges_completed
            //     = challenges_required do content. Preserva histórico.
            migrationBuilder.Sql(@"
                UPDATE user_content uc
                SET status = 3,
                    challenges_completed = c.challenges_required
                FROM content c
                WHERE c.id = uc.content_id
                  AND uc.is_completed = true;
            ");

            // (2) Cada UserTrail ativo precisa ter acesso ao 1º content
            //     da trilha. Insere UserContent (Available) só pra quem
            //     ainda não tem nenhum UserContent naquela trilha — não
            //     sobrescreve histórico nem reabre ilhas já desbloqueadas.
            //     ROW_NUMBER seleciona o 1º content (Order ASC, Id ASC)
            //     determinístico igual à query do TrailProgressService.
            migrationBuilder.Sql(@"
                INSERT INTO user_content
                    (user_id, content_id, is_completed, started_at,
                     challenges_completed, status)
                SELECT ut.user_id, first.id, false, NOW(), 0, 1
                FROM user_trail ut
                CROSS JOIN LATERAL (
                    SELECT c.id
                    FROM content c
                    WHERE c.trail_id = ut.trail_id
                      AND c.is_active = true
                    ORDER BY c.""order"" ASC, c.id ASC
                    LIMIT 1
                ) AS first
                WHERE NOT EXISTS (
                    SELECT 1 FROM user_content uc
                    JOIN content c2 ON c2.id = uc.content_id
                    WHERE uc.user_id = ut.user_id
                      AND c2.trail_id = ut.trail_id
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "challenges_completed",
                table: "user_content");

            migrationBuilder.DropColumn(
                name: "status",
                table: "user_content");

            migrationBuilder.DropColumn(
                name: "challenges_required",
                table: "content");
        }
    }
}
