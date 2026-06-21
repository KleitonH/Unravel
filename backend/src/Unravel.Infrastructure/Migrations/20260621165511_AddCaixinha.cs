using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCaixinha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "caixinha",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    emblem = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    leader_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caixinha", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "caixinha_member",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    caixinha_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caixinha_member", x => x.id);
                    table.ForeignKey(
                        name: "fk_caixinha_member_caixinha_caixinha_id",
                        column: x => x.caixinha_id,
                        principalTable: "caixinha",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_caixinha_member_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "caixinha_message",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    caixinha_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caixinha_message", x => x.id);
                    table.ForeignKey(
                        name: "fk_caixinha_message_caixinha_caixinha_id",
                        column: x => x.caixinha_id,
                        principalTable: "caixinha",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_caixinha_message_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_caixinha_leader_id",
                table: "caixinha",
                column: "leader_id");

            migrationBuilder.CreateIndex(
                name: "ix_caixinha_member_caixinha_id",
                table: "caixinha_member",
                column: "caixinha_id");

            migrationBuilder.CreateIndex(
                name: "ix_caixinha_member_user_id",
                table: "caixinha_member",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_caixinha_message_caixinha_id_created_at",
                table: "caixinha_message",
                columns: new[] { "caixinha_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_caixinha_message_user_id",
                table: "caixinha_message",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "caixinha_member");

            migrationBuilder.DropTable(
                name: "caixinha_message");

            migrationBuilder.DropTable(
                name: "caixinha");
        }
    }
}
