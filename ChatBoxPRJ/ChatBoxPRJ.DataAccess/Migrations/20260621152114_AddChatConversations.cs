using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatBoxPRJ.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddChatConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_SessionId_DocumentId_CreatedAtUtc",
                table: "ChatMessages");

            migrationBuilder.AddColumn<Guid>(
                name: "ConversationId",
                table: "ChatMessages",
                type: "uniqueidentifier",
                nullable: true);

            // Preserve old history by creating one conversation per session and document.
            migrationBuilder.Sql("""
                SELECT
                    [SessionId],
                    ISNULL([DocumentId], CAST('00000000-0000-0000-0000-000000000000' AS uniqueidentifier)) AS [DocumentKey],
                    NEWID() AS [ConversationId]
                INTO [#ConversationMap]
                FROM [ChatMessages]
                GROUP BY
                    [SessionId],
                    ISNULL([DocumentId], CAST('00000000-0000-0000-0000-000000000000' AS uniqueidentifier));

                UPDATE [message]
                SET [message].[ConversationId] = [map].[ConversationId]
                FROM [ChatMessages] AS [message]
                INNER JOIN [#ConversationMap] AS [map]
                    ON [map].[SessionId] = [message].[SessionId]
                   AND [map].[DocumentKey] = ISNULL(
                        [message].[DocumentId],
                        CAST('00000000-0000-0000-0000-000000000000' AS uniqueidentifier));

                DROP TABLE [#ConversationMap];
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "ConversationId",
                table: "ChatMessages",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SessionId_ConversationId_CreatedAtUtc",
                table: "ChatMessages",
                columns: new[] { "SessionId", "ConversationId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_SessionId_ConversationId_CreatedAtUtc",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ConversationId",
                table: "ChatMessages");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SessionId_DocumentId_CreatedAtUtc",
                table: "ChatMessages",
                columns: new[] { "SessionId", "DocumentId", "CreatedAtUtc" });
        }
    }
}
