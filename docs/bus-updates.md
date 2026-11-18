# Bus actions

The partner Buses table replaces Partner with an Actions column. View opens the
fleet number, plate number, bus class, seat capacity, partner and active status.
Update requires both `bus.read` and `bus.write`, with an active membership in the
bus's approved bus operator partner.

`GET /api/partner/buses/{id}` loads the editable details and `hasUpcomingTrips`.
`PUT /api/partner/buses/{id}` accepts `fleetNumber`, `plateNumber`, `busClass`,
`seatCapacity` and `isActive`. Both endpoints require write permission and return
404 for unknown or inaccessible buses. The partner cannot be reassigned.

Updates use the existing creation validation, capitalize fleet/plate numbers,
resolve the class from CodeDecodeBusClasses and require 1–100 seats. The existing
unique database indexes enforce global plate uniqueness, partner/fleet uniqueness
and partner/fleet/plate uniqueness. A conflict returns 409 without saving anything.

Capacity is read-only while a bus has future Scheduled departures. The backend
checks this inside the update transaction as well, returning 409 if scheduling
changed after the form loaded. Other bus details remain editable. This preserves
the inventory established when trips were scheduled; changing a bus does not reset
their available seats or existing reservations.

The bus update and `bus.updated` outbox activity commit together. Active members
of the selected partner receive `{user} updated a bus.`. Successful updates refresh
the bus and scheduled-trip lists through the existing live update mechanism.
No database migration is needed. Deploy the updated notification consumer before
the eByahe API and frontend.
