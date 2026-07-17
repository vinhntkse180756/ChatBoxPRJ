using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using ChatBoxPRJ.Business.Options;

namespace ChatBoxPRJ.Business.External;

public sealed class VnPayGateway(VnPayOptions options)
{
    public bool IsConfigured => options.IsConfigured;

    public string BuildPaymentUrl(
        string orderCode,
        decimal amountVnd,
        string orderInfo,
        string clientIp,
        string returnUrl,
        DateTime createTimeLocal)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Chưa cấu hình VNPay (TmnCode / HashSecret).");

        var amount = ((long)Math.Round(amountVnd * 100m, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
        var data = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = options.Version.Trim(),
            ["vnp_Command"] = options.Command.Trim(),
            ["vnp_TmnCode"] = options.TmnCodeTrimmed,
            ["vnp_Amount"] = amount,
            ["vnp_CurrCode"] = options.CurrCode.Trim(),
            ["vnp_TxnRef"] = orderCode,
            ["vnp_OrderInfo"] = orderInfo,
            ["vnp_OrderType"] = options.OrderType.Trim(),
            ["vnp_Locale"] = options.Locale.Trim(),
            ["vnp_ReturnUrl"] = returnUrl,
            ["vnp_IpAddr"] = string.IsNullOrWhiteSpace(clientIp) ? "127.0.0.1" : clientIp,
            ["vnp_CreateDate"] = createTimeLocal.ToString("yyyyMMddHHmmss")
        };

        if (!string.IsNullOrWhiteSpace(options.BankCode))
            data["vnp_BankCode"] = options.BankCode.Trim();

        var query = BuildQuery(data);
        var secureHash = HmacSha512(options.HashSecretTrimmed, query);
        return $"{options.PaymentUrl.Trim()}?{query}&vnp_SecureHash={secureHash}";
    }

    public bool ValidateSignature(IReadOnlyDictionary<string, string> query)
    {
        if (!IsConfigured) return false;
        if (!query.TryGetValue("vnp_SecureHash", out var secureHash) || string.IsNullOrWhiteSpace(secureHash))
            return false;

        var data = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in query)
        {
            if (string.IsNullOrEmpty(value)) continue;
            if (key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase)) continue;
            if (key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase)) continue;
            if (!key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase)) continue;
            data[key] = value;
        }

        var raw = BuildQuery(data);
        var computed = HmacSha512(options.HashSecretTrimmed, raw);
        return string.Equals(computed, secureHash, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Encode theo mẫu VNPay (.NET): UrlEncode rồi đưa %20 về +.</summary>
    private static string BuildQuery(SortedDictionary<string, string> data)
        => string.Join("&", data.Select(kv =>
            $"{Encode(kv.Key)}={Encode(kv.Value)}"));

    private static string Encode(string value)
        => WebUtility.UrlEncode(value)?.Replace("%20", "+", StringComparison.Ordinal) ?? "";

    private static string HmacSha512(string key, string input)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hash = HMACSHA512.HashData(keyBytes, inputBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
