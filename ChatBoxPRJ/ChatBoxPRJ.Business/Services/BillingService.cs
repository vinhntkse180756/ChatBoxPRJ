using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.DataAccess.Models;
using ChatBoxPRJ.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatBoxPRJ.Business.Services;

public sealed class BillingService(ChatBoxDbContext db) : IBillingService
{
    public async Task<IReadOnlyList<BillingPackageDto>> ListPackagesAsync(CancellationToken ct = default)
    {
        var packages = await db.BillingPackages
            .OrderBy(x => x.Price)
            .Select(x => new BillingPackageDto(x.Id, x.Name, x.Price, x.Credits, x.Description))
            .ToListAsync(ct);
        return packages;
    }

    public async Task<(bool Success, string Message, string? TransactionNo)> CreateTransactionAsync(
        Guid userId, Guid packageId, CancellationToken ct = default)
    {
        var user = await db.Users.FindAsync([userId], ct);
        if (user is null) return (false, "Người dùng không tồn tại.", null);

        var package = await db.BillingPackages.FindAsync([packageId], ct);
        if (package is null) return (false, "Gói cước không tồn tại.", null);

        // Tạo mã giao dịch ngẫu nhiên duy nhất
        var rand = new Random();
        var transactionNo = "VNP_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + rand.Next(100, 999);

        var transaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PackageId = packageId,
            Amount = package.Price,
            Status = "Pending",
            PaymentGate = "VNPAY_MOCK",
            TransactionNo = transactionNo,
            CreatedAtUtc = DateTime.UtcNow
        };

        await db.PaymentTransactions.AddAsync(transaction, ct);
        await db.SaveChangesAsync(ct);

        return (true, "Khởi tạo giao dịch thành công.", transactionNo);
    }

    public async Task<(bool Success, string Message, int NewBalance)> CompleteTransactionAsync(
        string transactionNo, bool isSuccess, CancellationToken ct = default)
    {
        using var dbTx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var transaction = await db.PaymentTransactions
                .Include(x => x.User)
                .Include(x => x.Package)
                .FirstOrDefaultAsync(x => x.TransactionNo == transactionNo, ct);

            if (transaction is null)
                return (false, "Không tìm thấy giao dịch.", 0);

            if (transaction.Status != "Pending")
                return (false, "Giao dịch này đã được xử lý trước đó.", transaction.User.Credits);

            if (isSuccess)
            {
                transaction.Status = "Completed";
                transaction.User.Credits += transaction.Package.Credits;
                await db.SaveChangesAsync(ct);
                await dbTx.CommitAsync(ct);
                return (true, $"Thanh toán thành công. Đã cộng {transaction.Package.Credits} credits vào tài khoản.", transaction.User.Credits);
            }
            else
            {
                transaction.Status = "Failed";
                await db.SaveChangesAsync(ct);
                await dbTx.CommitAsync(ct);
                return (false, "Giao dịch bị hủy hoặc thanh toán thất bại.", transaction.User.Credits);
            }
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync(ct);
            return (false, $"Lỗi hệ thống khi xử lý thanh toán: {ex.Message}", 0);
        }
    }

    public async Task<IReadOnlyList<PaymentTransactionDto>> ListUserTransactionsAsync(Guid userId, CancellationToken ct = default)
    {
        var list = await db.PaymentTransactions
            .Include(x => x.User)
            .Include(x => x.Package)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new PaymentTransactionDto(
                x.Id,
                x.UserId,
                x.User.FullName,
                x.PackageId,
                x.Package.Name,
                x.Amount,
                x.Status,
                x.PaymentGate,
                x.TransactionNo,
                x.CreatedAtUtc))
            .ToListAsync(ct);
        return list;
    }

    public async Task<int> GetUserCreditsAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.FindAsync([userId], ct);
        return user?.Credits ?? 0;
    }
}
