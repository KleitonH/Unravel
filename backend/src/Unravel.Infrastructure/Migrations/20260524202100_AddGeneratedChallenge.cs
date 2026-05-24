using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGeneratedChallenge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "generated_challenge",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    content_id = table.Column<int>(type: "integer", nullable: false),
                    topic_id = table.Column<int>(type: "integer", nullable: false),
                    trail_id = table.Column<int>(type: "integer", nullable: false),
                    strategy = table.Column<int>(type: "integer", nullable: false),
                    prompt = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    body_json = table.Column<string>(type: "text", nullable: false),
                    estimated_difficulty = table.Column<double>(type: "double precision", nullable: false),
                    served_count = table.Column<int>(type: "integer", nullable: false),
                    correct_rate = table.Column<double>(type: "double precision", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_generated_challenge", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_generated_challenge_content_id_is_active_served_count",
                table: "generated_challenge",
                columns: new[] { "content_id", "is_active", "served_count" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "generated_challenge");
        }
    }
}
