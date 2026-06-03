using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModeratorGoldItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "moderator_gold_item",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    content_id = table.Column<int>(type: "integer", nullable: false),
                    curator_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_generated_challenge_id = table.Column<int>(type: "integer", nullable: true),
                    source_claim = table.Column<string>(type: "text", nullable: true),
                    prompt = table.Column<string>(type: "text", nullable: false),
                    correct_answer = table.Column<string>(type: "text", nullable: false),
                    distractors_json = table.Column<string>(type: "text", nullable: false),
                    explanation = table.Column<string>(type: "text", nullable: true),
                    difficulty_hint = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moderator_gold_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_moderator_gold_item_content_content_id",
                        column: x => x.content_id,
                        principalTable: "content",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_moderator_gold_item_content_id_is_active",
                table: "moderator_gold_item",
                columns: new[] { "content_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "moderator_gold_item");
        }
    }
}
