using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddBenchmarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BenchmarkRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QuestionCount = table.Column<int>(type: "int", nullable: false),
                    HitCount = table.Column<int>(type: "int", nullable: false),
                    RejectCount = table.Column<int>(type: "int", nullable: false),
                    AverageLatencyMs = table.Column<double>(type: "float", nullable: false),
                    AverageTopScore = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenchmarkRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BenchmarkRuns_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BenchmarkRuns_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BenchmarkRuns_Users_StartedById",
                        column: x => x.StartedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BenchmarkResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Question = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ExpectReject = table.Column<bool>(type: "bit", nullable: false),
                    Rejected = table.Column<bool>(type: "bit", nullable: false),
                    Hit = table.Column<bool>(type: "bit", nullable: false),
                    ExpectationMet = table.Column<bool>(type: "bit", nullable: false),
                    LatencyMs = table.Column<long>(type: "bigint", nullable: false),
                    TopScore = table.Column<double>(type: "float", nullable: false),
                    CitationCount = table.Column<int>(type: "int", nullable: false),
                    Answer = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenchmarkResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BenchmarkResults_BenchmarkRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "BenchmarkRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkResults_RunId_QuestionId",
                table: "BenchmarkResults",
                columns: new[] { "RunId", "QuestionId" });

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkRuns_CourseId",
                table: "BenchmarkRuns",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkRuns_DocumentId",
                table: "BenchmarkRuns",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkRuns_StartedAtUtc",
                table: "BenchmarkRuns",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BenchmarkRuns_StartedById",
                table: "BenchmarkRuns",
                column: "StartedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BenchmarkResults");

            migrationBuilder.DropTable(
                name: "BenchmarkRuns");
        }
    }
}
