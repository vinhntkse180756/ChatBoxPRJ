using ChatBoxPRJ.Business;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.Business.Options;
using ChatBoxPRJ.DataAccess.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ChatBoxPRJ"));
var config = new ConfigurationBuilder()
    .SetBasePath(root)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var services = new ServiceCollection();
var rag = config.GetSection("Rag").Get<RagOptions>() ?? new RagOptions();
var storage = config.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
var ai = config.GetSection("AI").Get<AiOptions>() ?? new AiOptions();
var vectorStore = config.GetSection("VectorStore").Get<VectorStoreOptions>() ?? new VectorStoreOptions();
storage.RootPath = Path.GetFullPath(storage.RootPath, root);

// Force Local embeddings for deterministic smoke against seeded hash vectors.
ai.Provider = "Local";

services.AddBusinessLayer(new ApplicationLayerOptions
{
    ConnectionString = config.GetConnectionString("ChatBoxDb")
        ?? throw new InvalidOperationException("Missing connection string."),
    Rag = rag,
    Storage = storage,
    Ai = ai,
    VectorStore = vectorStore
});

await using var sp = services.BuildServiceProvider();
using var scope = sp.CreateScope();
var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
var benchmarks = scope.ServiceProvider.GetRequiredService<IBenchmarkService>();

var adminUser = await users.FindByLoginAsync("admin");
if (adminUser is null) { Console.WriteLine("FAIL: admin not found"); return 1; }

var testSet = Path.Combine(root, BenchmarkScope.TestSetRelativePath);
Console.WriteLine($"Test set: {testSet}");
Console.WriteLine($"Exists: {File.Exists(testSet)}");

var result = await benchmarks.RunAsync(adminUser.Id, testSet);
Console.WriteLine($"Success: {result.Success}");
Console.WriteLine($"Message: {result.Message}");
if (result.Run is null)
{
    Console.WriteLine("FAIL: no run returned");
    return 1;
}

var run = result.Run;
Console.WriteLine($"RunId: {run.Id}");
Console.WriteLine($"Questions: {run.QuestionCount}, Hit: {run.HitCount}, Reject: {run.RejectCount}");
Console.WriteLine($"AvgLatencyMs: {run.AverageLatencyMs:0}, AvgTopScore: {run.AverageTopScore:0.000}");
Console.WriteLine($"ExpectationMet: {run.ExpectationMetCount}/{run.QuestionCount}");
foreach (var row in run.Results)
    Console.WriteLine($"  {row.QuestionId}: latency={row.LatencyMs}ms top={row.TopScore:0.000} hit={row.Hit} reject={row.Rejected} expectOk={row.ExpectationMet}");

Console.WriteLine(result.Success && run.QuestionCount == 12 ? "SMOKE_PASS" : "SMOKE_FAIL");
return result.Success && run.QuestionCount == 12 ? 0 : 1;
