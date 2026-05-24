using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMastery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mastery",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic_id = table.Column<int>(type: "integer", nullable: false),
                    trail_id = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: false),
                    confidence = table.Column<int>(type: "integer", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    srs_interval_days = table.Column<int>(type: "integer", nullable: false),
                    ease_factor = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mastery", x => new { x.user_id, x.topic_id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_mastery_user_id_last_seen_at",
                table: "mastery",
                columns: new[] { "user_id", "last_seen_at" });

            migrationBuilder.CreateIndex(
                name: "ix_mastery_user_id_trail_id",
                table: "mastery",
                columns: new[] { "user_id", "trail_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mastery");
        }
    }
}
