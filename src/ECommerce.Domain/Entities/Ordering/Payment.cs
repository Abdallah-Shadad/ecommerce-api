using System;
using ECommerce.Domain.Entities.Common;
using ECommerce.Domain.Enums;

namespace ECommerce.Domain.Entities.Ordering;

public class Payment : AuditableEntity
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string Provider { get; set; } = string.Empty;
    public string? TransactionReference { get; set; }
    public decimal Amount { get; set; }
    public DateTime? PaidAtUtc { get; set; }
}
