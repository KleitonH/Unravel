using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTitles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "title",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    text = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    criterion = table.Column<int>(type: "integer", nullable: false),
                    threshold = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_title", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_title",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title_id = table.Column<int>(type: "integer", nullable: false),
                    earned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_title", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_title_title_title_id",
                        column: x => x.title_id,
                        principalTable: "title",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_title_code",
                table: "title",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_title_title_id",
                table: "user_title",
                column: "title_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_title_user_id_title_id",
                table: "user_title",
                columns: new[] { "user_id", "title_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_title");

            migrationBuilder.DropTable(
                name: "title");
        }
    }
}
