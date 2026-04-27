using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGamification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "active_cosmetic_id",
                table: "user",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "active_title",
                table: "user",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "coins",
                table: "user",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_activity_date",
                table: "user",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_login_date",
                table: "user",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "lives",
                table: "user",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "login_cycle_day",
                table: "user",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "longest_streak",
                table: "user",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "stars",
                table: "user",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "streak_days",
                table: "user",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "xp",
                table: "user",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "badge",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    icon = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    is_exclusive = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_badge", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "challenge",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    trail_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    xp_reward = table.Column<int>(type: "integer", nullable: false),
                    coins_reward = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_daily_challenge = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_challenge", x => x.id);
                    table.ForeignKey(
                        name: "fk_challenge_trail_trail_id",
                        column: x => x.trail_id,
                        principalTable: "trail",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "navi_cosmetic",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    rarity = table.Column<int>(type: "integer", nullable: false),
                    coin_price = table.Column<int>(type: "integer", nullable: false),
                    is_exclusive = table.Column<bool>(type: "boolean", nullable: false),
                    asset_slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_navi_cosmetic", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_badge",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    badge_id = table.Column<int>(type: "integer", nullable: false),
                    earned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_badge", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_badge_badge_badge_id",
                        column: x => x.badge_id,
                        principalTable: "badge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_badge_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "challenge_option",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    challenge_id = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_challenge_option", x => x.id);
                    table.ForeignKey(
                        name: "fk_challenge_option_challenge_challenge_id",
                        column: x => x.challenge_id,
                        principalTable: "challenge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_challenge",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    challenge_id = table.Column<int>(type: "integer", nullable: false),
                    correct_answers = table.Column<int>(type: "integer", nullable: false),
                    total_questions = table.Column<int>(type: "integer", nullable: false),
                    xp_earned = table.Column<int>(type: "integer", nullable: false),
                    coins_earned = table.Column<int>(type: "integer", nullable: false),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    is_perfect = table.Column<bool>(type: "boolean", nullable: false),
                    avg_response_time_ms = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_challenge", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_challenge_challenge_challenge_id",
                        column: x => x.challenge_id,
                        principalTable: "challenge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_challenge_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_cosmetic",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cosmetic_id = table.Column<int>(type: "integer", nullable: false),
                    is_equipped = table.Column<bool>(type: "boolean", nullable: false),
                    acquired_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_cosmetic", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_cosmetic_navi_cosmetic_cosmetic_id",
                        column: x => x.cosmetic_id,
                        principalTable: "navi_cosmetic",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_cosmetic_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_challenge_trail_id",
                table: "challenge",
                column: "trail_id");

            migrationBuilder.CreateIndex(
                name: "ix_challenge_option_challenge_id",
                table: "challenge_option",
                column: "challenge_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_badge_badge_id",
                table: "user_badge",
                column: "badge_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_badge_user_id_badge_id",
                table: "user_badge",
                columns: new[] { "user_id", "badge_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_challenge_challenge_id",
                table: "user_challenge",
                column: "challenge_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_challenge_user_id_challenge_id_started_at",
                table: "user_challenge",
                columns: new[] { "user_id", "challenge_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_user_cosmetic_cosmetic_id",
                table: "user_cosmetic",
                column: "cosmetic_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_cosmetic_user_id_cosmetic_id",
                table: "user_cosmetic",
                columns: new[] { "user_id", "cosmetic_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "challenge_option");

            migrationBuilder.DropTable(
                name: "user_badge");

            migrationBuilder.DropTable(
                name: "user_challenge");

            migrationBuilder.DropTable(
                name: "user_cosmetic");

            migrationBuilder.DropTable(
                name: "badge");

            migrationBuilder.DropTable(
                name: "challenge");

            migrationBuilder.DropTable(
                name: "navi_cosmetic");

            migrationBuilder.DropColumn(
                name: "active_cosmetic_id",
                table: "user");

            migrationBuilder.DropColumn(
                name: "active_title",
                table: "user");

            migrationBuilder.DropColumn(
                name: "coins",
                table: "user");

            migrationBuilder.DropColumn(
                name: "last_activity_date",
                table: "user");

            migrationBuilder.DropColumn(
                name: "last_login_date",
                table: "user");

            migrationBuilder.DropColumn(
                name: "lives",
                table: "user");

            migrationBuilder.DropColumn(
                name: "login_cycle_day",
                table: "user");

            migrationBuilder.DropColumn(
                name: "longest_streak",
                table: "user");

            migrationBuilder.DropColumn(
                name: "stars",
                table: "user");

            migrationBuilder.DropColumn(
                name: "streak_days",
                table: "user");

            migrationBuilder.DropColumn(
                name: "xp",
                table: "user");
        }
    }
}
