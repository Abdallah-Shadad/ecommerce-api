using ECommerce.Application.DTOs.Order;
using ECommerce.Application.Interfaces.Services;

namespace ECommerce.Infrastructure.Services;

public class MockPaymentService : IPaymentService
{
    // Simulate payment authorization logic
    public async Task<PaymentResult> AuthorizePaymentAsync(decimal amount, CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken); // Simulate some processing time
        return new PaymentResult(
            IsSuccess: true,
            TransactionReference: "MOCK-" + Guid.NewGuid().ToString(),
            ErrorMessage: null
        );
    }
}
