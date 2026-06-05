using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSeenChallenge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_seen_challenge",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generated_challenge_id = table.Column<int>(type: "integer", nullable: false),
                    seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    was_correct = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_seen_challenge", x => new { x.user_id, x.generated_challenge_id });
                    table.ForeignKey(
                        name: "fk_user_seen_challenge_generated_challenge_generated_challenge",
                        column: x => x.generated_challenge_id,
                        principalTable: "generated_challenge",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_seen_challenge_generated_challenge_id",
                table: "user_seen_challenge",
                column: "generated_challenge_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_seen_challenge");
        }
    }
}
