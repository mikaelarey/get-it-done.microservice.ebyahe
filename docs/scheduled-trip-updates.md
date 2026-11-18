# Scheduled trip details and updates

The partner Scheduled Trips table has an Actions column with View and Update;
the Partner column is removed. View opens a read-only modal showing the partner,
boarding/destination terminals and locations, departure in Manila time, travel
time, fleet/plate, class, capacity, available seats, fare and status. Update reuses
the creation form and reloads the saved record and current active catalog options.

`GET /api/partner/scheduled-trips/{id}` requires `scheduled-trip.read` and current
active membership in the trip's approved bus-operator partner. Missing or foreign
records return 404. `PUT` at the same URL additionally requires
`scheduled-trip.write`. The body accepts busId, originTerminalId,
destinationTerminalId, departureAtUtc, durationMinutes and baseFare. The partner is
derived from the saved trip and cannot be transferred. Seat counts and status are
not editable in this form. No database migration is needed.

Only future Scheduled trips without non-cancelled bookings and with their full,
configured seat inventory can be edited. This protects pending checkouts, failed
payments that could be retried, and confirmed bookings. The GET response includes
`canUpdate` and `updateRestriction` for the form; PUT checks again transactionally
and returns 409 if the trip becomes protected while the form is open.

Updates validate active buses and terminals under the same partner, future UTC
departure, positive fare with at most two decimals and existing route duration.
The existing trip is excluded from the bus overlap check. Changing direction
reuses or creates a route without altering a shared route. An unbooked trip's
available seats follow the newly selected bus's capacity.

Successful updates append ScheduledTripLogs with before/after JSON and the
authenticated creator, queue a `scheduled-trip.updated` activity for the selected
partner's active members, and publish the existing scheduled-trips live change.
The trip, route, audit and outbox save atomically. The notification consumer
normalizes the action to `Updated a scheduled trip.` and does not default to
administrator recipients. The UI refreshes after success, restores trigger focus
on close and cancels requests on account changes or permission revocation.
