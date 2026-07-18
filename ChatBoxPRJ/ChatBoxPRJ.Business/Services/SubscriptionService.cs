using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.External;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.Business.Options;
using ChatBoxPRJ.DataAccess.Interfaces;
using ChatBoxPRJ.DataAccess.Models;

namespace ChatBoxPRJ.Business.Services;

public sealed class SubscriptionService(
    ISubscriptionRepository subscriptions,
    StudentUsageOptions freeDefaults,
    VnPayGateway vnPay) : ISubscriptionService
{
    public async Task<IReadOnlyList<SubscriptionPackageDto>> ListPackagesAsync(Guid userId, CancellationToken ct = default)
    {
        var packages = await subscriptions.ListActivePackagesAsync(ct);
        var current = await GetEffectiveSubscriptionAsync(userId, ct);
        return packages.Select(p => new SubscriptionPackageDto(
            p.Id,
            p.Code,
            p.Name,
            p.Description,
            p.PriceVnd,
            p.ChatQuestionsPerDay,
            p.MaxQuestionChars,
            p.DurationDays,
            string.Equals(p.Code, current.PackageCode, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    public async Task<StudentSubscriptionDto> GetEffectiveSubscriptionAsync(Guid userId, CancellationToken ct = default)
    {
        var (limit, maxChars, code, name, ends) = await ResolveInternalAsync(userId, ct);
        return new(code, name, limit, maxChars, ends);
    }

    public async Task<(int QuestionsPerDay, int MaxQuestionChars, string PackageCode, string PackageName)> ResolveLimitsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var (limit, maxChars, code, name, _) = await ResolveInternalAsync(userId, ct);
        return (limit, maxChars, code, name);
    }

    public async Task<CreatePaymentResult> StartCheckoutAsync(
        Guid userId,
        Guid packageId,
        string clientIp,
        string returnUrl,
        CancellationToken ct = default)
    {
        if (!vnPay.IsConfigured)
            return new(false, "VNPay chưa được cấu hình. Vui lòng bổ sung VnPay:TmnCode và VnPay:HashSecret trong appsettings.");

        var package = await subscriptions.FindPackageByIdAsync(packageId, ct);
        if (package is null || !package.IsActive)
            return new(false, "Không tìm thấy gói đăng ký.");

        if (package.PriceVnd <= 0)
            return new(false, "Gói Free không cần thanh toán.");

        var current = await GetEffectiveSubscriptionAsync(userId, ct);
        if (string.Equals(current.PackageCode, package.Code, StringComparison.OrdinalIgnoreCase)
            && current.EndsAtUtc is { } ends
            && ends > DateTime.UtcNow.AddDays(1))
        {
            // Cho phép gia hạn cùng gói; không chặn.
        }

        var orderCode = $"CB{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
        var order = new PaymentOrder
        {
            OrderCode = orderCode,
            UserId = userId,
            PackageId = package.Id,
            AmountVnd = package.PriceVnd,
            Currency = "VND",
            Status = PaymentOrderStatus.Pending,
            Provider = "VNPay"
        };
        await subscriptions.AddPaymentOrderAsync(order, ct);

        var paymentUrl = vnPay.BuildPaymentUrl(
            orderCode,
            package.PriceVnd,
            $"Nang cap goi {package.Name}",
            clientIp,
            returnUrl,
            DateTime.Now);

        return new(true, "Đang chuyển sang VNPay…", paymentUrl);
    }

    public Task<PaymentCallbackResult> CompleteVnPayReturnAsync(
        IReadOnlyDictionary<string, string> query,
        CancellationToken ct = default)
        => CompleteCallbackAsync(query, activateOnSuccess: true, ct);

    public Task<PaymentCallbackResult> CompleteVnPayIpnAsync(
        IReadOnlyDictionary<string, string> query,
        CancellationToken ct = default)
        => CompleteCallbackAsync(query, activateOnSuccess: true, ct);

    private async Task<PaymentCallbackResult> CompleteCallbackAsync(
        IReadOnlyDictionary<string, string> query,
        bool activateOnSuccess,
        CancellationToken ct)
    {
        if (!vnPay.ValidateSignature(query))
            return new(false, "Chữ ký VNPay không hợp lệ.");

        if (!query.TryGetValue("vnp_TxnRef", out var orderCode) || string.IsNullOrWhiteSpace(orderCode))
            return new(false, "Thiếu mã đơn hàng.");

        var order = await subscriptions.FindPaymentByOrderCodeAsync(orderCode, ct);
        if (order is null)
            return new(false, "Không tìm thấy đơn thanh toán.", orderCode);

        var responseCode = query.GetValueOrDefault("vnp_ResponseCode");
        var transactionNo = query.GetValueOrDefault("vnp_TransactionNo");
        order.ProviderResponseCode = responseCode;
        order.ProviderTransactionNo = transactionNo;

        if (responseCode != "00")
        {
            if (order.Status == PaymentOrderStatus.Pending)
                order.Status = PaymentOrderStatus.Failed;
            await subscriptions.UpdatePaymentOrderAsync(order, ct);
            return new(false, $"Thanh toán không thành công (mã {responseCode}).", orderCode, order.Package.Name);
        }

        if (order.Status == PaymentOrderStatus.Paid)
            return new(true, "Đơn hàng đã được kích hoạt trước đó.", orderCode, order.Package.Name);

        if (!activateOnSuccess)
            return new(true, "Xác nhận thanh toán thành công.", orderCode, order.Package.Name);

        var now = DateTime.UtcNow;
        order.Status = PaymentOrderStatus.Paid;
        order.PaidAtUtc = now;
        await subscriptions.UpdatePaymentOrderAsync(order, ct);

        await subscriptions.ExpireActiveSubscriptionsAsync(order.UserId, now, ct);
        var durationDays = order.Package.DurationDays > 0 ? order.Package.DurationDays : 30;
        await subscriptions.ActivateSubscriptionAsync(new UserSubscription
        {
            UserId = order.UserId,
            PackageId = order.PackageId,
            PaymentOrderId = order.Id,
            Status = SubscriptionStatus.Active,
            StartsAtUtc = now,
            EndsAtUtc = now.AddDays(durationDays)
        }, ct);

        return new(true, $"Thanh toán thành công. Đã kích hoạt gói {order.Package.Name}.", orderCode, order.Package.Name);
    }

    public async Task<PaymentResultDto?> GetPaymentResultAsync(string orderCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(orderCode)) return null;
        var order = await subscriptions.FindPaymentByOrderCodeAsync(orderCode.Trim(), ct);
        if (order is null) return null;

        DateTime? endsAt = null;
        if (order.Status == PaymentOrderStatus.Paid && order.Package.DurationDays > 0 && order.PaidAtUtc is { } paid)
            endsAt = paid.AddDays(order.Package.DurationDays);

        var success = order.Status == PaymentOrderStatus.Paid;
        var message = success
            ? $"Thanh toán thành công. Gói {order.Package.Name} đã được kích hoạt."
            : order.Status == PaymentOrderStatus.Failed
                ? $"Thanh toán không thành công (mã {order.ProviderResponseCode ?? "—"})."
                : "Đơn hàng đang chờ thanh toán.";

        return new(
            success,
            message,
            order.OrderCode,
            order.Package.Code,
            order.Package.Name,
            order.AmountVnd,
            order.Package.ChatQuestionsPerDay,
            order.PaidAtUtc,
            endsAt);
    }

    public async Task<(bool Success, string Message)> DowngradeToFreeAsync(Guid userId, CancellationToken ct = default)
    {
        var current = await GetEffectiveSubscriptionAsync(userId, ct);
        if (string.Equals(current.PackageCode, "FREE", StringComparison.OrdinalIgnoreCase))
            return (false, "Bạn đang dùng gói Free rồi.");

        await subscriptions.ExpireActiveSubscriptionsAsync(userId, DateTime.UtcNow, ct);
        return (true, "Đã chuyển về gói Free. Hạn mức 10 câu/ngày được áp dụng ngay.");
    }

    public async Task<AdminStudentsDashboardDto> GetAdminStudentsDashboardAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var rows = await subscriptions.ListStudentAccountsAsync(today, now, ct);
        var payments = await subscriptions.ListRecentPaymentsAsync(40, ct);

        var students = rows.Select(r => new AdminStudentAccountDto(
            r.UserId,
            r.Code,
            r.FullName,
            r.Email,
            r.CreatedAtUtc,
            r.PackageCode,
            r.PackageName,
            r.QuestionsPerDay,
            r.EndsAtUtc,
            r.QuestionsUsedToday,
            Math.Max(0, r.QuestionsPerDay - r.QuestionsUsedToday),
            r.PaidOrderCount,
            r.PaidAmountTotal)).ToList();

        var paymentDtos = payments.Select(p => new AdminPaymentDto(
            p.OrderCode,
            p.User.Code,
            p.User.FullName,
            p.Package.Name,
            p.AmountVnd,
            p.Status.ToString(),
            p.ProviderResponseCode,
            p.CreatedAtUtc,
            p.PaidAtUtc)).ToList();

        var (revenuePro, _, revenueTotal) = await subscriptions.GetPaidRevenueAsync(ct);

        return new(
            students.Count,
            students.Count(x => x.PackageCode == "FREE"),
            students.Count(x => x.PackageCode == "PRO"),
            paymentDtos.Count(x => x.Status == "Paid" && x.PaidAtUtc is { } paid && DateOnly.FromDateTime(paid) == today),
            revenuePro,
            revenueTotal,
            students,
            paymentDtos);
    }

    public async Task<(bool Success, string Message)> GrantPackageAsync(
        Guid studentId,
        string packageCode,
        CancellationToken ct = default)
    {
        var code = packageCode.Trim().ToUpperInvariant();
        if (code == "FREE")
            return await DowngradeToFreeAsync(studentId, ct);

        if (code == "PRE")
            return (false, "Gói Pre đã ngừng cung cấp. Chỉ còn Free và Pro.");

        var package = await subscriptions.FindPackageByCodeAsync(code, ct);
        if (package is null || !package.IsActive || package.PriceVnd <= 0)
            return (false, "Không tìm thấy gói Pro.");

        var now = DateTime.UtcNow;
        await subscriptions.ExpireActiveSubscriptionsAsync(studentId, now, ct);
        var days = package.DurationDays > 0 ? package.DurationDays : 30;
        await subscriptions.ActivateSubscriptionAsync(new UserSubscription
        {
            UserId = studentId,
            PackageId = package.Id,
            Status = SubscriptionStatus.Active,
            StartsAtUtc = now,
            EndsAtUtc = now.AddDays(days)
        }, ct);

        return (true, $"Đã gán gói {package.Name} cho sinh viên ({days} ngày).");
    }

    private async Task<(int QuestionsPerDay, int MaxQuestionChars, string PackageCode, string PackageName, DateTime? EndsAtUtc)> ResolveInternalAsync(
        Guid userId,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var active = await subscriptions.GetActiveSubscriptionAsync(userId, now, ct);
        if (active?.Package is { } paid)
            return (paid.ChatQuestionsPerDay, paid.MaxQuestionChars, paid.Code, paid.Name, active.EndsAtUtc);

        var free = await subscriptions.FindPackageByCodeAsync("FREE", ct);
        if (free is not null)
            return (free.ChatQuestionsPerDay, free.MaxQuestionChars, free.Code, free.Name, null);

        return (freeDefaults.DailyQuestionLimit, freeDefaults.MaxQuestionChars, "FREE", "Free", null);
    }
}
