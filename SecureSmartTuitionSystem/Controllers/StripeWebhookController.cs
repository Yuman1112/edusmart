using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SecureSmartTuitionSystem.Services.Billing;
using SecureSmartTuitionSystem.Settings;
using Stripe;
using Stripe.Checkout;

namespace SecureSmartTuitionSystem.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [Route("api/stripe/webhook")]
    public class StripeWebhookController : ControllerBase
    {
        private readonly IPaymentCompletionService
            _paymentCompletionService;

        private readonly StripeSettings
            _stripeSettings;

        private readonly ILogger<StripeWebhookController>
            _logger;

        public StripeWebhookController(
            IPaymentCompletionService
                paymentCompletionService,
            IOptions<StripeSettings> stripeSettings,
            ILogger<StripeWebhookController> logger)
        {
            _paymentCompletionService =
                paymentCompletionService;

            _stripeSettings =
                stripeSettings.Value;

            _logger =
                logger;
        }

        [HttpPost]
        public async Task<IActionResult> Receive(
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(
                    _stripeSettings.WebhookSecret))
            {
                _logger.LogError(
                    "Stripe webhook secret is not configured.");

                return StatusCode(
                    StatusCodes
                        .Status500InternalServerError);
            }

            string requestBody;

            using (var reader =
                   new StreamReader(Request.Body))
            {
                requestBody =
                    await reader.ReadToEndAsync(
                        cancellationToken);
            }

            string signatureHeader =
                Request.Headers["Stripe-Signature"]
                    .ToString();

            if (string.IsNullOrWhiteSpace(
                    signatureHeader))
            {
                return BadRequest(
                    "Stripe signature is missing.");
            }

            Event stripeEvent;

            try
            {
                stripeEvent =
                    EventUtility.ConstructEvent(
                        requestBody,
                        signatureHeader,
                        _stripeSettings.WebhookSecret);
            }
            catch (StripeException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Stripe webhook signature validation failed.");

                return BadRequest(
                    "Invalid Stripe webhook signature.");
            }

            /*
             * checkout.session.completed:
             * Used for immediate payment methods.
             *
             * checkout.session.async_payment_succeeded:
             * Used when a delayed payment method later succeeds.
             */
            bool isSupportedEvent =
                stripeEvent.Type ==
                    "checkout.session.completed" ||
                stripeEvent.Type ==
                    "checkout.session.async_payment_succeeded";

            if (!isSupportedEvent)
            {
                return Ok();
            }

            if (stripeEvent.Data.Object is not Session session)
            {
                _logger.LogWarning(
                    "Stripe event {EventId} did not contain " +
                    "a Checkout Session.",
                    stripeEvent.Id);

                return BadRequest(
                    "Checkout Session data is missing.");
            }

            /*
             * A completed Checkout Session is not always paid.
             * Process it only after Stripe reports payment_status=paid.
             */
            if (!string.Equals(
                    session.PaymentStatus,
                    "paid",
                    StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Stripe Checkout Session {SessionId} " +
                    "is not paid yet.",
                    session.Id);

                return Ok();
            }

            if (session.AmountTotal == null ||
                session.AmountTotal <= 0)
            {
                _logger.LogWarning(
                    "Stripe Checkout Session {SessionId} " +
                    "has an invalid amount.",
                    session.Id);

                return BadRequest(
                    "The payment amount is invalid.");
            }

            if (string.IsNullOrWhiteSpace(
                    session.Currency))
            {
                _logger.LogWarning(
                    "Stripe Checkout Session {SessionId} " +
                    "has no currency.",
                    session.Id);

                return BadRequest(
                    "The payment currency is missing.");
            }

            try
            {
                PaymentCompletionResult result =
                    await _paymentCompletionService
                        .CompleteStripePaymentAsync(
                            checkoutSessionId:
                                session.Id,

                            paymentIntentId:
                                session.PaymentIntentId,

                            stripeEventId:
                                stripeEvent.Id,

                            amountPaidInSen:
                                session.AmountTotal.Value,

                            currency:
                                session.Currency,

                            cancellationToken:
                                cancellationToken);

                _logger.LogInformation(
                    "Stripe event {EventId} processed. " +
                    "Payment {PaymentNumber}, " +
                    "Bill {BillNumber}, " +
                    "Already processed: {AlreadyProcessed}.",
                    stripeEvent.Id,
                    result.PaymentNumber,
                    result.BillNumber,
                    result.AlreadyProcessed);

                return Ok();
            }
            catch (InvalidOperationException exception)
            {
                /*
                 * Return 500 so Stripe can retry if the problem
                 * was caused by temporary application/database state.
                 */
                _logger.LogError(
                    exception,
                    "Stripe payment completion failed for " +
                    "event {EventId} and session {SessionId}.",
                    stripeEvent.Id,
                    session.Id);

                return StatusCode(
                    StatusCodes
                        .Status500InternalServerError);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Unexpected Stripe webhook error for " +
                    "event {EventId}.",
                    stripeEvent.Id);

                return StatusCode(
                    StatusCodes
                        .Status500InternalServerError);
            }
        }
    }
}