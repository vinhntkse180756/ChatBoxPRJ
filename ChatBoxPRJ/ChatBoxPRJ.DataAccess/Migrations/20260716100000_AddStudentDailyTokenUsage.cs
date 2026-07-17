using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ChatBoxPRJ.DataAccess.Persistence;

#nullable disable

namespace ChatBoxPRJ.DataAccess.Migrations
{
    [DbContext(typeof(ChatBoxDbContext))]
    [Migration("20260716100000_AddStudentDailyTokenUsage")]
    public partial class AddStudentDailyTokenUsage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentDailyTokenUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsageDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TokensUsed = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentDailyTokenUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentDailyTokenUsages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentDailyTokenUsages_UserId_UsageDate",
                table: "StudentDailyTokenUsages",
                columns: new[] { "UserId", "UsageDate" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "StudentDailyTokenUsages");
        }
    }
}
