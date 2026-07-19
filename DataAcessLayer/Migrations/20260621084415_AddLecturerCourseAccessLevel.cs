using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddLecturerCourseAccessLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LecturerCourses_CourseId",
                table: "LecturerCourses");

            migrationBuilder.AddColumn<int>(
                name: "AccessLevel",
                table: "LecturerCourses",
                type: "int",
                nullable: false,
                // Existing assignments represented course heads before access levels existed.
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_LecturerCourses_CourseId",
                table: "LecturerCourses",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_LecturerCourses_CourseId_AccessLevel",
                table: "LecturerCourses",
                columns: new[] { "CourseId", "AccessLevel" },
                unique: true,
                filter: "[AccessLevel] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LecturerCourses_CourseId",
                table: "LecturerCourses");

            migrationBuilder.DropIndex(
                name: "IX_LecturerCourses_CourseId_AccessLevel",
                table: "LecturerCourses");

            migrationBuilder.DropColumn(
                name: "AccessLevel",
                table: "LecturerCourses");

            migrationBuilder.CreateIndex(
                name: "IX_LecturerCourses_CourseId",
                table: "LecturerCourses",
                column: "CourseId",
                unique: true);
        }
    }
}
