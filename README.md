# EByahe microservice

Consumes Account registration events into local user references, using the
Account microservice's .NET 9, EF Core 9, and SQL Server structure.

## Layers

- `EByahe.Application`: API host; `Events` contains the Kafka background worker,
  registration event contract, and incoming processor. `Extensions` contains
  application DI and the design-time context factory.
- `EByahe.Domain/Models`: database entities without EF Core dependencies.
  `Interfaces` contains transport and unit-of-work contracts. `Repositories`
  contains the incoming persistence contract.
- `EByahe.Infrastructure/Persistence`: database context, entity configurations,
  and generated migrations. `Initialization` handles startup migrations;
  `Repositories` persists receipts and projects users transactionally.
  `Messaging` implements Kafka consumption. `DependencyInjection` registers services.
- `EByahe.Tests`: startup, SQL Server persistence, failure/replay, and Kafka checks.

## Database

The SQL Server database is `Getitdone.Ebyahe`. It contains:

| Table | Purpose |
| --- | --- |
| `RefUsers` | Local GUID `Id`, Account `ExternalUserId`, name, email, provider, role and creation time |
| `OutboxMessages` | Mutable Kafka delivery state, retry schedule, and delivery leases |
| `Outgoing` | Append-only outgoing Kafka status history |
| `Incoming` | One original envelope and current processing state per consumer group/topic/partition/offset |
| `IncomingLogs` | Append-only status history, timestamps, attempts, and errors for each incoming envelope |
| `PartnerApplications` | Partner corporate, authorized representative, and payout details with tracking ID and submission status |
| `PartnerApplicationDocuments` | Document metadata, checksum, and private object-storage references |
| `PartnerApplicationHistories` | Immutable before/after JSON, including versioned document references |
| `PartnerApplicationApprovals` | Approval status transitions, actor, remarks, and UTC timestamp |
| `BusBookings` | Authenticated bus booking submissions, server references, validated snapshots and retry keys |
| `Partners`, `Locations`, `BusTerminals`, `Buses` | Approved partner catalog, search locations, terminals and fleet |
| `PartnerUsers` | GUID links between partners and local user references, active status and UTC creation time |
| `BusRoutes`, `ScheduledTrips` | Directional routes and dated departures with database fares |

See [bus-catalog.md](docs/bus-catalog.md) for public search endpoints, development
sample data and the future partner management workflow.
See [partner-users.md](docs/partner-users.md) for partner membership relationships
and how the approval workflow should create the first membership.
See [application-review.md](docs/application-review.md) for the reviewer-only
application list API used by the admin Applications page.
See [partner-directory.md](docs/partner-directory.md) for the database-backed
admin Partners Lists API, filters, counts and legacy record handling.

All application primary keys and their foreign keys use SQL Server
`uniqueidentifier` / .NET `Guid`. `RefUsers.Id` is a local GUID; its unique,
case-sensitive `ExternalUserId` preserves the original Account identity and is
used when processing Kafka events. Account subjects in ownership/audit fields
remain external strings. `RefUsers` otherwise preserves Account's profile column
types, lengths, nullability, collations, defaults, unique email index and role
constraint. Roles are case-sensitive; email uniqueness is case-insensitive.
Account's contact and history tables are not replicated here. EF's internal
`__EFMigrationsHistory.MigrationId` remains framework-managed text.

The `ConvertApplicationKeysToGuids` migration patches existing catalog keys,
relationships and saved booking trip IDs transactionally without resetting
reservations. See [GUID key upgrade](docs/bus-catalog.md#guid-key-upgrade) before
upgrading an existing database.

The outgoing table schemas follow Account. Incoming storage separates each
envelope from its status history, avoiding repeated payloads. The context
rejects updates and deletes of `IncomingLogs` and `Outgoing` entries; raw SQL
is outside that guard. Incoming envelopes keep their current status and attempt
count; their `DeliveryId` identifies the initial receipt. The normalization
migration moves legacy status entries into `IncomingLogs` and consolidates
duplicate Kafka coordinates. Legacy acknowledgements are receipts, not evidence
of a completed user projection, so their envelope state becomes `Received`.

Configure the local connection without putting credentials in source files:

```powershell
dotnet user-secrets set "ConnectionStrings:EByaheDatabase" "Server=.\SQLEXPRESS;Database=Getitdone.Ebyahe;Integrated Security=True;Encrypt=True;TrustServerCertificate=True" --project EByahe.Application
dotnet tool restore
.\scripts\Configure-LocalDocumentStorage.ps1
dotnet run --project EByahe.Application --no-launch-profile -- --environment Development --MigratePartnerFilesOnly true
dotnet run --project EByahe.Application --launch-profile https
```

For deployments, set `ConnectionStrings__EByaheDatabase`. Startup applies pending
migrations before serving requests. Set `Database__MigrateOnStartup=false` when
migrations are applied separately; migration failures stop startup.

## Bus bookings

Booking creation and partner application submission queue `booking.created` /
`partner-application.submitted` activities in the same transaction as their
records and seat reservations. `OutboxMessages` and append-only `Outgoing`
track delivery to `Kafka:NotificationTopic` (default
`get-it-done.notifications.v1`) with leases and retries. Failed saves and
replayed bookings do not create additional events. Payloads contain identifiers,
small summaries and protected details paths; they exclude payment, passenger,
banking and document contents. Enable Kafka and import the new topic to deliver;
disabled publishing retains pending rows.

For the authenticated booking intake endpoint, JSON contract, validation and
frontend behavior, see [Bus booking submissions](docs/bus-bookings.md).

## Account registration consumer

Development enables Kafka at `localhost:9092` using plaintext, matching Account's
local broker. Other environments default to disabled; configure `Kafka__Enabled`,
`Kafka__BootstrapServers`, and the appropriate TLS/SASL options to enable them.

- Topic: `get-it-done.account.users.v1` (same as Account's publisher).
- Consumer group: `get-it-done-ebyahe-account-users-v1` (independent of Account).
- Event: `user.registered`, schema version `1`, with `eventId`, `occurredAtUtc`,
  and `user` containing `id`, `firstName`, `lastName`, `email`, `provider`, `role`.
- Message key must equal the user's ID. `occurredAtUtc` supplies `CreatedAtUtc`.
- A new consumer group starts at the earliest retained event. Kafka cannot
  supply registrations that were never published or have expired from retention.

Processing order:

1. Commit the raw envelope to `Incoming` with a `Received` log before parsing.
2. Record a `Processing` log and increment the attempt count.
3. Validate the event, insert the missing `RefUsers` row, and mark the receipt
   `Processed` with a log in the same SQL transaction.
4. Manually commit the Kafka offset, then append an `Acknowledged` log.

Redelivery of the same Kafka coordinate reuses its envelope. A processed receipt
skips business processing and retries acknowledgement. Registration events for an
existing user ID leave the existing reference unchanged, so duplicate events do
not overwrite newer profile details. This consumer handles registration only;
profile-update synchronization requires a separate event handler.

Database/validation failures record `Failed` without committing the offset.
The worker closes and rejoins from committed offsets after `RetryDelaySeconds`
(default 5). An invalid event or persistent data conflict blocks progress past
that record in its partition until resolved; no automatic dead-letter or skip
policy is configured. A Kafka commit failure logs `AcknowledgementFailed` while
the envelope stays `Processed`. Kafka and SQL Server do not share a transaction;
the receipt and insert-only projection make replay safe.

Generate an idempotent deployment script:

```powershell
dotnet ef migrations script --idempotent --project EByahe.Infrastructure --startup-project EByahe.Application --output ebyahe-migrations.sql -- --environment Development
```

## Validation

```powershell
dotnet test EByahe.Tests/EByahe.Tests.csproj --configuration Release
```

Enable SQL Server checks against disposable, uniquely named test databases:

```powershell
$env:EBYAHE_TEST_SQL_SERVER = '.\SQLEXPRESS'
dotnet test EByahe.Tests/EByahe.Tests.csproj --configuration Release
Remove-Item Env:EBYAHE_TEST_SQL_SERVER
```

These checks use Windows authentication and delete only their generated
`Getitdone.Ebyahe.Tests.<guid>` databases. They verify startup migration,
reinitialization, defaults, email uniqueness, case sensitivity, role/status
constraints, delivery uniqueness, restricted deletion, and append-only logs.
They also verify registration projection, SQL rollback, failed-receipt retries,
acknowledgement-failure replay, and preservation of legacy receipt history.

Include the real Kafka worker test with a disposable topic:

```powershell
$env:EBYAHE_TEST_SQL_SERVER = '.\SQLEXPRESS'
$env:EBYAHE_TEST_KAFKA = 'localhost:9092'
dotnet test EByahe.Tests/EByahe.Tests.csproj --configuration Release
Remove-Item Env:EBYAHE_TEST_SQL_SERVER, Env:EBYAHE_TEST_KAFKA
```

## Partner application submission

See [document storage and migration](docs/partner-document-storage.md) for
Docker DEV setup, hosted UAT/Production secrets, and the guarded legacy-file cutover.

`POST /api/partner-applications` accepts `multipart/form-data` and returns
`201 Created` only after the files upload to private object storage and the
application, document metadata, history, and initial approval commit in SQL.
The response contains `id`, `trackingId`,
`status: "Submitted"`, and `submittedAtUtc`. Client-supplied IDs, timestamps,
and status are not bound to the application model.

Submission requires a signed, unexpired Keycloak bearer access token with the
configured issuer and audience `get-it-done-ebyahe-api`. Invalid/missing tokens,
invalid subjects, ID tokens, wrong audiences, and invalid signatures return
`401`. A customer does not need an admin role. `CreatedBy` comes exclusively
from the validated `sub` claim; multipart fields cannot override it. This
indexed, immutable user ID is not a foreign key to asynchronously replicated
`RefUsers`. Existing anonymous applications retain NULL; new applications must
have an authenticated creator. Passwords and verification tokens are not stored.

Configure `Keycloak:Authority` / `Keycloak:Audience` or their `Keycloak__...`
environment variables. Local Development uses
`http://localhost:8080/realms/get-it-done-dev`; override this for hosted
Development. UAT/Production use their respective HTTPS realms. HTTP discovery
is allowed only for loopback Development. The web client's optional
`ebyahe-api` scope must map audience `get-it-done-ebyahe-api`; both standalone
EByahe and the public shell request it.

### History and approval tables

`PartnerApplicationHistories`: `Id` (PK), `PartnerApplicationId` (FK), nullable
`PreviousValue`, required `CurrentValue`, `Status`, and `DateTimeCreated`.
Snapshots include every application field and every document's ID, kind, name,
content type, byte count, SHA-256, upload time, `StorageBucket`, and `StorageKey`. Creation
has NULL `PreviousValue`; later field/document changes capture full persisted
before and after values, including documents not loaded by the caller. No-op
saves create no history. SQL constraints require valid JSON.

`PartnerApplicationApprovals`: `Id` (PK), `PartnerApplicationId` (FK), nullable
`PreviousStatus`, `Status`, `ChangedBy` (Keycloak subject), optional `Remarks`
(2,000 characters), and `DateTimeCreated`. Submission appends `NULL -> Pending`
under the applicant's ID. Statuses are `Pending`, `UnderReview`, `NeedsChanges`,
`Approved`, and `Rejected`. Later workflow actions can append transitions;
the latest record represents the approval state. Approval-action endpoints
are left for the subsequent workflow implementation.

Both tables are append-only through the DbContext, indexed by application/time;
approvals also have a status/time index. Automatic audit writes start a
serializable transaction when no caller transaction exists. Application,
document metadata, initial approval, and history commit together or roll back together.
The context rejects physical application deletion, creator changes, and moving
documents to another application. Direct SQL is outside these context guards.
All timestamps are UTC. Authenticated customers can read their own applications:

- `GET /api/partner-applications?page=1&pageSize=10` returns paginated summaries
  with `items`, `totalCount`, `page`, and `pageSize` (maximum 50 per page).
- `GET /api/partner-applications/{id}` returns the creator's submitted details
  and document metadata. Another user's or an unknown application returns `404`.
- `GET /api/partner-applications/{id}/documents/{documentId}` streams an owned
  document for preview or download. It verifies stored size and SHA-256, returns
  the recorded content type with inline filename, supports byte ranges, and sets
  `Cache-Control: private, no-store` and `X-Content-Type-Options: nosniff`.
  Unknown documents and other owners return `404`; guests return `401`.

Ownership comes from the validated `sub`, never a client-supplied user ID.
Bank account numbers are masked. Storage keys, buckets and history are not
exposed; file bytes are available only through the owner-authorized document
endpoint. CORS permits GET and POST from configured origins.

### Submission fields

Use these form field names (case-insensitive):

| Field | Values / limits |
| --- | --- |
| `businessCategory` | `hotel` or `bus-operator` |
| `corporate.businessName`, `corporate.tradeName` | Required, up to 255 characters each |
| `corporate.entityType` | `sole-proprietorship`, `partnership`, or `corporation` |
| `corporate.tin` | Required, up to 32 characters |
| `corporate.address` | Required, up to 2,000 characters |
| `corporate.representativeName` | Required, up to 255 characters |
| `corporate.representativeEmail` | Required email, up to 254 characters |
| `corporate.representativeMobile` | 10–15 digits, optional leading `+`, formatting spaces/dots/parentheses/hyphens; up to 32 characters |
| `payout.bankName` | Required, up to 100 characters |
| `payout.accountNumber` | Required digits with optional spaces/hyphens, up to 64 characters |
| `payout.accountHolder` | Required, up to 255 characters |
| `payout.settlementFrequency` | `daily` or `weekly` |
| `documents.registration` | DTI certificate for sole proprietorship; SEC certificate otherwise |
| `documents.bir` | BIR Form 2303 |
| `documents.permit` | Mayor's permit |
| `documents.accreditation` | DOT accreditation for hotels; LTFRB CPC/franchise for bus operators |
| `documents.bankAccountProof` | Bank certificate, statement, or account screenshot showing the account holder and account number |

All five documents are required. Accepted files are images (JPG/JPEG, PNG, GIF,
WebP, BMP), PDF, and Word (DOC, DOCX), no larger
than 10 MiB (10,485,760 bytes). Server validation checks extension, declared
content type, actual byte count, and file signature. Word checks require a
Word stream in a legacy OLE container or a Word main-document part in a DOCX ZIP
package. Signature checking does
not verify document authenticity or scan for malware. Filenames are reduced to
their final component. Contents are uploaded to private SeaweedFS S3 storage.
SQL stores only the original filename, format, byte size, SHA-256, upload time,
document kind, bucket, and object key. Versions use immutable keys under
`ebyahe/partner-applications/{application-id}/{document-id}/{sha256}`.
History retains references to original file versions; historical objects are
not overwritten or deleted when document metadata changes.

The request limit is 52 MiB, including multipart overhead for five 10 MiB files. The included IIS
`web.config` raises native request filtering to that limit; API request/form
limits and IIS server limits match it. Other reverse proxies must allow that
request size. Configure `Cors:AllowedOrigins` for deployed frontend origins;
Development allows localhost ports 4204/4200 and the development EByahe origin.

Validation failures return `400` with field errors. Unsupported request content
types return `415`; requests beyond the server limit return `413` (IIS request
filtering may reject them before ASP.NET Core). Unexpected save failures return
`500` without echoing application, banking, or file data. A failed save leaves
no partial application or documents. Each successful POST creates a new
application; request idempotency is not implemented.

Storage failures return `503`. All files are validated before upload; known
uncommitted uploads are cleaned up on failure. If a database commit is confirmed
or cannot be checked, objects are retained to avoid deleting committed files.

The EByahe microfrontend's live HTTP gateway now calls this endpoint. It uses
`https://localhost:7170/api` locally/in development,
`https://uat-ebyahe.getitdone.com.ph/api` in UAT, and
`https://ebyahe.getitdone.com.ph/api` in production. The partner flow collects
business details/documents/payout without fake account/OTP steps; account sign-in
remains in Keycloak. Deployed CORS settings live in `appsettings.UAT.json` and
`appsettings.Production.json`. Production hosting must route `/api/*` to this
service on the shared EByahe hostname.

The adapter maps nested data into dotted multipart fields:

```typescript
const body = new FormData();
body.append('businessCategory', application.businessCategory);
for (const [key, value] of Object.entries(application.corporate)) {
  body.append(`corporate.${key}`, value);
}
for (const [key, value] of Object.entries(application.payout)) {
  body.append(`payout.${key}`, value);
}
for (const [key, file] of Object.entries(application.documents)) {
  body.append(`documents.${key}`, file, file.name);
}
const response = await fetch(`${apiBaseUrl}/api/partner-applications`, {
  method: 'POST', headers: { Authorization: `Bearer ${accessToken}` }, body
});
if (!response.ok) throw new Error('Application submission failed');
return response.json(); // Includes trackingId for the confirmation page.
```

Let the browser set the multipart boundary; do not set `Content-Type` manually.
The SQL Server test suite covers multipart binding for both partner categories,
all saved fields/files, required fields, file signature/type/size rejection,
image formats, filename handling, CORS, JWT rejection, creator spoofing,
complete document snapshots, append-only enforcement, and transaction rollback.

Live update endpoints, delivery rules and hosting requirements: [SignalR](docs/realtime.md).
