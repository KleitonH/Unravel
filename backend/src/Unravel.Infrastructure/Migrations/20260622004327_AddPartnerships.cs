using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "partnership",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    addressee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    novelos_completed = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partnership", x => x.id);
                    table.ForeignKey(
                        name: "fk_partnership_user_addressee_id",
                        column: x => x.addressee_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_partnership_user_requester_id",
                        column: x => x.requester_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "partnership_log",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    partnership_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    message = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partnership_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_partnership_log_partnership_partnership_id",
                        column: x => x.partnership_id,
                        principalTable: "partnership",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "yarn_ball",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    partnership_id = table.Column<int>(type: "integer", nullable: false),
                    current_owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    progress = table.Column<int>(type: "integer", nullable: false),
                    daily_goal = table.Column<int>(type: "integer", nullable: false),
                    cycles_completed = table.Column<int>(type: "integer", nullable: false),
                    total_cycles = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_progress_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_yarn_ball", x => x.id);
                    table.ForeignKey(
                        name: "fk_yarn_ball_partnership_partnership_id",
                        column: x => x.partnership_id,
                        principalTable: "partnership",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_partnership_addressee_id",
                table: "partnership",
                column: "addressee_id");

            migrationBuilder.CreateIndex(
                name: "ix_partnership_requester_id",
                table: "partnership",
                column: "requester_id");

            migrationBuilder.CreateIndex(
                name: "ix_partnership_status",
                table: "partnership",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_partnership_log_partnership_id",
                table: "partnership_log",
                column: "partnership_id");

            migrationBuilder.CreateIndex(
                name: "ix_yarn_ball_partnership_id_is_completed",
                table: "yarn_ball",
                columns: new[] { "partnership_id", "is_completed" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "partnership_log");

            migrationBuilder.DropTable(
                name: "yarn_ball");

            migrationBuilder.DropTable(
                name: "partnership");
        }
    }
}
