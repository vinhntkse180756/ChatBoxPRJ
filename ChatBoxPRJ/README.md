# StudySpace - Chatbot tài liệu cho sinh viên

Ứng dụng ASP.NET Core 8 Razor Pages triển khai 6 luồng nghiệp vụ trong tài liệu yêu cầu: tài khoản/phân quyền, ma trận giảng viên - môn học, không gian tri thức mở, upload/index tài liệu, xóa nhất quán và chat RAG có trích dẫn.

## Chạy nhanh

```powershell
dotnet restore ChatBoxPRJ.csproj --configfile NuGet.Config
dotnet run --project ChatBoxPRJ.csproj
```

Ứng dụng luôn dùng SQL Server thật. Hãy tạo `StudentChatBoxDb` bằng script trong thư mục `Database` và cấu hình connection string trước khi chạy.

Tài khoản quản trị mặc định:

- Tên đăng nhập: `admin`
- Mật khẩu: `Admin@123`

Hãy đổi `SeedAdmin` trước khi triển khai thật.

## Kiến trúc

```text
ChatBoxPRJ (Presentation)
├── Razor Pages: Account, Admin, Lecturer, Student
├── Cookie Authentication + Role Authorization
├── SignalR DocumentHub
└── Background Document Queue
          ↓
ChatBoxPRJ.Business
├── Services: Account, Course, Document, Chat
├── DTOs + Domain models
└── Repository / AI / Vector interfaces
          ↓
ChatBoxPRJ.DataAccess
├── EF Core DbContext
├── Repositories
├── SQL Server provider
└── EF vector fallback
```

### Mô hình dữ liệu chính

- `AppUser`: Student, Lecturer, Admin; mật khẩu PBKDF2-SHA256.
- `Course` và `LecturerCourse`: quan hệ nhiều-nhiều, cập nhật bằng diff trong transaction.
- `LearningDocument` và `DocumentChunk`: SHA-256, trạng thái Processing/Completed/Failed, nội dung và vector.
- `ChatSession`: duy nhất theo cặp Student-Course.
- `ChatMessage`: lịch sử hội thoại và citations dạng JSON.

## Chế độ Production với SQL Server

1. Chạy `Database/01_Create_StudentChatBoxDb.sql` trong SSMS.
2. Cập nhật `ConnectionStrings:ChatBoxDb` trong biến môi trường hoặc User Secrets.
3. Chạy ứng dụng. Không có InMemory hoặc database giả làm fallback.

Ví dụ biến môi trường:

```powershell
$env:ConnectionStrings__ChatBoxDb="Server=.;Database=StudentChatBoxDb;Trusted_Connection=True;TrustServerCertificate=True"
dotnet run
```

### Tạo database bằng SQL Server Authentication

Mở [Database/01_Create_StudentChatBoxDb.sql](Database/01_Create_StudentChatBoxDb.sql) trong SSMS và Execute bằng tài khoản `sa`. Script tạo đầy đủ database, bảng, khóa ngoại và index. Sau đó chạy `Database/02_Verify_StudentChatBoxDb.sql` để kiểm tra đủ 7 bảng nghiệp vụ.

Sau đó chạy ứng dụng bằng SQL Authentication:

```powershell
$env:ConnectionStrings__ChatBoxDb="Server=.;Database=StudentChatBoxDb;User Id=chatbox_app;Password=MAT_KHAU_DA_DAT;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True"
dotnet run --project .\ChatBoxPRJ\ChatBoxPRJ.csproj
```

## Gemini + SQL Server Vector Store

Vector, nội dung chunk và metadata được lưu trực tiếp trong bảng `DocumentChunks`. Không cần Docker hoặc Qdrant. Để dùng Gemini cho embedding và sinh câu trả lời:

```powershell
$env:AI__Provider="Gemini"
$env:AI__GeminiApiKey="YOUR_KEY"
$env:VectorStore__Provider="SqlServer"
dotnet run
```

Adapter mặc định dùng `gemini-embedding-001`, `gemini-2.5-flash`; cosine search luôn lọc theo `CourseId`. Khi xóa tài liệu, các chunk/vector SQL liên quan được xóa cascade theo `DocumentId`.

## Luồng tài liệu

1. Kiểm tra quyền quản lý môn tại Business layer.
2. Tính SHA-256 và chặn trùng nội dung trong cùng môn.
3. Lưu metadata trạng thái `Processing`, đưa việc xử lý vào background queue.
4. Trích xuất DOCX/TXT/PDF text; PDF scan không có text chuyển sang `Failed`.
5. Chia đoạn 350 từ, overlap 50 từ; embedding và nạp vector store.
6. Chuyển `Completed` và phát sự kiện SignalR; chỉ tài liệu hoàn thành được sinh viên nhìn thấy.

Khi xóa, vector store luôn được xóa trước. Nếu hạ tầng vector lỗi, file vật lý và bản ghi SQL được giữ nguyên để tránh lệch dữ liệu.

## Lưu ý triển khai

- Không commit API key hoặc mật khẩu thật vào `appsettings.json`; dùng environment variables hoặc User Secrets.
- Bộ đọc PDF tích hợp phù hợp demo với PDF có text đơn giản. Với sản phẩm thật nên thay extractor bằng PdfPig/Azure Document Intelligence và bổ sung OCR cho PDF scan.
- `App_Data/Uploads` và `App_Data/Keys` phải có quyền ghi tại server.
- Ngưỡng similarity mặc định là `0.7`; có thể điều chỉnh ở `Rag:SimilarityThreshold` sau khi benchmark test set.
