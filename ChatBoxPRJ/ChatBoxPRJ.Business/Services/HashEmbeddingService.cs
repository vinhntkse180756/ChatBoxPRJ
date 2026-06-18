using System.Security.Cryptography;
using System.Text;
using ChatBoxPRJ.Business.Interfaces;

namespace ChatBoxPRJ.Business.Services;

public sealed class HashEmbeddingService : IEmbeddingService
{
    private const int Dimensions = 192;
    public Task<float[]> EmbedAsync(string text, EmbeddingTask task = EmbeddingTask.Document, CancellationToken ct = default)
    {
        var vector = new float[Dimensions];
        foreach (var token in Tokenize(text))
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            var index = BitConverter.ToUInt16(bytes, 0) % Dimensions;
            vector[index] += (bytes[2] & 1) == 0 ? 1 : -1;
        }
        var norm = MathF.Sqrt(vector.Sum(x => x * x));
        if (norm > 0) for (var i = 0; i < vector.Length; i++) vector[i] /= norm;
        return Task.FromResult(vector);
    }

    private static IEnumerable<string> Tokenize(string text) => text.ToLowerInvariant().Split(
        [' ', '\r', '\n', '\t', '.', ',', ';', ':', '?', '!', '(', ')', '[', ']', '"'], StringSplitOptions.RemoveEmptyEntries);
}
