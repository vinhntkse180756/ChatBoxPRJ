using BusinessLogic;
using BusinessLogic.Interfaces;
using BusinessLogic.Options;
using Presentation.Infrastructure;
using Presentation.SignalR;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true);
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
    options.Conventions.AllowAnonymousToPage("/Payments/VnPayReturn");
    options.Conventions.AllowAnonymousToPage("/Payments/VnPayIpn");
    options.Conventions.AllowAnonymousToPage("/Payments/Result");
});
builder.Services.AddSignalR();
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

var rag = builder.Configuration.GetSection("Rag").Get<RagOptions>() ?? new RagOptions();
var storage = builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
var ai = builder.Configuration.GetSection("AI").Get<AiOptions>() ?? new AiOptions();
var vectorStore = builder.Configuration.GetSection("VectorStore").Get<VectorStoreOptions>() ?? new VectorStoreOptions();
var studentUsage = builder.Configuration.GetSection("StudentUsage").Get<StudentUsageOptions>() ?? new StudentUsageOptions();
var vnPay = builder.Configuration.GetSection("VnPay").Get<VnPayOptions>() ?? new VnPayOptions();
storage.RootPath = Path.GetFullPath(storage.RootPath, builder.Environment.ContentRootPath);
builder.Services.AddBusinessLayer(new ApplicationLayerOptions
{
    ConnectionString = builder.Configuration.GetConnectionString("ChatBoxDb")
        ?? throw new InvalidOperationException("Thiếu ConnectionStrings:ChatBoxDb cho SQL Server."),
    Rag = rag,
    Storage = storage,
    Ai = ai,
    VectorStore = vectorStore,
    StudentUsage = studentUsage,
    VnPay = vnPay
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<DocumentWorkQueue>();
builder.Services.AddSingleton<IDocumentWorkQueue>(sp => sp.GetRequiredService<DocumentWorkQueue>());
builder.Services.AddSingleton<IDocumentStatusNotifier, SignalRDocumentStatusNotifier>();
builder.Services.AddHostedService<DocumentWorker>();

var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error"); app.UseHsts(); }
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapHub<DocumentHub>("/hubs/documents");
app.MapHub<CourseHub>("/hubs/courses");

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<IApplicationInitializer>().InitializeAsync(
        builder.Configuration["SeedAdmin:Code"] ?? "admin",
        builder.Configuration["SeedAdmin:Email"] ?? "admin@chatbox.local",
        builder.Configuration["SeedAdmin:Password"] ?? "Admin@123");
}

app.Run();
