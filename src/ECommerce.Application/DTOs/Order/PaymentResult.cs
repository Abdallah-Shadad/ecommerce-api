namespace ECommerce.Application.DTOs.Order;

public record PaymentResult(
    bool IsSuccess,
    string? TransactionReference,
    string? ErrorMessage
);