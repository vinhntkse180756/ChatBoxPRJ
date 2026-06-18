/* Nâng cấp database cũ: tách lịch sử chat theo từng tài liệu. */
USE [StudentChatBoxDb];
GO

IF COL_LENGTH('dbo.ChatMessages', 'DocumentId') IS NULL
    ALTER TABLE [dbo].[ChatMessages] ADD [DocumentId] uniqueidentifier NULL;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_ChatMessages_SessionId_DocumentId_CreatedAtUtc'
      AND [object_id] = OBJECT_ID(N'dbo.ChatMessages')
)
BEGIN
    CREATE INDEX [IX_ChatMessages_SessionId_DocumentId_CreatedAtUtc]
        ON [dbo].[ChatMessages] ([SessionId], [DocumentId], [CreatedAtUtc]);
END;
GO
