using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ArenaHpDamage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "damage1",
                table: "arena_round",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "damage2",
                table: "arena_round",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "crit1",
                table: "arena_match",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "crit2",
                table: "arena_match",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "disconnected_at",
                table: "arena_match",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "disconnected_user_id",
                table: "arena_match",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "hp1",
                table: "arena_match",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "hp2",
                table: "arena_match",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "damage1",
                table: "arena_round");

            migrationBuilder.DropColumn(
                name: "damage2",
                table: "arena_round");

            migrationBuilder.DropColumn(
                name: "crit1",
                table: "arena_match");

            migrationBuilder.DropColumn(
                name: "crit2",
                table: "arena_match");

            migrationBuilder.DropColumn(
                name: "disconnected_at",
                table: "arena_match");

            migrationBuilder.DropColumn(
                name: "disconnected_user_id",
                table: "arena_match");

            migrationBuilder.DropColumn(
                name: "hp1",
                table: "arena_match");

            migrationBuilder.DropColumn(
                name: "hp2",
                table: "arena_match");
        }
    }
}
