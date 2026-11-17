# PayMongo payment confirmation

The sibling `get-it-done.microservice.payment` backend owns PayMongo Checkout Sessions,
backend PayMongo configuration, signed webhook validation, and the payment outbox.
EByahe owns booking prices, customer ownership, seat reservations, and final booking confirmation.

`GET /api/bus-bookings/{id}/payment-summary` accepts the existing customer access token,
enforces ownership, and returns booking ID/reference/status/total, payment type/status,
passenger count, available seats and reserved seats.
No passenger, contact, or card details are returned by this endpoint.
For new online bookings, send `payment: { method: "paymongo", provider: "PayMongo", type: "GCash" }`.
Other types are `Maya` and `Credit/Debit Card`. Cash uses `type: "Cash"`.

The existing Kafka worker also subscribes to `Kafka:PaymentCompletedTopic`
(default `get-it-done.payment.completed.v1`). The producer is the Payment service;
the key is the booking ID. The `payment.completed` v1 envelope contains event ID,
occurred-at timestamp, local payment ID, booking ID/reference, owner subject, integer
amount in centavos, PHP currency, provider, provider session ID, and provider payment ID.
Do not put card data, keys, or passenger details into this event.

The consumer validates the envelope and atomically locks the inbox record and booking,
checks ownership/reference/amount/currency/type, and locks the scheduled departure.
Only verified `Payment Success` with sufficient seats changes the booking to `Confirmed`
and decrements inventory by the passenger count. These changes, the status log and the
processed inbox record commit together before the Kafka offset is acknowledged.
SQL uniqueness prevents reusing a payment identity for another booking. Replays are idempotent;
they never decrement seat availability again. Failed validation leaves the booking unchanged
and the inbox record failed without acknowledging its offset.

Apply `AddBookingPaymentStatusesAndHistory` after `AddBookingPaymentConfirmation`,
then `RenamePaymentSetupTablesToCodeDecode`. The latter renames existing tables
without deleting their data and updates the primary/foreign key names.
The resulting setup tables are `CodeDecodeBookingStatuses` (Submitted, Confirmed, Cancelled), `CodeDecodePaymentStatuses`
(Pending Payment, Payment Failed, Payment Success, Cancelled), and `CodeDecodePaymentTypes`
(GCash, Maya, Credit/Debit Card, Cash). Booking columns reference these setup tables.
`BusBookingStatusLogs` is append-only and records previous/new booking and payment
statuses, payment type, event ID, UTC timestamp and remarks. Owners can read it at
`GET /api/bus-bookings/{id}/status-history`.

The migration releases old unpaid reservations and records that correction, preserving
confirmed paid reservations. Historical PayMongo types remain null when the request did
not identify the selected method; new payments fill them from the verified payment.
No submission, including cash, reserves seats. New bookings start `Submitted` / `Pending Payment`.
Browser redirects and customer-supplied statuses cannot confirm a booking.

Provision the topic for all environments using the Kafka repository's updated manifests.
For secure brokers, grant only the Payment principal Write permission and the EByahe
principal Read permission on this topic and its existing consumer group.

The topic also carries `payment.failed` and `payment.cancelled` with explicit payment
status/type fields. Failures keep the booking Submitted with zero seats. Success may
follow a failed attempt; a delayed failure cannot undo success. Cancellation sets both
statuses to Cancelled and records the unavailable-seat reason. Cancelled bookings cannot pay again.

Before opening or reopening PayMongo, Payment rechecks available seats. If unavailable,
it expires any existing provider checkout before cancelling. Provider rejection because
payment is paid/processing blocks cancellation until reconciliation; it never reports a
processing payment as cancelled. The payment MFE shows an unavailable-seat modal.

Seats remain available during checkout. If concurrent payments exhaust capacity before
a successful payment is consumed, EByahe records Payment Success but leaves Submitted,
reserves zero seats, sets `PaymentReviewRequired`, and logs the need for manual refund
or rebooking. It never overbooks or misreports received funds as failed.
Refunds, settlement and ticket issuance remain separate workflows.
