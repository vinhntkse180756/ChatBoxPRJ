using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.Business.Mapping;
using ChatBoxPRJ.Business.Options;
using ChatBoxPRJ.Business.Services;
using ChatBoxPRJ.DataAccess.Interfaces;
using ChatBoxPRJ.DataAccess.Migrations;
using ChatBoxPRJ.DataAccess.Persistence;
using ChatBoxPRJ.DataAccess.Repositories;
using ChatBoxPRJ.Infrastructure;
using ChatBoxPRJ.Pages.SignalR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
// Chừa dung lượng cho phần header/boundary của multipart; DocumentService vẫn giới hạn file ở đúng 100 MB.
const long MaxUploadRequestSize = 110L * 1024 * 1024;
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxUploadRequestSize);
builder.Services.Configure<IISServerOptions>(options => options.MaxRequestBodySize = MaxUploadRequestSize);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = MaxUploadRequestSize);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "Keys")));

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
    options.Conventions.AuthorizeFolder("/Lecturer", "LecturersOnly");
    options.Conventions.AuthorizeFolder("/Student", "ChatUsers");
});
builder.Services.AddSignalR();
builder.Services.AddAutoMapper(_ => { }, typeof(MappingProfile).Assembly);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("LecturersOnly", p => p.RequireRole("Lecturer"));
    options.AddPolicy("StudentsOnly", p => p.RequireRole("Student"));
    options.AddPolicy("ChatUsers", p => p.RequireRole("Student", "Lecturer"));
});
builder.Services.AddDbContext<ChatBoxDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ChatBoxDb")
        ?? throw new InvalidOperationException("Thiếu ConnectionStrings:ChatBoxDb cho SQL Server."),
        sql => sql.EnableRetryOnFailure(3)));

var rag = builder.Configuration.GetSection("Rag").Get<RagOptions>() ?? new RagOptions();
var storage = builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
storage.RootPath = Path.GetFullPath(storage.RootPath, builder.Environment.ContentRootPath);
builder.Services.AddSingleton(rag);
builder.Services.AddSingleton(storage);
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IChatRepository, ChatRepository>();
// Vector và nội dung chunk được lưu trực tiếp trong SQL Server.
// Không phụ thuộc Docker/Qdrant khi chạy ứng dụng.
builder.Services.AddScoped<IVectorStore, EfVectorStore>();
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
if (builder.Configuration["AI:Provider"]?.Equals("Gemini", StringComparison.OrdinalIgnoreCase) == true)
{
    builder.Services.AddSingleton<IEmbeddingService, GeminiEmbeddingService>();
    builder.Services.AddSingleton<IAnswerGenerator, GeminiAnswerGenerator>();
}
else
{
    builder.Services.AddSingleton<IEmbeddingService, HashEmbeddingService>();
    builder.Services.AddSingleton<IAnswerGenerator, ExtractiveAnswerGenerator>();
}
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddSingleton<DocumentWorkQueue>();
builder.Services.AddSingleton<IDocumentWorkQueue>(sp => sp.GetRequiredService<DocumentWorkQueue>());
builder.Services.AddSingleton<IDocumentStatusNotifier, SignalRDocumentStatusNotifier>();
builder.Services.AddHostedService<DocumentWorker>();
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddScoped<DatabaseMigrationManager>();

var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error"); app.UseHsts(); }
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapHub<DocumentHub>("/hubs/documents");

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AutoMapper.IMapper>()
        .ConfigurationProvider.AssertConfigurationIsValid();
    await scope.ServiceProvider.GetRequiredService<DatabaseMigrationManager>().MigrateAsync();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync(
        builder.Configuration["SeedAdmin:Code"] ?? "admin",
        builder.Configuration["SeedAdmin:Email"] ?? "admin@chatbox.local",
        passwordHasher.Hash(builder.Configuration["SeedAdmin:Password"] ?? "Admin@123"));
}

app.Run();
