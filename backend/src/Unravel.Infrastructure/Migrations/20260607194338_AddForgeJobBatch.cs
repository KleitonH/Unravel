using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddForgeJobBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Self-healing: migration anterior com mesmo nome ficou gravada
            // vazia no __EFMigrationsHistory durante refactor local. Remove
            // a entrada órfã pra essa migration aplicar limpo.
            migrationBuilder.Sql(
                "DELETE FROM \"__EFMigrationsHistory\" " +
                "WHERE \"migration_id\" = '20260607192432_AddForgeJobBatch';");

            // IF NOT EXISTS pra ser idempotente.
            migrationBuilder.Sql(
                "ALTER TABLE question_forge_job " +
                "ADD COLUMN IF NOT EXISTS batch_id uuid NULL;");
            migrationBuilder.Sql(
                "ALTER TABLE question_forge_job " +
                "ADD COLUMN IF NOT EXISTS enqueued_by_user_id uuid NULL;");

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_question_forge_job_batch_id " +
                "ON question_forge_job (batch_id) " +
                "WHERE batch_id IS NOT NULL;");
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_question_forge_job_enqueued_by_user_id_enqueued_at " +
                "ON question_forge_job (enqueued_by_user_id, enqueued_at) " +
                "WHERE enqueued_by_user_id IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_question_forge_job_batch_id",
                table: "question_forge_job");

            migrationBuilder.DropIndex(
                name: "ix_question_forge_job_enqueued_by_user_id_enqueued_at",
                table: "question_forge_job");

            migrationBuilder.DropColumn(
                name: "batch_id",
                table: "question_forge_job");

            migrationBuilder.DropColumn(
                name: "enqueued_by_user_id",
                table: "question_forge_job");
        }
    }
}
