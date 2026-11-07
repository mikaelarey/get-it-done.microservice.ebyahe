# Partner portal

The partner shell is a separate Angular project, `get-it-done.microfrontend.partner-shell`, served locally on port 4400. It embeds eByahe only. Its five menu destinations map to `/partner/dashboard`, `/partner/bookings`, `/partner/terminals`, `/partner/scheduled-trips`, and `/partner/users` in eByahe. Page sources are under `src/app/pages/partner` in the eByahe microfrontend.

Entry requires the exact `partner` client role on `get-it-done-web`. `admin` alone and a realm role with the same name do not authorize the portal. The iframe requests `get-it-done:request-partner-auth` and accepts correlated `get-it-done:partner-auth-session` responses only from the configured partner shell parent window and origin. The public and admin session channels are separate. Local eByahe configuration trusts `http://localhost:4400` in `partnerShellOrigins`. Configure the exact deployed partner shell origins in each hosted environment before deployment.

Bearer-authenticated, read-only API routes:

- `GET /api/partner/dashboard`: partner workspace count, booking count, terminal count, scheduled-trip count, and distinct active user count.
- `GET /api/partner/bookings`: saved bus booking summaries.
- `GET /api/partner/terminals`: terminals, locations, addresses, partner names and active state.
- `GET /api/partner/scheduled-trips`: routes, departures, bus plates/classes, remaining seats, fares and status.
- `GET /api/partner/users`: partner memberships, names, emails and active state.

Lists accept `page` (1–1,000,000) and `pageSize` (1–50, default 10), with SQL paging and deterministic ordering. Responses use `no-store`. All queries resolve the exact signed JWT subject through `RefUsers.ExternalUserId` and require an active `PartnerUsers` membership for an approved partner. They never trust caller-supplied user or partner IDs. A user can belong to multiple partners and sees only those partners' records. Inactive memberships and suspended partners grant no access. Legacy bookings without a scheduled trip cannot be attributed to a partner and are excluded. Booking snapshots and private payment/contact data are not returned.

The dashboard is currently a frontend design preview populated from `dashboard.data.ts`, with sample metrics, period controls, charts, departures, bookings and activity. It does not call the dashboard API or write sample records to the database. The dashboard API remains available for future integration. Bookings, terminals, scheduled trips and users continue to load database records; terminal/trip/user editing is not part of this portal setup.

Account's partner-approval consumer now grants `partner` on `get-it-done-web`, plus the existing booking, member and category permissions. It no longer requests an `admin` grant. Existing unrelated role assignments are preserved.

Local Keycloak setup is idempotent:

```powershell
cd get-it-done.keycloak
.\development\Enable-PartnerShell.ps1 -BackfillApprovedPartnerMembers
```

This creates the web client role if missing, permits localhost port 4400 redirects/origin, and optionally grants the role only to existing active approved partner members. It reads the existing local Keycloak environment credentials without printing them. The DEV/UAT/Production portable realm files include the partner role and use it for the Approved Partner group. Sign in again to receive updated roles.
