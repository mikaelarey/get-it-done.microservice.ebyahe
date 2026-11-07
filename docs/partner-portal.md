# Partner portal

The partner shell is a separate Angular project, `get-it-done.microfrontend.partner-shell`, served locally on port 4400. It embeds eByahe only. Its menu destinations map to `/partner/dashboard`, `/partner/bookings`, `/partner/buses`, `/partner/terminals`, `/partner/scheduled-trips`, and `/partner/users` in eByahe. Buses appears immediately after Bookings and requires `bus.read` on `get-it-done-ebyahe-api`. Page sources are under `src/app/pages/partner` in the eByahe microfrontend.

Entry requires the exact `partner` client role on `get-it-done-web`. `admin` alone and a realm role with the same name do not authorize the portal. The iframe requests `get-it-done:request-partner-auth` and accepts correlated `get-it-done:partner-auth-session` responses only from the configured partner shell parent window and origin. The public and admin session channels are separate. Local eByahe configuration trusts `http://localhost:4400` in `partnerShellOrigins`. Configure the exact deployed partner shell origins in each hosted environment before deployment.

Bearer-authenticated, read-only API routes:

- `GET /api/partner/dashboard`: partner workspace count, booking count, terminal count, scheduled-trip count, and distinct active user count.
- `GET /api/partner/bookings`: saved bus booking summaries.
- `GET /api/partner/buses`: fleet numbers, plates, classes, seat capacities, partner names and active state; requires `bus.read` on `get-it-done-ebyahe-api`.
- `GET /api/partner/terminals`: terminals, locations, addresses, partner names and active state.
- `GET /api/partner/scheduled-trips`: routes, departures, bus plates/classes, remaining seats, fares and status.
- `GET /api/partner/users`: partner memberships, names, emails and active state.

Lists accept `page` (1–1,000,000) and `pageSize` (1–50, default 10), with SQL paging and deterministic ordering. Responses use `no-store`. All queries resolve the exact signed JWT subject through `RefUsers.ExternalUserId` and require an active `PartnerUsers` membership for an approved partner. They never trust caller-supplied user or partner IDs. A user can belong to multiple partners and sees only those partners' records. Inactive memberships and suspended partners grant no access. Legacy bookings without a scheduled trip cannot be attributed to a partner and are excluded. Booking snapshots and private payment/contact data are not returned.

The dashboard is currently a frontend design preview populated from `dashboard.data.ts`, with sample metrics, period controls, charts, departures, bookings and activity. It does not call the dashboard API or write sample records to the database. The dashboard API remains available for future integration. Bookings, terminals, scheduled trips and users continue to load database records. Users with `member.create` can now add partner users through the [partner member workflow](partner-members.md); terminal/trip editing is not part of this portal setup.

The Buses page has an Add bus modal for users with both `bus.read` and `bus.write` on the eByahe client. `GET /api/partner/buses/workspaces` returns only approved bus operators with an active membership for that caller. `POST /api/partner/buses` validates that scope again and saves a GUID bus ID, partner ID, fleet number, plate number, bus class and seat capacity (1–100). New buses are active. Plate numbers are globally unique and fleet numbers unique per partner; duplicate submissions return 409. Read-only partners cannot open the modal or call either management endpoint. The shell route, remote route, live permission checks and API all enforce the required roles.

Terminals uses the existing paginated database list. Its Add terminal modal requires the same `bus.read` and `bus.write` client permissions as Add bus. `GET /api/partner/terminals/workspaces` lists only the caller's approved bus operators, and `GET /api/partner/terminals/locations` lists active database locations for eligible callers. `POST /api/partner/terminals` accepts `partnerId`, `locationId`, `name` (maximum 254 characters) and `address` (maximum 500). The server checks active partner membership and active location again, creates an active terminal with a GUID ID, and returns 201. A duplicate name for the same partner and location returns 409; unavailable locations return 400. Partners without write access retain the existing read-only list. The list refreshes after a successful save.

Scheduled Trips also uses its existing paginated database list and an Add scheduled trip modal with the same read/write permissions. `GET /api/partner/scheduled-trips/options` returns the caller's approved bus operators and their active buses, terminals and routes. `POST /api/partner/scheduled-trips` accepts `partnerId`, `busId`, `originTerminalId`, `destinationTerminalId`, `departureAtUtc`, `durationMinutes` (1–2,880) and `baseFare` (PHP 0.01–1,000,000 with at most two decimal places). The UI treats departure input as Manila time (UTC+8), independent of the browser's time zone, and sends UTC. The API rechecks membership, partner approval, active assets, distinct terminals and future departure. Existing routes retain their travel time; a new terminal pair creates its route atomically with the trip. Each departure has its own GUID and starts with the selected bus's full seat capacity. Overlapping scheduled journeys for the same bus return 409, including concurrent submissions; another departure can start when the previous journey ends. Options and creation require bus write permission, while existing partner read access to the list is preserved.

Account's partner-approval consumer now grants `partner` on `get-it-done-web`, plus the existing booking, member and category permissions. It no longer requests an `admin` grant. Existing unrelated role assignments are preserved.

Bus, terminal and scheduled-trip creation queues `bus.created`, `terminal.created` and `scheduled-trip.created` on `get-it-done.notifications.v1` in the same SQL transaction as the new record. Recipients are a snapshot of active `PartnerUsers` for that record's selected partner, including the creator when they are an active member. Other workspaces belonging to the creator, inactive memberships and general administrator audiences are excluded. Payloads contain only actor, relative list link, entity/partner GUIDs and explicit user targets. Disjoint batches of at most 25 recipients keep payloads below 64 KiB even for long Unicode subjects. Existing records and demo seeding do not generate retrospective creation notifications. Notification consumption deduplicates stable event IDs on broker redelivery and waits for missing RefUser projections without adding unrelated users.

Local Keycloak setup is idempotent:

```powershell
cd get-it-done.keycloak
.\development\Enable-PartnerShell.ps1 -BackfillApprovedPartnerMembers
```

This creates the web client role if missing, permits localhost port 4400 redirects/origin, and optionally grants the role only to existing active approved partner members. It reads the existing local Keycloak environment credentials without printing them. The DEV/UAT/Production portable realm files include the partner role and use it for the Approved Partner group. Sign in again to receive updated roles.
