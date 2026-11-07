# Bus booking submissions

`POST /api/bus-bookings` accepts JSON from the eByahe checkout. It requires a valid
Keycloak **access token** in `Authorization: Bearer <token>`, with this service's
issuer and `get-it-done-ebyahe-api` audience. It uses the same customer authentication
as partner applications; no administrative role is required. Anonymous, expired,
forged, wrong-audience and invalid-subject tokens return **401** before persistence.

The authenticated token's `sub` is the owner. The request has no owner/user ID field;
unknown fields, including attempts to supply `createdBy`, are rejected.

## Request

```json
{
  "requestId": "9579e835-4bad-4b47-8bce-80387b7d0b20",
  "trip": {
    "tripId": "9e5d0a95-fbcb-4939-9ff3-f1d0856a1eb2",
    "origin": "Manila",
    "destination": "Baguio",
    "operator": "Victory Liner",
    "busClass": "Deluxe",
    "departureDate": "2026-12-01",
    "departureTime": "06:00",
    "travelTime": "6 hrs"
  },
  "booking": {
    "boarding": "Victory Liner Manila Bus Station",
    "dropping": "Victory Liner Baguio Bus Station",
    "passengers": [
      { "fullName": "Example Traveler", "age": null, "gender": "", "type": "adult", "request": "Window" }
    ],
    "contact": {
      "fullName": "Example Booker",
      "email": "booker@example.com",
      "phone": "09171234567",
      "emergencyPhone": "",
      "updates": true
    },
    "protection": false,
    "baggage": false
  },
  "payment": { "method": "cash", "provider": "Bus station" },
  "fare": {
    "fares": [{ "label": "Passenger 1 · Adult", "amount": 750 }],
    "protection": 0,
    "baggage": 0,
    "fee": 22.5,
    "total": 772.5
  }
}
```

There must be 1–20 passengers and one fare line for each. Age can be omitted or
`null`; if supplied it must be an integer from 0 to 120. Names, contact details,
route/date/time, passenger types, payment providers and fare arithmetic are
validated. The intake retains the checkout's illustrative add-on prices and 3%
fee. The trip ID, directional metadata, stations and passenger fares must match
the published database schedule from [the catalog API](bus-catalog.md). Use its
returned fields rather than the illustrative IDs/dates in this example. Accepted
details are saved as a snapshot, and one seat per passenger is reserved against
the selected scheduled-trip ID. No payment is processed or ticket issued.
The persisted status is **Submitted**.

`trip.tripId` must be a non-empty GUID returned by the catalog API. After the
GUID key upgrade, restart any checkout that still has a numeric trip ID with a
new search. Existing booking references remain valid. The migration patches
stored snapshot IDs while retaining the original request hashes and retry keys;
an edited payload must use a new request key rather than replaying an old one.

Allowed methods/providers: `card`/`Card`, `cash`/`Bus station`,
`wallet`/`GCash` or `Maya`, `bank`/`BDO`, `BPI` or `Metrobank`. Card submissions
carry only `cardholder`, `last4` and `expiry`. Full card numbers, CVVs and other
unknown payment fields are rejected. No card processing or money transfer occurs.

## Response and retries

First successful submission returns **201 Created**, with a `Location` pointing
to its receipt. Example response (IDs and UTC timestamp are server-generated):

```json
{
  "id": "bdb0f7c1-e361-4fa5-8efe-fb8041ad70a9",
  "reference": "BK-BDB0F7C1E3614FA58EFEFB8041AD70A9",
  "status": "Submitted",
  "createdAtUtc": "2026-11-06T03:00:00+00:00",
  "total": 772.5,
  "reservedSeats": 1
}
```

Keep the same `requestId` when retrying the same payload. Repeated or concurrent
identical submissions by the same owner return the same receipt (**200** on
replay) and create only one row. Reusing the key with different details returns
**409 Conflict**. Keys are scoped to the authenticated owner. A different booking
needs a new key. Validation failures return **400**, unsupported media **415**,
and requests over 64 KiB **413**.

Seat reservation and booking persistence commit together. Concurrent requests
cannot overbook the remaining count, and retries do not reserve again. Insufficient
seats or a changed/unavailable departure return **409** with `code: seats_unavailable`;
the checkout offers a new search. No seats are taken when persistence fails.
Inventory is scoped to the exact `ScheduledTripId`, including its departure date
and time, rather than a general route or bus. Trips with unconfigured inventory
cannot be reserved. See [bus-catalog.md](bus-catalog.md) for transaction details.

Receipts, summaries and details include `reservedSeats`. Earlier bookings default
to zero and are not retroactively allocated inventory. Specific seat numbers are
not assigned yet; passenger fare labels are not seat identifiers. Reservations
currently have no automatic expiry or cancellation/release endpoint.

`GET /api/bus-bookings?page=1&pageSize=10` reads the authenticated owner's bookings
from SQL Server, newest first. It returns `{ items, totalCount, page, pageSize }`.
Items contain the receipt fields plus `trip`, passenger count (`passengers`) and
the selected provider (`paymentMethod`). Page must be 1–1,000,000; page size 1–50.
Passenger names, contact details and payment metadata are excluded from summaries.

`GET /api/bus-bookings/{id}` and
`GET /api/bus-bookings/by-reference/{reference}` return the receipt fields plus
the stored `trip`, `booking`, `payment` and `fare` snapshot, only to its owner;
another owner receives **404**. All reads require authentication and use `private,
no-store`. The API never returns its request hash, request key or internal owner
field, and never accepts a client-selected owner for listing or reading bookings.

## Persistence and frontend

`AddBusBookings` creates the `BusBookings` table, storing the receipt, owner,
request key/hash and validated JSON snapshot. Startup applies the migration
using the existing migration pipeline. For managed deployments that disable
startup migration, apply the EF migration before starting the updated API.

The frontend posts to `${environment.ebyaheApiUrl}/bus-bookings` with a refreshed
bearer token. It shows success only after acceptance, uses the server reference,
blocks repeated clicks while submitting and retains the same key for unchanged
retries. Errors retain the checkout and display an actionable message.
My Bookings loads paginated database summaries through the authenticated list
endpoint. Existing `/booking-details?reference=...` links load the database snapshot
through the reference endpoint. Both pages handle loading, unavailable sessions,
request failures/retries and missing records. Results are cleared on logout or
account changes; late responses from an earlier session are ignored.

The UI no longer reads or writes booking history in localStorage. Earlier records
that exist only in browser storage are not imported into the database. Account
profile fields that were not part of a submitted snapshot are shown as unavailable;
the frontend does not substitute the user's current profile for historical data.

Run the SQL-backed API checks against disposable databases:

```powershell
$env:EBYAHE_TEST_SQL_SERVER = '.\SQLEXPRESS'
dotnet test EByahe.Tests --filter 'FullyQualifiedName~BusBooking'
```
