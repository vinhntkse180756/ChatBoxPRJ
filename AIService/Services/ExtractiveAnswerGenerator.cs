using System.Diagnostics;
using System.Runtime.CompilerServices;
using AIService.Models;

namespace AIService.Services;

public sealed class ExtractiveAnswerGenerator : IAnswerGenerator
{
    public Task<string> GenerateAsync(string question, IReadOnlyList<AnswerChunkContext> context, IReadOnlyList<AnswerMessageContext> history, CancellationToken ct = default)
    {
        var passages = context.Take(3).Select((x, i) => $"{i + 1}. {Shorten(x.Content, 650)}");
        var answer = "Dựa trên tài liệu của môn học, các nội dung liên quan nhất là:\n\n" + string.Join("\n\n", passages) +
                     "\n\nBạn có thể hỏi cụ thể hơn để mình giải thích sâu một ý trong các đoạn trên.";
        return Task.FromResult(answer);
    }

    public async IAsyncEnumerable<GenerateChunkResult> GenerateStreamAsync(
        string question,
        IReadOnlyList<AnswerChunkContext> context,
        IReadOnlyList<AnswerMessageContext> history,
        string? modelOverride = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        // Simulate a small network delay for local mock
        await Task.Delay(40, ct);

        var passages = context.Take(3).Select((x, i) => $"{i + 1}. {Shorten(x.Content, 650)}");
        var answer = "Dựa trên tài liệu của môn học, các nội dung liên quan nhất là:\n\n" + string.Join("\n\n", passages) +
                     "\n\nBạn có thể hỏi cụ thể hơn để mình giải thích sâu một ý trong các đoạn trên.";

        var words = answer.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            var chunkText = words[i] + (i == words.Length - 1 ? "" : " ");
            // Simulate inter-chunk delay of 12ms
            await Task.Delay(12, ct);
            yield return new GenerateChunkResult(chunkText, sw.ElapsedMilliseconds);
        }
    }

    private static string Shorten(string value, int max) => value.Length <= max ? value : value[..max].TrimEnd() + "…";
}
