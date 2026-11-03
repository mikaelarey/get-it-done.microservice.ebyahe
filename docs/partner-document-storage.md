# Partner document storage

Partner upload bytes are stored in the existing private SeaweedFS S3 service.
SQL Server retains document metadata, SHA-256, `StorageBucket`, and `StorageKey`.
History snapshots retain those references instead of Base64 file contents. An
immutable key includes application ID, document ID, and the file's SHA-256, so a
later version cannot overwrite a historical version.

## Environment settings

| Environment | Endpoint | Bucket |
| --- | --- | --- |
| Development | `http://localhost:18333` | `get-it-done-dev` |
| UAT | `https://storage.getitdone.com.ph` | `get-it-done-uat` |
| Production | `https://storage.getitdone.com.ph` | `get-it-done-production` |

The SDK uses region `us-east-1`, path-style addressing, signed requests, and
private objects. No permanent signed URL, public ACL, or storage credential is
stored in document rows or sent to a browser. Backend requests keep the existing
authenticated multipart contract. The [AWS S3 client configuration](https://docs.aws.amazon.com/sdkfornet/v4/apidocs/items/S3/TS3Config.html)
documents the path-style and custom endpoint settings used here.

For development, run from the Ebyahe service directory:

```powershell
.\scripts\Configure-LocalDocumentStorage.ps1
```

This imports only the existing Docker DEV credential from the neighboring
storage project's ignored `development/config/storage-login.json` into
Ebyahe's .NET user secrets. It preserves other secrets, never prints keys,
and does not create/reset buckets. Custom DEV endpoint and bucket settings
are imported from that same file.

For hosted UAT and Production, supply `Storage__AccessKey` and
`Storage__SecretKey` through the deployment's secret store, using the matching
hosted bucket credential. Local Docker UAT/PROD keys are independent test
identities and must not be used against the hosted service. The bucket,
endpoint, and region can be overridden with `Storage__Bucket`,
`Storage__Endpoint`, and `Storage__Region`. HTTP is allowed only on loopback;
remote storage uses HTTPS and normal certificate validation. See the storage
project's README for its protected server credential locations.

## Upgrading existing database files

Stop the old Ebyahe API before the cutover so it cannot continue writing SQL
blobs. The upgrade has two migrations:

1. `AddPartnerDocumentStorageReferences` adds nullable references and permits a
   staged transition of legacy binary data.
2. The application initializer uploads current documents and all historical
   Base64 versions. It downloads each unique object and verifies its size and
   SHA-256 before replacing the SQL bytes with references. SQL updates occur in
   a transaction, retaining application/history/approval IDs and timestamps.
3. `RemovePartnerDocumentDatabaseContent` requires every document and history
   version to have a reference before it removes the `Content` column.

Run the migration without starting another API listener:

```powershell
dotnet run --project EByahe.Application --no-launch-profile -- --environment Development --MigratePartnerFilesOnly true
```

For a published deployment, set its normal UAT/Production environment and secret
configuration, then run `dotnet EByahe.Application.dll --MigratePartnerFilesOnly
true`. This explicit command runs migration even if `Database:MigrateOnStartup`
is false. Alternatively, normal startup performs the same upgrade when startup
migration is enabled.

Running `dotnet ef database update` alone against legacy files intentionally
fails the final guard until the file-copy step has run. Failed size/checksum or
read-back verification retains SQL data; retries reuse the same object keys.
Rollback past the reference migration refuses to discard references until the
bytes have been restored from storage.

## Submission failure handling

All five documents, including bank account proof, are validated before any upload. Confirmed upload/database
failures clean up this submission's uncommitted objects with a bounded timeout.
If the database commit succeeded, or the commit outcome cannot be determined,
objects are retained to avoid breaking committed references. Failed cleanup is
logged for reconciliation using the unique application prefix. SQL and S3 do
not share a distributed transaction. Storage operation failures return a safe
`503` response without exposing credentials or raw S3 error details.

Historical objects are retained when metadata changes or a document is removed
from the current application; no automatic history-object deletion is performed.

## Verification

```powershell
$env:EBYAHE_TEST_SQL_SERVER = '.\SQLEXPRESS'
$env:EBYAHE_TEST_STORAGE_CONFIG = (Resolve-Path '..\get-it-done.storage\development\config\storage-login.json').Path
dotnet test EByahe.Tests/EByahe.Tests.csproj --configuration Release
```

SQL tests create disposable databases and cover historical versions, corrupt
read rollback, removal guards, retries, and cleanup. The real S3 test permits
only a loopback DEV endpoint and deletes only its uniquely named test object.
