using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddBenchmarkStreamingMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AverageChunksPerSecond",
                table: "BenchmarkRuns",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "AverageTtftMs",
                table: "BenchmarkRuns",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "ModelName",
                table: "BenchmarkRuns",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ChunkCount",
                table: "BenchmarkResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "ChunksPerSecond",
                table: "BenchmarkResults",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "ModelName",
                table: "BenchmarkResults",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "TtftMs",
                table: "BenchmarkResults",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageChunksPerSecond",
                table: "BenchmarkRuns");

            migrationBuilder.DropColumn(
                name: "AverageTtftMs",
                table: "BenchmarkRuns");

            migrationBuilder.DropColumn(
                name: "ModelName",
                table: "BenchmarkRuns");

            migrationBuilder.DropColumn(
                name: "ChunkCount",
                table: "BenchmarkResults");

            migrationBuilder.DropColumn(
                name: "ChunksPerSecond",
                table: "BenchmarkResults");

            migrationBuilder.DropColumn(
                name: "ModelName",
                table: "BenchmarkResults");

            migrationBuilder.DropColumn(
                name: "TtftMs",
                table: "BenchmarkResults");
        }
    }
}
