# Cash booking deadline

Cash booking requires departure to be at least 24 hours away at submission. The API checks the current database departure under the booking transaction, so changing the browser clock or bypassing the payment UI cannot bypass this rule. Identical request retries still return the original booking.

The deadline is the original booking creation time plus 24 hours, in UTC. At the deadline, payment is no longer accepted. Cash bookings do not hold seats. A successful on-time cash receipt continues to confirm the booking and reserve seats through the existing payment event.

Each cash booking transaction writes a `booking.created` registration into its SQL outbox for `get-it-done.ebyahe.cash-bookings.v1`, keyed by booking ID. Its payload contains only the booking ID/reference, owner ID, partner ID, amount/currency, creation time and fixed deadline. The normal notification event remains on its own topic. A one-minute background repair pass registers legacy unpaid cash bookings using their original creation time; retries never extend their deadline.

The payment service consumes registrations into `CashBookingDeadlines`. Its expiry worker scans every 60 seconds and takes the same transaction-owned per-booking lock as cash acceptance. It checks the actual receipt table before marking an obligation cancelled and queuing `payment.cancelled` in the same transaction. A cash receipt saved before the deadline remains paid even if Kafka confirmation is delayed. Expired obligations reject cash acceptance, including requests with stale summaries. Neither registration, expiry nor cash acceptance calls PayMongo.

The existing EByahe payment consumer applies cash expiry events after validating the owner, partner, amount, payment method and elapsed deadline. It marks booking and payment status `Cancelled`, retains the booking, and writes this immutable history remark:

> Automatically cancelled: cash payment was not received within 24 hours of booking.

The cancellation reason appears on customer and partner booking details. The partner cash acceptance card and saved booking page show the payment deadline in Manila time. The payment form disables cash inside 24 hours and updates at the cutoff without polling a server.

## Deployment

1. Import the added Kafka topic from the environment's manifest in `get-it-done.kafka`. Allow the EByahe producer to write and the payment consumer group to read it. Keep the existing payment completion topic and EByahe consumer enabled.
2. Apply the payment service's `AddCashBookingDeadlines` migration and deploy both APIs and frontends. No EByahe schema migration is needed.
3. Keep `Kafka:Enabled=true` in both services and `Kafka:ConsumerEnabled=true` in payment. `Kafka:CashBookingsTopic` defaults to `get-it-done.ebyahe.cash-bookings.v1` in both services. Payment `Kafka:CashExpiryIntervalSeconds` defaults to 60 (allowed range 1–300).

Kafka or database downtime delays cancellation projection; SQL outboxes and idempotent registrations resume it after recovery. Payment acceptance still checks the trusted deadline synchronously. The one-minute scan adds up to about one minute plus event delivery time to the displayed cancellation status.
