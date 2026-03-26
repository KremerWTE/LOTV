using Lotv.Core.Common;
using Lotv.Core.Models;

namespace Lotv.Core.Services.Interfaces;

public interface IPaymentService
{
    /// <summary>Create a Stripe PaymentIntent. Returns the client secret for the frontend.</summary>
    Task<Result<string>> CreatePaymentIntentAsync(decimal amount, string currency, int donorId, int chapterId);

    /// <summary>Process an inbound Stripe webhook event (signature already verified by controller).</summary>
    Task<Result> HandleWebhookAsync(string eventType, string payloadJson);

    /// <summary>Issue a refund for a processed donation.</summary>
    Task<Result> RefundAsync(int donationId, string approvedBy);
}
