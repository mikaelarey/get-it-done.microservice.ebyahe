# Partner record search

`GET /api/partner/{bookings|buses|terminals|users}` accepts an optional `search` query parameter (maximum 200 characters), alongside `page` and `pageSize`.

Search is case-insensitive and matches literal substrings. Whitespace separates keywords; every keyword must appear somewhere in the searchable columns of a record. Filtering runs in SQL before counting and pagination, within the caller's active memberships for approved partners. Existing read permissions still apply.

Searchable columns:

| Page | Columns |
| --- | --- |
| Buses | Fleet number, plate number, bus class, seat capacity |
| Terminals | Terminal name, location, full address including available geographic values |
| Bookings | Reference, origin, destination, bus plate, departure date/time, passenger count, total |
| Users | Full name, email |

Status, membership status, action labels, partner names, private booking contact/passenger information and temporary passwords are excluded. Booking dates support the displayed Manila date/time and ISO date/time; totals support the displayed PHP amount and unformatted decimal. SQL wildcard characters are literal search text.

The eByahe frontend places one Search field in each table header, waits 1,000 milliseconds after the last input, then calls the backend with `search` and resets to page 1. Clearing the field also waits 1,000 milliseconds. Search, pagination and scheduled-trip filters use the MFE's internal Angular router; they do not navigate the parent shell or reload its iframe. Current rows stay visible until the response replaces them, with a table loading indicator. Changing a search cancels the previous request. Pagination, booking details/back links, and live refresh retain the search. Navigation, identity changes and access revocation cancel pending searches. Identity changes and access revocation also clear previous rows. Matching text is highlighted using escaped Angular text nodes and `<mark>` elements after the response arrives.

No database migration is required.
