using System;

namespace ChatBoxPRJ.Business.DTOs;

public sealed record BillingPackageDto(
    Guid Id,
    string Name,
    decimal Price,
    int Credits,
    string Description);

public sealed record PaymentTransactionDto(
    Guid Id,
    Guid UserId,
    string UserFullName,
    Guid PackageId,
    string PackageName,
    decimal Amount,
    string Status,
    string PaymentGate,
    string TransactionNo,
    DateTime CreatedAtUtc);
