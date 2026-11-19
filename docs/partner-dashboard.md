# Partner dashboard

`GET /api/partner/dashboard/overview?period=week` returns the dashboard in one response. `period` accepts `week` (default) or `month`; other values return 400. The existing `/api/partner/dashboard` count endpoint remains available.

All queries use the signed-in subject's active memberships in approved partners. Query parameters cannot override the subject or partner scope. Section data requires the same read permissions as its corresponding table. Booking information requires `booking.read`, trip information requires `scheduled-trip.read`, terminals require `terminal.read`, bus activity requires `bus.read`, and user counts/activity require `member.create` or `member.manage`. Inaccessible sections return empty lists / zero aggregates and are hidden in the UI. Responses use `Cache-Control: no-store`.

## Reporting definitions

- Reporting periods cover the last 7 or 30 calendar dates including today, using Manila time (UTC+8). The previous period is the immediately preceding 7 or 30 dates. Today's figures are partial. `CreatedAtUtc` is stored in UTC; future-created bookings are excluded.
- **Total bookings:** bookings created in the selected period, including cancelled bookings.
- **Booking value:** totals of non-cancelled bookings created in the selected period. This includes unpaid submissions and is not collected revenue.
- **Scheduled trips:** non-cancelled departures in the selected period, based on departure time.
- **Seat occupancy:** reserved seats on Confirmed / Payment Success bookings for those trips, divided by the trips' total bus capacity. Trips with unconfigured inventory or zero capacity are excluded from this ratio. Empty capacity produces 0%. Unpaid submissions and failed payments do not reserve seats.
- **Booking statuses:** actual Submitted, Confirmed and Cancelled statuses of bookings created in the selected period.
- **Chart:** daily booking counts, zero-filled for every date. The 30-day view groups the daily values into five 6-day bars in the frontend.
- Comparisons use the prior period. A zero-to-positive baseline is shown as “New”; zero-to-zero is 0%. Occupancy comparisons use percentage points.

The next three departures are future, non-cancelled trips with active buses, routes, and terminals, ordered by departure time. The latest five bookings and five activities are independent of the selected reporting period. Activities come from booking status/payment logs, catalog audit logs, and persisted partner membership creation dates; older bookings without logs use their creation event. Catalog log snapshots, payment secrets, contact details, and complete passenger lists are not returned.

Terminal counts include all registered terminals; team size counts distinct active users across accessible partners. Record counts and aggregates are calculated in SQL; detailed lists are bounded before materialization. No migration or seed data is required.

The frontend has loading, error/retry, access-denied and empty states. Period changes and live record updates request new data without navigating the shell or reloading the iframe. Session/permission changes cancel requests and clear previous data. The sample badge, design-preview footer and header “View bookings” button have been removed.
