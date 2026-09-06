using ECommerce.Application.DTOs.Order;

namespace ECommerce.Application.Interfaces.Services;

public interface IPaymentService
{
    Task<PaymentResult> AuthorizePaymentAsync(decimal amount, CancellationToken cancellationToken = default);
}