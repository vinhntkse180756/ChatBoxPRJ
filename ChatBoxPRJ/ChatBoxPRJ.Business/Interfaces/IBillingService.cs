using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ChatBoxPRJ.Business.DTOs;

namespace ChatBoxPRJ.Business.Interfaces;

public interface IBillingService
{
    Task<IReadOnlyList<BillingPackageDto>> ListPackagesAsync(CancellationToken ct = default);
    
    Task<(bool Success, string Message, string? TransactionNo)> CreateTransactionAsync(
        Guid userId, Guid packageId, CancellationToken ct = default);
        
    Task<(bool Success, string Message, int NewBalance)> CompleteTransactionAsync(
        string transactionNo, bool isSuccess, CancellationToken ct = default);
        
    Task<IReadOnlyList<PaymentTransactionDto>> ListUserTransactionsAsync(Guid userId, CancellationToken ct = default);
    
    Task<int> GetUserCreditsAsync(Guid userId, CancellationToken ct = default);
}
