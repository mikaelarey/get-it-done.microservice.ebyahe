# Bus catalog

The From/Destination controls, search results and checkout trip now read SQL Server
through the public catalog API. Booking creation and history still require the
authenticated customer's access token.

| Table | Purpose |
| --- | --- |
| `Partners` | Approved operating partners, optional application link and owner subject |
| `PartnerUsers` | Many-to-many membership links from partners to local `RefUsers` |
| `Locations` | Shared cities/search locations |
| `BusTerminals` | Partner-owned terminals at a location |
| `Buses` | Partner-owned fleet, unique plate number, bus class and passenger capacity |
| `BusRoutes` | Directional terminal pair and journey duration |
| `ScheduledTrips` | Bus assigned to a route, UTC departure, base fare, remaining seats and status |

Composite foreign keys enforce that a route's terminals and a scheduled trip's bus
belong to its partner. Reverse directions are separate routes and schedules.
`BusBookings.ScheduledTripId` links new submissions to their schedule. It remains
nullable for older submissions; their saved JSON details continue to work.

The future workflow can create a Partner linked to an approved PartnerApplication,
then provide authenticated owner-managed terminals, buses, routes and schedules.
Those management/approval screens and write endpoints are not implemented here.
Submitting an application alone does not publish a partner or trips.
See [partner memberships](partner-users.md) for the user relationship to populate
when an application is approved.

## Endpoints

- `GET /api/bus-catalog/locations`: origins with upcoming published schedules.
- `GET /api/bus-catalog/destinations?originId={originGuid}`: destinations served from that origin.
- `GET /api/bus-catalog/trips?originId={originGuid}&destinationId={destinationGuid}&departure=2026-11-07&passengers=2`
- `GET /api/bus-catalog/trips/{id}`: current published trip for checkout.

Use the returned location and trip IDs rather than assuming fixed seed IDs. Search
matches the exact date in Asia/Manila, excludes departed/cancelled trips, suspended
partners and inactive buses/routes/terminals/locations, and checks bus capacity.
Responses include terminal names, bus class, fare, local departure date/time,
duration, plate number, capacity and `isDemo`. Search returns at most 100 trips, ordered by departure.

Location and trip IDs are non-empty GUID strings in standard hyphenated format.
Numeric IDs are no longer accepted. The frontend keeps these IDs as strings
through search, checkout and booking submission.

## GUID key upgrade

`ConvertApplicationKeysToGuids` converts the six catalog tables' numeric primary
keys and all dependent foreign keys to `uniqueidentifier`. It gives `RefUsers` a
local GUID primary key and preserves its former Account ID in the unique
`ExternalUserId` column. Other application primary keys were already GUIDs.
External identity subjects and EF's migration-history identifiers retain their
required string formats.

The migration generates one GUID per existing catalog row, maps dependent keys,
patches saved booking JSON trip IDs and rebuilds the affected keys, indexes and
constraints inside the migration transaction. It verifies row counts and total
available/reserved seats. Booking IDs, references, existing reservations, fares
and departure times remain unchanged. Historical submissions without a schedule
foreign key remain unlinked and do not acquire reservations; their snapshot ID
uses an exact matching departure where available, otherwise a historical GUID.

Back up and verify the database before applying this version, stop the previous
API, apply the migration, and deploy the matching frontend/API together. Existing
numeric checkout URLs or in-progress checkouts require a fresh search; saved
booking-reference links continue to work. Original request hashes are retained as
an audit of the original payload, so a changed payload must not reuse its retry key.
The conversion is forward-only: restoring the verified backup and the matching
application version is required to return to numeric keys. `Down` intentionally
rejects a lossy conversion. Development seeding matches existing records by their
codes/relationships and preserves their inventory after conversion.

`AddBusPlateNumbers` adds nullable `Buses.PlateNumber` with a filtered unique index.
The development seeder fills missing sample plates (`SMP-1001` through `SMP-1066`)
without changing plates already assigned. Non-demo buses with no plate remain null.
New booking snapshots capture the catalog plate at submission time, so later fleet
changes do not rewrite earlier booking details. Old bookings show "Not recorded".

`POST /api/bus-bookings` validates trip metadata, terminals and per-passenger fares
against the current database schedule. Invalid submitted metadata/fares return
400; departed, changed or insufficient-seat trips return 409 with `code: seats_unavailable`.
Accepted identical retries replay the saved receipt,
even if the schedule was subsequently cancelled. Earlier booking snapshots are
not rewritten when catalog data changes.

`AddTripAvailableSeats` adds nullable `ScheduledTrips.AvailableSeats`. Availability
is a separate per-departure count, not a label for bus capacity. Local sample trips
start with the assigned bus's capacity; repeated seeding preserves remaining counts
and never refills missing inventory on a trip that already has reservations.
Trips with zero or unknown inventory are unavailable for new reservations.
Search and submission check both capacity and remaining seats. Counts returned by
the API never exceed bus capacity.

`ReserveBookingSeats` adds `BusBookings.ReservedSeats`. Submission reserves one seat
per passenger against the selected `ScheduledTripId`, decrements that departure's
available seats, and saves the booking in one database transaction. A per-request
transaction lock serializes retries, and an update lock on the scheduled trip
serializes seat allocation. The catalog is checked again under the transaction;
failed saves roll back the inventory change. Identical retries, including after
sellout or cancellation of the schedule, return the original receipt without
another decrement. Other departures keep their own inventory.

Each scheduled trip is one bus/route/departure date and time. The unique database
index on `(BusId, DepartureAtUtc)` prevents assigning a bus twice at the exact same
instant. Different buses can have the same departure time and different trip IDs.

Existing bookings retain `ReservedSeats = 0`; they are not retroactively reserved.
The receipt, list and detail APIs return the reserved count, and the UI distinguishes
these older submissions from reservations. Created booking UIs use the saved plate
snapshot. Fare labels identify passengers rather than fabricated seat numbers.
This feature reserves a quantity; specific seat-number assignment remains pending.
No payment is collected or ticket issued. There is no cancellation/release endpoint
or automatic reservation expiry yet; these need a future transactional release flow.

## Local sample data

`AddBusCatalog` is applied by the existing startup migration pipeline.
Development's `Catalog:SeedDemoData` is enabled by default. On startup it adds
missing sample records and schedules for today through the next 13 days:

- 5 approved sample operators, 20 locations, 22 terminals;
- 66 buses, 34 directional routes and 924 initial schedules;
- the 33 former UI bus/class/fare options, with separate buses for each direction.

Samples use `DEMO-` codes/fleet numbers and `IsDemo = true`. Terminal names and
addresses are illustrative. The seeder is transactional and safe to repeat;
it preserves existing records and does not modify applications, approval history,
bookings or credentials. Restart locally to replenish the upcoming schedule window.

Set `Catalog:SeedDemoData` to false to stop adding samples. Existing samples remain
in the database. UAT/Production never run the sample seeder, even if that flag is
set; they receive the schema migration and need actual approved partner data.
If automatic migrations are disabled, apply migrations before running this version.

Run integration checks against disposable SQL Server test databases:

```powershell
$env:EBYAHE_TEST_SQL_SERVER = '.\SQLEXPRESS'
dotnet test EByahe.Tests
```
