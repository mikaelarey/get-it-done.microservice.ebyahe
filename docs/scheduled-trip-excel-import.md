# Scheduled trip Excel import

The partner Scheduled Trips page offers Download template and Upload Excel beside Add scheduled trip. Download template opens a dedicated dialog with a partner selector and date range; its Download template action saves the workbook and closes the dialog. Upload Excel opens an import dialog with a partner selector and file picker, without template date fields. Both dialogs keep the header and footer fixed while the body scrolls when needed. The user needs the web partner role and the eByahe scheduled-trip.read and scheduled-trip.write roles, plus active membership in an approved bus operator workspace.

## Template

The backend generates Scheduled-trip-import-template.xlsx for the selected partner and a date range of 1–31 days. Each departure date has a sheet named YYYY-MM-DD. Users may copy a date sheet and rename it to another date; the workbook supports up to 31 date sheets. Instructions and the hidden _Lists reference sheet do not contain imported trips.

Each date sheet has six columns: Origin terminal, Destination terminal, Bus, Departure time (Manila), Duration (minutes), Fare (PHP). Database-backed dropdowns contain only the selected partner's active buses and active terminals with active locations. Labels include record IDs to distinguish similarly named terminals or fleet numbers. The selected record ID is authoritative; changing label text never creates a bus or terminal. Download a new template to refresh the available records. Existing active routes and their current travel times are listed in Instructions.

The header is frozen and has a height of 24 points; input rows use 20 points, with borders on all six columns through row 501. Excel validation provides dropdowns for terminals, buses and hourly departure times, plus duration and fare limits. The departure list has 24 choices from 12:00 AM through 11:00 PM, and numeric Excel times display with an AM/PM suffix. Server validation also runs because Excel validation can be bypassed.

## Dates and validation

Sheet dates and departure times use Manila time (UTC+08:00). Past dates are rejected. Today's sheet is allowed only for departure times that are still in the future. The backend repeats the future-departure check inside the save transaction. Dates are taken from sheet names; a row never supplies a different date. Choose an hourly departure time from the dropdown, for example 7:00 AM or 7:00 PM. Uploads also accept the previous 24-hour HH:mm text and Excel time values without a date component. Departures are saved in UTC.

The workbook must be a standard .xlsx file, at most 5 MB, with at most 500 non-empty trip rows across all date sheets. Empty input rows are ignored. Every date sheet must retain the six headers. Invalid sheet names, formulas, extra populated columns, malformed workbook content, invalid time values, non-integer durations, invalid fares and identical origin/destination terminals produce sheet- and row-specific errors. Duration must be 1–2880 minutes; fare must be PHP 0.01–1,000,000 with no more than two decimal places.

All referenced buses and terminals are rechecked against the database and selected partner. Inactive/foreign records are rejected. Existing routes must be active and use their current travel time. A new origin/destination pair creates one shared route, and every row using that pair must agree on travel time. A bus cannot have overlapping scheduled trips in the database or within the file, including trips spanning midnight and different date sheets. A departure exactly at the preceding trip's arrival is allowed.

## Saving and tracking

An authorized upload attempt creates an Uploads entry with upload type ScheduledTrips before parsing. All trips, new routes, ScheduledTripLogs, UploadRecords links and one grouped scheduled-trips.imported outbox activity are committed in one serializable SQL transaction. A partner lock also serializes imports against manual trip creation and updates. Invalid files create no trips. Database failures roll back every record from that file while preserving the upload attempt.

Each created trip starts Scheduled, with available seats taken from its bus's current seat capacity. Audit entries use action Imported and reference the upload ID. The notification reads {user} successfully created {count} scheduled trips using file upload (singular for one), and links to /uploads/{uploadId}. The existing upload details page shows the records linked through UploadRecords. No UploadId column is added to ScheduledTrips and no schema migration is needed.

GET /api/partner/scheduled-trips/template accepts partnerId, startDate and endDate query parameters. POST /api/partner/scheduled-trips/import accepts multipart partnerId and file. Successful imports return importedCount, errors and uploadId. Validation errors return HTTP 400 with zero imported records. Unauthorized workspace access returns 403. Retrying an already completed file is checked against the schedules it created, preventing duplicate overlapping trips.
