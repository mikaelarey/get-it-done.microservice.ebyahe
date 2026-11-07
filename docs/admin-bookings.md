# Admin booking lists

The admin-shell Bookings menu opens `/bus-bookings` and `/hotel-bookings` in the eByahe microfrontend. Customer booking pages continue using their existing routes.

Both admin lists require `admin` on `get-it-done-web` and `booking.read` on `get-it-done-ebyahe-api`. They use the admin shell's authenticated session channel; the API validates the signed bearer token independently.

`GET /api/admin/bus-bookings?page=1&pageSize=10&search=BK-&status=Submitted` reads saved `BusBookings`. Search matches the booking reference; status is optional. Page is 1–1,000,000, page size is 1–50, search is at most 64 characters, and status is at most 32 characters. SQL handles filtering, deterministic ordering, and pagination. The response includes `items`, `totalCount`, `page`, `pageSize`, and the available `statuses` for the authorized scope. Responses use `Cache-Control: private, no-store`.

Users with both admin and `partner.review`, as well as `booking.read`, can list all bookings, including historical records without an assigned departure. Other booking administrators see bookings only for partners where they have an active `PartnerUsers` membership linked through their exact Keycloak subject in `RefUsers`. Caller-supplied user or partner IDs do not expand access. Inactive memberships grant no access.

Rows contain reference, journey, operator, plate number, departure, passenger count, reserved seat count, total, status, and creation time. Journey details use the saved booking snapshot so later catalog changes do not rewrite booking history. Contact details, raw snapshots, hashes, payment metadata, and user IDs are not returned. Older snapshots missing a value show Not recorded.

The Hotel Bookings list route is present with an explicit unavailable state. Hotel booking storage and submission are not implemented yet; this page does not claim that an empty database query succeeded or show bus records as hotel reservations.

The bus list supports loading, errors/retry, empty states, filters, and numbered page links with an active page indicator. Query strings preserve filters and page on direct links, reloads, and shell navigation.
