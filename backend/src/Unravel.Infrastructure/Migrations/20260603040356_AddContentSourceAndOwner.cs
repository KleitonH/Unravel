using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContentSourceAndOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_published",
                table: "trail",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_user_id",
                table: "trail",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source",
                table: "trail",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "edited_at",
                table: "content",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "edited_by_user_id",
                table: "content",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source",
                table: "content",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_trail_source_owner_user_id",
                table: "trail",
                columns: new[] { "source", "owner_user_id" },
                filter: "\"owner_user_id\" IS NOT NULL");

            // PR 35: trilhas existentes são todas Git (source=0, default).
            // Marca todas como publicadas pra manter visibilidade. Novas trilhas
            // custom criadas via API começam como rascunho (is_published=false).
            migrationBuilder.Sql("UPDATE trail SET is_published = true WHERE source = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_trail_source_owner_user_id",
                table: "trail");

            migrationBuilder.DropColumn(
                name: "is_published",
                table: "trail");

            migrationBuilder.DropColumn(
                name: "owner_user_id",
                table: "trail");

            migrationBuilder.DropColumn(
                name: "source",
                table: "trail");

            migrationBuilder.DropColumn(
                name: "edited_at",
                table: "content");

            migrationBuilder.DropColumn(
                name: "edited_by_user_id",
                table: "content");

            migrationBuilder.DropColumn(
                name: "source",
                table: "content");
        }
    }
}
