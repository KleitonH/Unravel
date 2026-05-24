using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChallengeContentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "content_id",
                table: "challenge",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_challenge_content_id",
                table: "challenge",
                column: "content_id");

            migrationBuilder.AddForeignKey(
                name: "fk_challenge_content_content_id",
                table: "challenge",
                column: "content_id",
                principalTable: "content",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_challenge_content_content_id",
                table: "challenge");

            migrationBuilder.DropIndex(
                name: "ix_challenge_content_id",
                table: "challenge");

            migrationBuilder.DropColumn(
                name: "content_id",
                table: "challenge");
        }
    }
}
