using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLeague : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_league",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier = table.Column<int>(type: "integer", nullable: false),
                    week_key = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    baseline_xp = table.Column<int>(type: "integer", nullable: false),
                    previous_rank = table.Column<int>(type: "integer", nullable: true),
                    previous_result = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_league", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_user_league_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_league_tier_week_key",
                table: "user_league",
                columns: new[] { "tier", "week_key" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_league");
        }
    }
}
