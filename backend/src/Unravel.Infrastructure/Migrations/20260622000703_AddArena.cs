using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArena : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "arena_match",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player1id = table.Column<Guid>(type: "uuid", nullable: false),
                    player2id = table.Column<Guid>(type: "uuid", nullable: true),
                    trail_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    winner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    score1 = table.Column<int>(type: "integer", nullable: false),
                    score2 = table.Column<int>(type: "integer", nullable: false),
                    is_direct_challenge = table.Column<bool>(type: "boolean", nullable: false),
                    current_round_index = table.Column<int>(type: "integer", nullable: false),
                    current_round_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    seconds_per_question = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arena_match", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "arena_queue_entry",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trail_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arena_queue_entry", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "arena_ranking",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    wins = table.Column<int>(type: "integer", nullable: false),
                    losses = table.Column<int>(type: "integer", nullable: false),
                    draws = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arena_ranking", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_arena_ranking_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "arena_round",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    match_id = table.Column<int>(type: "integer", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    generated_challenge_id = table.Column<int>(type: "integer", nullable: false),
                    prompt = table.Column<string>(type: "text", nullable: false),
                    options_json = table.Column<string>(type: "text", nullable: false),
                    correct_index = table.Column<int>(type: "integer", nullable: false),
                    explanation = table.Column<string>(type: "text", nullable: true),
                    shape = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    selected_index1 = table.Column<int>(type: "integer", nullable: true),
                    ms_to_answer1 = table.Column<int>(type: "integer", nullable: true),
                    points1 = table.Column<int>(type: "integer", nullable: false),
                    selected_index2 = table.Column<int>(type: "integer", nullable: true),
                    ms_to_answer2 = table.Column<int>(type: "integer", nullable: true),
                    points2 = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_arena_round", x => x.id);
                    table.ForeignKey(
                        name: "fk_arena_round_arena_match_match_id",
                        column: x => x.match_id,
                        principalTable: "arena_match",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_arena_match_player1id",
                table: "arena_match",
                column: "player1id");

            migrationBuilder.CreateIndex(
                name: "ix_arena_match_player2id",
                table: "arena_match",
                column: "player2id");

            migrationBuilder.CreateIndex(
                name: "ix_arena_match_status",
                table: "arena_match",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_arena_queue_entry_trail_id",
                table: "arena_queue_entry",
                column: "trail_id");

            migrationBuilder.CreateIndex(
                name: "ix_arena_queue_entry_user_id",
                table: "arena_queue_entry",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_arena_ranking_points",
                table: "arena_ranking",
                column: "points");

            migrationBuilder.CreateIndex(
                name: "ix_arena_round_match_id_order_index",
                table: "arena_round",
                columns: new[] { "match_id", "order_index" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "arena_queue_entry");

            migrationBuilder.DropTable(
                name: "arena_ranking");

            migrationBuilder.DropTable(
                name: "arena_round");

            migrationBuilder.DropTable(
                name: "arena_match");
        }
    }
}
