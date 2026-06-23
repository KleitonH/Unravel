using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLifeRefill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_life_refill_at",
                table: "user",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_life_refill_at",
                table: "user");
        }
    }
}
