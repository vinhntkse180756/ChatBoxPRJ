using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcessLayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBenchmarkRunDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BenchmarkRuns_Documents_DocumentId",
                table: "BenchmarkRuns");

            migrationBuilder.AddForeignKey(
                name: "FK_BenchmarkRuns_Documents_DocumentId",
                table: "BenchmarkRuns",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BenchmarkRuns_Documents_DocumentId",
                table: "BenchmarkRuns");

            migrationBuilder.AddForeignKey(
                name: "FK_BenchmarkRuns_Documents_DocumentId",
                table: "BenchmarkRuns",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
