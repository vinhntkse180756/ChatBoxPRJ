using System.Threading.Channels;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Pages.SignalR;
using Microsoft.AspNetCore.SignalR;

namespace ChatBoxPRJ.Infrastructure;

public sealed class DocumentWorkQueue : IDocumentWorkQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();
    public ValueTask EnqueueAsync(Guid documentId, CancellationToken ct = default) => _channel.Writer.WriteAsync(documentId, ct);
    public ValueTask<Guid> DequeueAsync(CancellationToken ct) => _channel.Reader.ReadAsync(ct);
}

public sealed class DocumentWorker(DocumentWorkQueue queue, IServiceScopeFactory scopes, ILogger<DocumentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var id = await queue.DequeueAsync(stoppingToken);
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IDocumentService>().ProcessAsync(id, stoppingToken);
            }
            catch (Exception ex) { logger.LogError(ex, "Document worker failed for {DocumentId}", id); }
        }
    }
}

public sealed class SignalRDocumentStatusNotifier(IHubContext<DocumentHub> hub) : IDocumentStatusNotifier
{
    public Task NotifyAsync(Guid documentId, DocumentStatus status, int progress, string? message, CancellationToken ct = default)
        => hub.Clients.All.SendAsync("documentStatusChanged", new
        {
            documentId,
            status = status.ToString(),
            progress = Math.Clamp(progress, 0, 100),
            message
        }, ct);
}

public sealed class SignalRCourseStatusNotifier(IHubContext<CourseHub> hub) : ICourseStatusNotifier
{
    public Task NotifyCourseCreatedAsync(CourseDto course, CancellationToken ct = default)
        => hub.Clients.All.SendAsync("courseCreated", course, ct);

    public Task NotifyCourseUpdatedAsync(CourseDto course, CancellationToken ct = default)
        => hub.Clients.All.SendAsync("courseUpdated", course, ct);

    public Task NotifyCourseDeletedAsync(Guid courseId, CancellationToken ct = default)
        => hub.Clients.All.SendAsync("courseDeleted", courseId, ct);
}
