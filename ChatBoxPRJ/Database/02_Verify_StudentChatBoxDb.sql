/* Kiểm tra database sau khi chạy 01_Create_StudentChatBoxDb.sql */

USE [StudentChatBoxDb];
GO

SELECT
    DB_NAME() AS [DatabaseName],
    SUSER_SNAME() AS [SqlLogin],
    USER_NAME() AS [DatabaseUser],
    @@SERVERNAME AS [ServerName];

SELECT
    t.[name] AS [TableName],
    SUM(p.[rows]) AS [RowCount]
FROM sys.tables AS t
LEFT JOIN sys.partitions AS p
    ON p.[object_id] = t.[object_id]
   AND p.[index_id] IN (0, 1)
WHERE t.[name] IN
(
    N'Users', N'Courses', N'LecturerCourses', N'Documents',
    N'DocumentChunks', N'ChatSessions', N'ChatMessages'
)
GROUP BY t.[name]
ORDER BY t.[name];

SELECT
    CASE
        WHEN COUNT(*) = 7 THEN N'OK - Đủ 7 bảng nghiệp vụ.'
        ELSE N'ERROR - Thiếu bảng, hãy chạy lại script 01.'
    END AS [SchemaStatus]
FROM sys.tables
WHERE [name] IN
(
    N'Users', N'Courses', N'LecturerCourses', N'Documents',
    N'DocumentChunks', N'ChatSessions', N'ChatMessages'
);
GO
