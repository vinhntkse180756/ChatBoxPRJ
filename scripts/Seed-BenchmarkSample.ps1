$ErrorActionPreference = "Stop"

function Get-HashEmbed([string]$text) {
  $dims = 192
  $vector = New-Object float[] $dims
  $tokens = [regex]::Split($text.ToLowerInvariant(), '[ \r\n\t\.,;:\?\!\(\)\[\]\"]+') | Where-Object { $_ }
  $sha = [System.Security.Cryptography.SHA256]::Create()
  foreach ($token in $tokens) {
    $bytes = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($token))
    $index = [BitConverter]::ToUInt16($bytes, 0) % $dims
    if (($bytes[2] -band 1) -eq 0) { $vector[$index] += 1 } else { $vector[$index] -= 1 }
  }
  $sumSq = 0.0
  foreach ($v in $vector) { $sumSq += ($v * $v) }
  $norm = [Math]::Sqrt($sumSq)
  if ($norm -gt 0) { for ($i = 0; $i -lt $dims; $i++) { $vector[$i] = [float]($vector[$i] / $norm) } }
  $parts = foreach ($v in $vector) { $v.ToString([Globalization.CultureInfo]::InvariantCulture) }
  return "[" + ($parts -join ",") + "]"
}

$root = "D:\PRN222\ChatBoxPRJ\ChatBoxPRJ\App_Data\Uploads\PRN222"
New-Item -ItemType Directory -Force -Path $root | Out-Null

$content = @"
Razor Pages trong ASP.NET Core khac MVC Controllers. Razor Page dung PageModel thay vi Controller.
SignalR dung cho realtime web, hub cap nhat trang thai document processing.
Kien truc 3 lop Presentation Business DataAccess. Business khong goi EF truc tiep.
Entity Framework Core ket noi SQL Server bang DbContext va connection string.
Cookie authentication va role authorization bao ve trang Admin Lecturer Student.
RAG retrieval-augmented generation dung embedding chunk va vector search similarity score.
Background queue xu ly upload extract chunk embed index tai lieu.
Similarity threshold loc ket qua vector search TopK khi diem score thap se reject.
"@

$path = Join-Path $root "benchmark-sample.txt"
Set-Content -Path $path -Value $content -Encoding UTF8
$vec = Get-HashEmbed $content
$courseId = "9F55176A-BE96-4A05-B84E-9B7969E502B0"
$adminId = "4F0A6C6A-A676-4F88-B5A6-7BBC855FB8A7"
$docId = [guid]::NewGuid().ToString()
$chunkId = [guid]::NewGuid().ToString()
$shaBytes = [System.Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($content))
$sha = ([BitConverter]::ToString($shaBytes)).Replace("-", "").ToLowerInvariant()
$contentEsc = $content.Replace("'", "''")
$storageEsc = $path.Replace("'", "''")

$sqlPath = "D:\PRN222\ChatBoxPRJ\scripts\seed-benchmark-temp.sql"
@"
IF NOT EXISTS (SELECT 1 FROM Documents WHERE OriginalFileName=N'benchmark-sample.txt' AND CourseId='$courseId')
BEGIN
  INSERT INTO Documents (Id, CourseId, UploadedById, OriginalFileName, StoragePath, Sha256, Status, FailureReason, UploadedAtUtc)
  VALUES ('$docId', '$courseId', '$adminId', N'benchmark-sample.txt', N'$storageEsc', N'$sha', 1, NULL, SYSUTCDATETIME());
  INSERT INTO DocumentChunks (Id, DocumentId, CourseId, PageNumber, ChunkNumber, Content, VectorJson)
  VALUES ('$chunkId', '$docId', '$courseId', 1, 1, N'$contentEsc', N'$vec');
  PRINT 'SEED_OK';
END
ELSE
  PRINT 'SEED_EXISTS';

SELECT c.Code, d.OriginalFileName, d.Status, (SELECT COUNT(*) FROM DocumentChunks ch WHERE ch.DocumentId=d.Id) AS Chunks
FROM Documents d JOIN Courses c ON c.Id=d.CourseId
WHERE d.OriginalFileName=N'benchmark-sample.txt';
"@ | Set-Content -Path $sqlPath -Encoding UTF8

sqlcmd -S "(localdb)\MSSQLLocalDB" -d StudentChatBoxDb -i $sqlPath -W
Remove-Item $sqlPath -Force
