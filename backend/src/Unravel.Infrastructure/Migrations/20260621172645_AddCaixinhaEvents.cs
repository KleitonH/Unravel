using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCaixinhaEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "caixinha_event",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    theme = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caixinha_event", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "caixinha_event_score",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_id = table.Column<int>(type: "integer", nullable: false),
                    caixinha_id = table.Column<int>(type: "integer", nullable: false),
                    baseline_points = table.Column<int>(type: "integer", nullable: false),
                    final_points = table.Column<int>(type: "integer", nullable: true),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caixinha_event_score", x => x.id);
                    table.ForeignKey(
                        name: "fk_caixinha_event_score_caixinha_caixinha_id",
                        column: x => x.caixinha_id,
                        principalTable: "caixinha",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_caixinha_event_score_caixinha_event_event_id",
                        column: x => x.event_id,
                        principalTable: "caixinha_event",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_caixinha_event_starts_at_ends_at",
                table: "caixinha_event",
                columns: new[] { "starts_at", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "ix_caixinha_event_score_caixinha_id",
                table: "caixinha_event_score",
                column: "caixinha_id");

            migrationBuilder.CreateIndex(
                name: "ix_caixinha_event_score_event_id_caixinha_id",
                table: "caixinha_event_score",
                columns: new[] { "event_id", "caixinha_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "caixinha_event_score");

            migrationBuilder.DropTable(
                name: "caixinha_event");
        }
    }
}
