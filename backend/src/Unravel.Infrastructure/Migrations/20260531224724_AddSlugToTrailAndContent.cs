using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSlugToTrailAndContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "slug",
                table: "trail",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "slug",
                table: "content",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_trail_slug",
                table: "trail",
                column: "slug",
                unique: true,
                filter: "\"slug\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_content_slug",
                table: "content",
                column: "slug",
                unique: true,
                filter: "\"slug\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_trail_slug",
                table: "trail");

            migrationBuilder.DropIndex(
                name: "ix_content_slug",
                table: "content");

            migrationBuilder.DropColumn(
                name: "slug",
                table: "trail");

            migrationBuilder.DropColumn(
                name: "slug",
                table: "content");
        }
    }
}
