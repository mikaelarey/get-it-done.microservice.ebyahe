# Admin partner directory

`GET /api/admin/partners?page=1&pageSize=5` reads the `Partners` table for the
admin shell's `/partners-lists` page. It requires both exact client roles:
`get-it-done-web/admin` and `get-it-done-ebyahe-api/partner.review`, using the
same reviewer policy as the cross-applicant Applications workspace. Missing or
invalid access tokens return 401; missing permissions return 403. Responses use
`Cache-Control: no-store`.

Optional query parameters are `search` (trimmed, up to 254 characters), `category`
and `status` (up to 32 characters each). `page` is 1–1,000,000; `pageSize` is 1–50.
Invalid queries return 400. Filtering, counts and pagination run in SQL. Results
sort by approval time descending, then current partner display name and GUID.

The response contains `items`, `totalCount`, `page`, `pageSize`, global `summary`
(`totalPartners`, `approved`, `suspended`), and database-derived category/status
filter options. The summary and options remain global while filters apply to
the results and `totalCount`.

Each item contains its partner GUID, code, current display name and saved status.
Category, address and representative name/email come from the linked application.
`joinedAtUtc` is the latest Approved entry in `PartnerApplicationApprovals`,
rather than submission time. No pending or declined application becomes a partner
through a directory read; approving an application creates its partner through
the existing approval workflow and it appears on the next list request.

Existing seeded/legacy partners without an application are preserved by left
joins. Their category is `bus-operator` when they have a saved bus, location is
the alphabetically first active terminal's location, and contact name/email come
from the exact owner's `RefUsers` record when available. Fields with no saved
source, including their join date, are null and display as "Not recorded".
The existing demo catalog rows are database records and are included; no browser
sample array is used. There is no new migration or seed operation for this API.

Bank accounts, tax identifiers, documents, storage keys and full applications
are excluded from the projection. All requests carry the refreshed eByahe access
token. The shell, route guard and page require reviewer access; revocation clears
loaded records and cancels pending requests. Errors show a retry action with no
sample-data fallback.

## Partner details

Each partner row's **View Details** button opens `/partner-details/{partnerId}`.
The separate admin page reads `GET /api/admin/partners/{id}` with the same
authenticated reviewer policy and `no-store` caching. It uses the partner GUID,
not an application GUID; unknown or malformed IDs return 404.

The response contains `partner` (the directory fields), nullable `applicationId`
and `applicationTrackingId`, nullable `owner` (`fullName`, `email` from the exact
owner's local RefUser), plus `busCount`, `terminalCount`, `activeUserCount` and `bookingCount`.
Fleet and terminal counts include all configured records for that partner;
user count includes only active `PartnerUsers` memberships. Other partners' data
does not affect these counts. These projections exclude bank/tax/document data.

`bookingCount` counts bus booking records linked through `ScheduledTripId` to this
partner's scheduled trips, across dates and bookers. It counts bookings rather
than passenger seats, including historical departures. Older bookings without a
scheduled trip cannot be attributed and are excluded. The Bookings summary card
uses this database count alongside fleet, terminal and active-user statistics.

The page shows business information, representative and owner contacts, and
operation counts. When an application is linked, **View application** opens the
existing reviewer details page. Legacy partners retain nullable fields and have
no application link. The admin shell supports direct partner-details URLs,
navigation from the list, the back link, and its guarded session channel.
Route/identity changes cancel old requests; losing reviewer access clears data.
Unavailable records and transient failures have distinct not-found/retry states.
No schema change is required.

## Tile record modals

The Buses, Bus terminals, Bookings and Active users tiles are keyboard-accessible
buttons, including tiles with zero records. Each opens a native modal dialog
with the partner name and a paginated database table. Escape, Close or clicking
the backdrop dismisses the modal and restores focus to the tile. Pending requests
are canceled when closing, changing partners or losing reviewer access.

The same reviewer policy and `no-store` headers protect these GET routes:

| Route | Records |
| --- | --- |
| `/api/admin/partners/{id}/buses` | Fleet number, plate, class, seat capacity and active state; includes inactive fleet records |
| `/api/admin/partners/{id}/terminals` | Terminal name, location, address and active state; includes inactive terminals |
| `/api/admin/partners/{id}/bookings` | Reference, route, Manila departure, plate, reserved seats, total and status; scoped through scheduled-trip ownership |
| `/api/admin/partners/{id}/users` | Name, email and membership creation time for active PartnerUsers memberships only |

All four accept `page` (1–1,000,000, default 1) and `pageSize` (1–50, default 10).
Responses contain `items`, `totalCount`, `page` and `pageSize`, with deterministic
ordering and SQL pagination. A partner with no records returns an empty page;
an unknown partner returns 404. Invalid paging returns 400, invalid/missing
tokens 401, and missing reviewer roles 403. Caller-supplied partner IDs cannot
broaden a collection beyond its saved foreign-key relationships. Booking intake
snapshots, contacts, payment details and user credentials are excluded.
