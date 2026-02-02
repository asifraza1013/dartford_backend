# Stripe Integration Implementation Plan
## Replacing TrueLayer with Stripe for UK (GBP) Payments

**Date:** 2026-02-02
**Status:** Ready for Implementation
**Approach:** Server-side charging with saved Stripe Payment Methods
**Migration Strategy:** Immediate switch to Stripe for all new GBP payments

---

## Executive Summary

This plan outlines the implementation of Stripe as the primary payment gateway for UK (GBP) clients, replacing TrueLayer. The implementation will:

1. ✅ Enable recurring/autopay functionality for UK clients (currently not possible with TrueLayer)
2. ✅ Store customer cards securely on Stripe (PCI compliant, not on our servers)
3. ✅ Support milestone-based automatic charging via background service
4. ✅ Maintain existing architecture patterns (IPaymentGateway interface)
5. ✅ Keep TrueLayer code for historical reference only
6. ✅ Support 3D Secure (SCA) compliance for UK/EU regulations

---

## Current System Overview

### Payment Gateway Routing
- **GBP (British Pounds):** TrueLayer → Open Banking (NO recurring payments)
- **NGN (Nigerian Naira):** Paystack → Card payments (WITH recurring payments)

### Problems with TrueLayer
1. **No recurring payment support** - Open Banking requires SCA for each transaction
2. **Cannot save cards** - No authorization codes or payment methods
3. **UK clients cannot use autopay** - Manual payment required for each milestone
4. **Poor user experience** - Bank login required every time

---

## Implementation Architecture

### Backend Components

#### 1. Stripe Gateway Service (`/Services/Payment/StripeGateway.cs`)

**Implements:** `IPaymentGateway` interface

**Key Methods:**

```csharp
// Payment Initiation
Task<PaymentInitiationResponse> InitiatePaymentAsync(
    PaymentInitiationRequest request)
// Creates PaymentIntent with automatic_payment_methods
// Returns Stripe Checkout session URL or client_secret for Stripe Elements

// Payment Verification
Task<PaymentVerificationResponse> VerifyPaymentAsync(
    string paymentReference)
// Retrieves PaymentIntent and confirms status

// Webhook Processing
Task<WebhookProcessingResponse> ProcessWebhookAsync(
    string payload, string signature)
// Handles: payment_intent.succeeded, payment_intent.failed,
//         payment_method.attached, setup_intent.succeeded

// Recurring Charges
Task<RecurringChargeResponse> ChargeAuthorizationAsync(
    RecurringChargeRequest request)
// Charges saved PaymentMethod for milestone autopay

// Card Saving
Task<SetupIntentResponse> CreateSetupIntentAsync(int userId)
// Creates SetupIntent for card saving without payment
```

**Additional Helper Methods:**
- `GetOrCreateStripeCustomer()` - Customer management
- `AttachPaymentMethodToCustomer()` - Link card to customer
- `ListCustomerPaymentMethods()` - Retrieve saved cards
- `DetachPaymentMethod()` - Remove saved card
- `CalculatePlatformFee()` - Fee calculation
- `HandleSuccessfulPayment()` - Post-payment processing

#### 2. Configuration (`/Models/PaymentConfig.cs`)

```csharp
public class StripeConfig
{
    public string SecretKey { get; set; }
    public string PublishableKey { get; set; }
    public string WebhookSecret { get; set; }
    public bool Enabled { get; set; } = true;
    public List<string> AllowedCurrencies { get; set; } = new() { "GBP" };
}
```

**appsettings.json:**
```json
{
  "Stripe": {
    "SecretKey": "sk_test_...",
    "PublishableKey": "pk_test_...",
    "WebhookSecret": "whsec_...",
    "Enabled": true,
    "AllowedCurrencies": ["GBP"]
  },
  "TrueLayer": {
    "Enabled": false  // Disabled for new payments
  }
}
```

#### 3. Update PaymentGatewayFactory (`/Services/Payment/PaymentGatewayFactory.cs`)

**Current Logic:**
```csharp
if (currency == "GBP") return _trueLayerGateway;  // OLD
if (currency == "NGN") return _paystackGateway;
```

**New Logic:**
```csharp
if (currency == "GBP") return _stripeGateway;     // NEW
if (currency == "NGN") return _paystackGateway;
```

#### 4. Update Background Service (`/Services/Payment/MilestonePaymentBackgroundService.cs`)

**Current Behavior:**
- UK brands: Send reminder emails only (cannot charge)
- Nigerian brands: Charge saved Paystack cards

**New Behavior:**
- UK brands: Charge saved Stripe Payment Methods ✅
- Nigerian brands: Charge saved Paystack cards (no change)

**Implementation:**
```csharp
if (brand.Location == "UK" || brand.Currency == "GBP")
{
    // NEW: Charge with Stripe
    var stripeGateway = _gatewayFactory.GetGateway("stripe");
    await _paymentOrchestrator.ChargeRecurringPaymentAsync(
        campaignId, milestoneId, savedPaymentMethod);
}
else
{
    // Existing Paystack logic
    var paystackGateway = _gatewayFactory.GetGateway("paystack");
    await _paymentOrchestrator.ChargeRecurringPaymentAsync(
        campaignId, milestoneId, savedPaymentMethod);
}
```

#### 5. Webhook Handler (`/Controllers/PaymentModuleController.cs`)

**New Endpoint:**
```csharp
[HttpPost("webhook/stripe")]
public async Task<IActionResult> StripeWebhook()
{
    var json = await new StreamReader(HttpContext.Request.Body)
        .ReadToEndAsync();
    var signature = Request.Headers["Stripe-Signature"];

    try
    {
        var stripeEvent = EventUtility.ConstructEvent(
            json, signature, _webhookSecret);

        var response = await _paymentOrchestrator
            .ProcessWebhookAsync("stripe", json, signature);

        return Ok();
    }
    catch (StripeException e)
    {
        return BadRequest();
    }
}
```

**Webhook Events to Handle:**
- `payment_intent.succeeded` - Payment completed
- `payment_intent.payment_failed` - Payment failed
- `payment_intent.requires_action` - 3D Secure required
- `payment_method.attached` - Card saved to customer
- `setup_intent.succeeded` - Card setup completed
- `charge.refunded` - Refund processed

#### 6. Payment Method Model Updates (`/Models/PaymentMethod.cs`)

**Add Fields:**
```csharp
public string? StripePaymentMethodId { get; set; }  // pm_xxxxx
public string? StripeCustomerId { get; set; }        // cus_xxxxx
```

**Updated Gateway Support:**
- `paystack` - AuthorizationCode
- `stripe` - StripePaymentMethodId + StripeCustomerId
- `truelayer` - Not supported (historical only)

---

### Frontend Components

#### 1. Stripe.js Integration

**Install Package:**
```bash
npm install @stripe/stripe-js @stripe/react-stripe-js
```

**Initialize Stripe:**
```typescript
// src/lib/stripe.ts
import { loadStripe } from '@stripe/stripe-js';

export const stripePromise = loadStripe(
  process.env.REACT_APP_STRIPE_PUBLISHABLE_KEY!
);
```

#### 2. Stripe Card Input Component

**New Component:** `/src/components/Payment/StripeCardInput.tsx`

```typescript
import { CardElement, useStripe, useElements } from '@stripe/react-stripe-js';

export const StripeCardInput = ({
  onSuccess,
  onError,
  saveCard = false
}) => {
  const stripe = useStripe();
  const elements = useElements();

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!stripe || !elements) return;

    const cardElement = elements.getElement(CardElement);

    // Confirm payment or setup
    const { error, paymentIntent } = await stripe
      .confirmCardPayment(clientSecret, {
        payment_method: { card: cardElement }
      });

    if (error) {
      onError(error.message);
    } else {
      onSuccess(paymentIntent);
    }
  };

  return (
    <form onSubmit={handleSubmit}>
      <CardElement options={CARD_ELEMENT_OPTIONS} />
      {saveCard && (
        <label>
          <input type="checkbox" />
          Save card for future payments
        </label>
      )}
      <button type="submit">Pay Now</button>
    </form>
  );
};
```

#### 3. Update PaymentGatewaySelector Component

**Current Options:**
- TrueLayer (Bank Transfer)
- Paystack (Card Payment)

**Updated Options:**
- **Stripe** (Card Payment) - For GBP ✅
- Paystack (Card Payment) - For NGN (no change)

**Conditional Rendering:**
```typescript
{currency === 'GBP' && (
  <GatewayOption
    name="stripe"
    label="Card Payment"
    icon={<CreditCardIcon />}
    description="Pay securely with Stripe"
    tags={["Instant", "Save card for autopay"]}
  />
)}

{currency === 'NGN' && (
  <GatewayOption
    name="paystack"
    label="Card Payment"
    icon={<CreditCardIcon />}
    description="Pay securely with Paystack"
  />
)}
```

#### 4. Update Payment Flow

**Current Flow:**
```
Select Gateway → Redirect to TrueLayer/Paystack → Callback
```

**New Flow for Stripe:**
```
Select Stripe → Show Stripe Card Input → Confirm Payment → Success
```

**Implementation Options:**

**Option A: Stripe Checkout (Redirect - Simpler)**
- Backend creates Checkout Session
- Redirect to Stripe-hosted page
- Similar to current TrueLayer flow
- ✅ Easier to implement
- ✅ Stripe handles 3D Secure
- ❌ Less customization

**Option B: Stripe Elements (Embedded - Better UX)**
- Embed card input in your page
- Backend creates PaymentIntent
- Frontend confirms payment
- ✅ Better user experience
- ✅ More control over UI
- ❌ More complex implementation

**Recommendation:** Start with Stripe Checkout (Option A), migrate to Elements (Option B) later.

#### 5. Update Payment API (`/src/app/apis/paymentApi.ts`)

**Add Endpoints:**
```typescript
createSetupIntent: builder.mutation<
  { clientSecret: string },
  void
>({
  query: () => ({
    url: '/payment/setup-intent',
    method: 'POST',
  }),
}),

getStripePublishableKey: builder.query<
  { publishableKey: string },
  void
>({
  query: () => '/payment/stripe-config',
}),
```

#### 6. Update PaymentCallback Page

**Add Stripe Success Handling:**
```typescript
// Check for Stripe parameters
const payment_intent = searchParams.get('payment_intent');
const payment_intent_client_secret = searchParams.get(
  'payment_intent_client_secret'
);

if (payment_intent) {
  // Verify Stripe payment
  await verifyPayment(payment_intent);
}
```

---

## Database Changes

### Migration: AddStripePaymentMethodFields

```csharp
public partial class AddStripePaymentMethodFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "StripePaymentMethodId",
            table: "PaymentMethods",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "StripeCustomerId",
            table: "PaymentMethods",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_PaymentMethods_StripePaymentMethodId",
            table: "PaymentMethods",
            column: "StripePaymentMethodId");

        migrationBuilder.CreateIndex(
            name: "IX_PaymentMethods_StripeCustomerId",
            table: "PaymentMethods",
            column: "StripeCustomerId");
    }
}
```

### User Model Extension

**Add Field:**
```csharp
public string? StripeCustomerId { get; set; }  // cus_xxxxx
```

This ensures one Stripe customer per user for better management.

---

## Implementation Phases

### Phase 1: Backend Foundation (2-3 days)
- [ ] Add Stripe configuration to appsettings.json
- [ ] Create StripeGateway service implementing IPaymentGateway
- [ ] Implement payment initiation (PaymentIntent creation)
- [ ] Implement payment verification
- [ ] Add webhook endpoint and event handling
- [ ] Update PaymentGatewayFactory routing
- [ ] Add database migration for Stripe fields
- [ ] Write unit tests for StripeGateway

### Phase 2: Card Saving & Recurring (2 days)
- [ ] Implement SetupIntent for card saving
- [ ] Implement Stripe Customer creation/management
- [ ] Implement recurring charge with saved PaymentMethod
- [ ] Update MilestonePaymentBackgroundService for Stripe
- [ ] Test autopay flow end-to-end

### Phase 3: Frontend Integration (2-3 days)
- [ ] Install @stripe/stripe-js packages
- [ ] Create StripeCardInput component
- [ ] Update PaymentGatewaySelector (show Stripe for GBP)
- [ ] Implement Stripe Checkout flow
- [ ] Update PaymentCallback for Stripe
- [ ] Add Stripe config endpoint
- [ ] Test payment flow in UI

### Phase 4: Testing & Deployment (2 days)
- [ ] Test one-time payments (GBP)
- [ ] Test card saving during payment
- [ ] Test recurring charges via background service
- [ ] Test 3D Secure flow
- [ ] Test webhook reliability
- [ ] Test error handling (declined cards, etc.)
- [ ] Deploy to staging
- [ ] Deploy to production

### Phase 5: Migration & Monitoring (1 day)
- [ ] Disable TrueLayer for new payments
- [ ] Monitor Stripe dashboard for issues
- [ ] Set up Stripe webhook monitoring
- [ ] Update documentation
- [ ] Train support team

**Total Estimated Time:** 9-11 days

---

## Testing Checklist

### Backend Tests
- [ ] Payment initiation creates PaymentIntent correctly
- [ ] Webhook signature validation works
- [ ] Payment verification returns correct status
- [ ] Card saving creates Stripe Customer and PaymentMethod
- [ ] Recurring charge works with saved card
- [ ] Platform fee calculation is correct
- [ ] Failed payment handling works
- [ ] Refund processing works

### Frontend Tests
- [ ] Stripe Elements loads correctly
- [ ] Card input validation works
- [ ] 3D Secure modal appears when required
- [ ] Payment success callback works
- [ ] Payment failure shows error message
- [ ] Saved cards display correctly
- [ ] Auto-pay toggle works for GBP campaigns

### Integration Tests
- [ ] Full payment flow (one-time)
- [ ] Milestone payment flow
- [ ] Recurring payment via background service
- [ ] Card management (add, set default, remove)
- [ ] Webhook → Database update flow
- [ ] Invoice generation after payment
- [ ] Auto-withdrawal to influencer after payment

### Security Tests
- [ ] Webhook signature validation cannot be bypassed
- [ ] API endpoints require authentication
- [ ] Card details never stored locally
- [ ] PCI compliance maintained
- [ ] 3D Secure (SCA) properly implemented

---

## Rollback Plan

If issues occur:

1. **Immediate:** Set `Stripe.Enabled = false` in appsettings.json
2. **Fallback:** Re-enable TrueLayer temporarily
3. **Communication:** Notify UK brands of temporary payment method change
4. **Investigation:** Check Stripe dashboard, logs, webhook events
5. **Resolution:** Fix issues in staging before re-enabling

---

## Monitoring & Alerts

### Stripe Dashboard Monitoring
- Payment success rate
- Declined payment reasons
- Webhook delivery failures
- Dispute/chargeback notifications

### Application Monitoring
- Failed webhook processing logs
- Background service charge failures
- Card saving success rate
- 3D Secure completion rate

### Alerts Setup
- Email notification for webhook failures
- Slack alert for high payment failure rate
- Daily summary of Stripe transactions

---

## Cost Comparison

### TrueLayer
- Open Banking: £0.30 per transaction
- No recurring payment support
- Manual intervention required

### Stripe
- UK/EU cards: 1.5% + £0.20 per transaction
- Recurring payments: Same rate
- 3D Secure included
- Better success rates (retries, recovery)

**Break-even analysis:** Stripe is cost-effective due to:
1. Reduced manual processing overhead
2. Better conversion rates
3. Automatic retries for failed payments
4. Lower support costs

---

## Documentation Updates

- [ ] Update API documentation for new Stripe endpoints
- [ ] Add Stripe integration guide to README
- [ ] Document webhook setup process
- [ ] Add Stripe testing guide (test cards)
- [ ] Update brand payment guide with Stripe info

---

## Success Criteria

✅ **Functional:**
- UK brands can make one-time payments with Stripe
- UK brands can save cards for autopay
- Background service charges saved cards on milestone due dates
- Webhooks process reliably
- 3D Secure works correctly

✅ **Performance:**
- Payment initiation < 2 seconds
- Webhook processing < 1 second
- 95%+ payment success rate

✅ **User Experience:**
- Simple card input UI
- Clear error messages
- Successful autopay notifications

✅ **Business:**
- Reduced payment failures
- Increased autopay adoption
- Reduced support tickets for UK payments

---

## Risk Assessment

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Stripe API downtime | High | Low | Implement retry logic, queue webhooks |
| 3D Secure failures | Medium | Medium | Clear user guidance, support documentation |
| Webhook delivery failures | High | Low | Implement webhook retry queue |
| Card decline rate | Medium | Medium | Smart retry logic, email reminders |
| Integration bugs | High | Medium | Comprehensive testing, gradual rollout |

---

## Questions to Resolve Before Implementation

1. ✅ **Approach:** Server-side charging with saved cards (CONFIRMED)
2. ✅ **Migration:** Immediate switch to Stripe (CONFIRMED)
3. ❓ **UI Preference:** Stripe Checkout (redirect) or Stripe Elements (embedded)?
4. ❓ **Stripe Account:** Do you have a Stripe account set up? Need API keys?
5. ❓ **Testing:** Do you want to test in Stripe Test Mode first?
6. ❓ **Webhooks:** Do you have access to expose webhook endpoint (ngrok/cloudflare tunnel for local testing)?

---

## Next Steps

Once approved:

1. **Setup:** Create/configure Stripe account, get API keys
2. **Backend:** Implement StripeGateway service (Phase 1)
3. **Database:** Run migration for Stripe fields
4. **Frontend:** Integrate Stripe.js and card input (Phase 3)
5. **Testing:** Full end-to-end testing (Phase 4)
6. **Deployment:** Deploy to production (Phase 5)

---

## Notes

- Keep TrueLayer code for historical transactions/reference
- Existing TrueLayer transactions remain queryable
- Stripe integration follows same patterns as Paystack
- PCI compliance maintained (cards stored on Stripe)
- Meets SCA/3D Secure requirements for UK/EU

---

**Ready to proceed with implementation? Please review and approve this plan.**
