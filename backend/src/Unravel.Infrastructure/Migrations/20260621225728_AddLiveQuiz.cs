using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Unravel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveQuiz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_quiz_answer",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<int>(type: "integer", nullable: false),
                    participant_id = table.Column<int>(type: "integer", nullable: false),
                    question_order_index = table.Column<int>(type: "integer", nullable: false),
                    selected_index = table.Column<int>(type: "integer", nullable: false),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false),
                    ms_to_answer = table.Column<int>(type: "integer", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    answered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_live_quiz_answer", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "live_quiz_session",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    host_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<int>(type: "integer", nullable: false),
                    join_code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    current_question_index = table.Column<int>(type: "integer", nullable: false),
                    current_question_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    seconds_per_question = table.Column<int>(type: "integer", nullable: false),
                    show_rank_between = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_live_quiz_session", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "live_quiz_allowed_user",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_live_quiz_allowed_user", x => x.id);
                    table.ForeignKey(
                        name: "fk_live_quiz_allowed_user_live_quiz_session_session_id",
                        column: x => x.session_id,
                        principalTable: "live_quiz_session",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_quiz_participant",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_live_quiz_participant", x => x.id);
                    table.ForeignKey(
                        name: "fk_live_quiz_participant_live_quiz_session_session_id",
                        column: x => x.session_id,
                        principalTable: "live_quiz_session",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_quiz_question",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<int>(type: "integer", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    generated_challenge_id = table.Column<int>(type: "integer", nullable: false),
                    prompt = table.Column<string>(type: "text", nullable: false),
                    options_json = table.Column<string>(type: "text", nullable: false),
                    correct_index = table.Column<int>(type: "integer", nullable: false),
                    explanation = table.Column<string>(type: "text", nullable: true),
                    shape = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_live_quiz_question", x => x.id);
                    table.ForeignKey(
                        name: "fk_live_quiz_question_live_quiz_session_session_id",
                        column: x => x.session_id,
                        principalTable: "live_quiz_session",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_live_quiz_allowed_user_session_id_user_id",
                table: "live_quiz_allowed_user",
                columns: new[] { "session_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_live_quiz_answer_participant_id_question_order_index",
                table: "live_quiz_answer",
                columns: new[] { "participant_id", "question_order_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_live_quiz_answer_session_id",
                table: "live_quiz_answer",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_quiz_participant_session_id_user_id",
                table: "live_quiz_participant",
                columns: new[] { "session_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_live_quiz_question_session_id_order_index",
                table: "live_quiz_question",
                columns: new[] { "session_id", "order_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_live_quiz_session_host_user_id",
                table: "live_quiz_session",
                column: "host_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_quiz_session_join_code",
                table: "live_quiz_session",
                column: "join_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_quiz_allowed_user");

            migrationBuilder.DropTable(
                name: "live_quiz_answer");

            migrationBuilder.DropTable(
                name: "live_quiz_participant");

            migrationBuilder.DropTable(
                name: "live_quiz_question");

            migrationBuilder.DropTable(
                name: "live_quiz_session");
        }
    }
}
