# Public hotel discovery

The Hotels tab uses the anonymous, read-only `/api/hotel-catalog` endpoints:

- `GET /api/hotel-catalog?search=&guests=2&roomType=&sort=name&page=1` searches hotel names and their four geographic address levels. All keyword terms must match. `roomType` is an optional setup code; omit it for all room types. Sort options are `name`, `price-asc`, and `price-desc`. Pages contain up to 12 hotels.
- `GET /api/hotel-catalog/types` lists types used by published rooms.
- `GET /api/hotel-catalog/{hotelId}?guests=2&roomType=3` returns hotel contact details and up to 100 matching rooms, ordered by nightly rate. The response includes the full matching room count.
- `GET /api/hotel-catalog/rooms/{roomId}/images/{imageId}` reads a published room photo through the existing storage adapter and verifies its size and SHA-256. Storage credentials, bucket names, object keys, audit data and removed photos are never part of public JSON responses.

Only active, non-deleted hotels and rooms belonging to an approved partner with an approved hotel application are published. A hotel needs at least one matching room to appear. Prices and counts come from rooms that match the selected capacity and room type. Guest count means guests per room, not the total capacity across several rooms.

This is a discovery catalog. `HotelBooking` currently snapshots room names and has no relation to individual room inventory; the catalog therefore makes no claim about availability for specific dates. The UI presents nightly rates, room photos and contact links so customers can confirm dates and the final price directly with the hotel. It does not create hotel bookings or process payments.

Catalog search runs on tab entry, search submission, sorting, pagination or explicit retry. Room details load on expansion; photos load lazily. There is no polling. Leaving the tab cancels pending catalog requests.

No database migration is required. `HotelCatalogTests` exercises the anonymous API against an isolated SQL Server database, including filtering, ordering, pagination, input validation and publication checks for all three public resources (search, details and photos).
