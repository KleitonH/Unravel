using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChallengeFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "challenge_feedback",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    generated_challenge_id = table.Column<int>(type: "integer", nullable: false),
                    content_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_challenge_feedback", x => x.id);
                    table.ForeignKey(
                        name: "fk_challenge_feedback_generated_challenge_generated_challenge_",
                        column: x => x.generated_challenge_id,
                        principalTable: "generated_challenge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_challenge_feedback_content_id_status",
                table: "challenge_feedback",
                columns: new[] { "content_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_challenge_feedback_generated_challenge_id_status",
                table: "challenge_feedback",
                columns: new[] { "generated_challenge_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_challenge_feedback_generated_challenge_id_user_id",
                table: "challenge_feedback",
                columns: new[] { "generated_challenge_id", "user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "challenge_feedback");
        }
    }
}
