using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCaixinhaDailyGoal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "daily_goal",
                table: "caixinha",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<DateTime>(
                name: "daily_goal_awarded_at",
                table: "caixinha",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "daily_points",
                table: "caixinha",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "daily_points_date",
                table: "caixinha",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "daily_goal",
                table: "caixinha");

            migrationBuilder.DropColumn(
                name: "daily_goal_awarded_at",
                table: "caixinha");

            migrationBuilder.DropColumn(
                name: "daily_points",
                table: "caixinha");

            migrationBuilder.DropColumn(
                name: "daily_points_date",
                table: "caixinha");
        }
    }
}
