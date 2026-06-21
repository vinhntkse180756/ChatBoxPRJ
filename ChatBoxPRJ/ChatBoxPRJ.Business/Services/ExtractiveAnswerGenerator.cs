using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;

namespace ChatBoxPRJ.Business.Services;

public sealed class ExtractiveAnswerGenerator : IAnswerGenerator
{
    public Task<string> GenerateAsync(string question, IReadOnlyList<RetrievedChunkContext> context, IReadOnlyList<ChatMessageContext> history, CancellationToken ct = default)
    {
        var passages = context.Take(3).Select((x, i) => $"{i + 1}. {Shorten(x.Content, 650)}");
        var answer = "Dựa trên tài liệu của môn học, các nội dung liên quan nhất là:\n\n" + string.Join("\n\n", passages) +
                     "\n\nBạn có thể hỏi cụ thể hơn để mình giải thích sâu một ý trong các đoạn trên.";
        return Task.FromResult(answer);
    }

    private static string Shorten(string value, int max) => value.Length <= max ? value : value[..max].TrimEnd() + "…";
}
