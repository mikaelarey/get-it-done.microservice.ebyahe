# Catalog audit and uploads

`BusLogs`, `TerminalLogs` and `ScheduledTripLogs` record successful application
actions from the partner catalog flows. Every entry has a GUID primary key, its
record's foreign key, `CreatedBy`, UTC `DateTimeCreated`, `Action`,
`PreviousValue` and `CurrentValue`. The values are JSON snapshots of the relevant
business fields. Creation/import entries have no previous value; updates preserve
both snapshots. Imported audit entries also reference the responsible upload.
All creator fields reference `RefUsers.ExternalUserId` with the existing
case-sensitive identity rules. Audit entries are append-only through EF and have
restrictive foreign keys. Business data, audit entries, upload links and outgoing
notifications save in one transaction; a failed save leaves no partial history.

Scheduled-trip creation logs the initial fare, departure, bus/route, status code
and available seats. Verified successful payment logs the available-seat change
in the same transaction as booking confirmation. Replayed payment events do not
reserve seats again or append another trip log. Failed or cancelled payments that
do not change inventory create no trip-change entry.

## Upload batches

The batch table is **Uploads**, with exactly these columns:

| Column | Meaning |
| --- | --- |
| Id | GUID primary key |
| CreatedBy | FK to the initiating RefUsers.ExternalUserId |
| DateTimeCreated | UTC timestamp |
| UploadType | Integer FK to CodeDecodeUploadTypes.Code |

`CodeDecodeUploadTypes` follows the existing setup schema: GUID `Id`, identity
integer `Code`, `Decode`, `CreatedBy` defaulting to `System` and UTC
`DateTimeCreated`. Its migration seeds codes 1–4 as Buses, Terminals,
ScheduledTrips and Users. Code has an indexed unique alternate key; references
use Code, not Id.

There is **no UploadId column on Buses, BusTerminals, ScheduledTrips or RefUsers**.
Instead, **UploadRecords** has GUID `Id`, required `UploadId` referencing Uploads,
and nullable `BusId`, `TerminalId`, `ScheduledTripId` and `RefUserId` foreign keys.
A database check requires exactly one record reference per link. Filtered unique
indexes prevent linking the same record twice within one upload. This retains real
foreign keys for every supported record type.

Each authorized bus/terminal import reaching the import action creates one Uploads
entry before file parsing. Validated rows each receive an UploadRecords link and
an Imported audit entry when the whole batch commits. Rejected files and failed
save attempts keep their upload entry with no record links, audit entries or
creation notifications. Requests rejected before the import action (authentication,
authorization, missing required fields, or request-size limits) do not create an
upload entry. With the requested four-column Uploads schema, a batch without links
can represent an in-progress or unsuccessful attempt; there is no outcome/status
column.

The existing single notification per upload is preserved. Its details now include
the actual upload GUID as both `id` and `uploadId`, plus the partner and record count.
Manual create/update actions do not create Uploads entries. ScheduledTrips and Users
have seeded upload types and record-link support; they do not yet have Excel import
endpoints.

## Upload details and notifications

Successful imports link to `/uploads/{uploadId}` in the partner shell, which loads
the eByahe `/partner/uploads/{uploadId}` page. The notification inbox displays
`{user} successfully created {count} {type} using file upload.` For example,
`Test Five successfully created 20 buses using file upload.` A one-record upload
uses the singular noun. Each recipient still receives one summary for the batch;
Kafka replays do not create another notification.

`GET /api/partner/uploads/{uploadId}?page=1&pageSize=10` returns metadata and the
matching typed record collection: `buses`, `terminals`, `scheduledTrips`, or
`users`. The page renders the corresponding table and paginates server-side.
Rows show current business details, rather than snapshots frozen at upload time.
User uploads expose only ID, name, email, provider and account-created date.

The caller needs the partner role, the corresponding resource's read permission
(member.create or member.manage for users), and an active membership in an
approved partner for every linked record. Missing, inaccessible, mixed-workspace
or mismatched-type batches return 404 without exposing metadata or partial rows.
Empty attempts are visible only to their creator with current workspace access.
Page sizes are limited to 50; responses are not cached. No additional migration is
required for the details page. Existing stored notifications retain their links;
new successful uploads use the details URL.

## Migration

Apply `AddCatalogAuditAndUploads` with the service's normal EF migration workflow.
It adds six tables without altering the existing four record tables. Existing
records remain unchanged; previous creators or historical snapshots are not
inferred or fabricated. Logging starts for new application actions after deployment.
Rollback removes the new tables and their history, so use it only where discarding
that history is intended. Tests exercise rollback/reapplication in disposable
databases, foreign keys, link constraints, append-only behavior, failed imports,
atomic saves and payment replay.
