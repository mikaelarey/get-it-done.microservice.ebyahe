# Geographic setup tables

The `AddCodeDecodeGeography` migration creates and seeds these SQL Server tables
from the supplied `region.sql`, `province.sql`, `municipality.sql` and `barangay.sql`:

| Table | Seed rows | Parent column |
| --- | ---: | --- |
| `CodeDecodeRegions` | 17 | None |
| `CodeDecodeProvinces` | 82 | `CodeDecodeRegion` |
| `CodeDecodeMunicipalities` | 1,634 | `CodeDecodeProvince` |
| `CodeDecodeBarangays` | 42,046 | `CodeDecodeMunicipality` |

Every table uses a GUID `Id` primary key, an identity integer `Code` starting at 1,
a required Unicode `Decode` (255 characters), `CreatedBy` defaulting to `System`,
and `DateTimeCreated` defaulting to the UTC database time. `CreatedBy` references
`RefUsers.ExternalUserId`. Regions also retain the source `description` as
`Description`. The source name is stored in `Decode`.

Source numeric IDs are preserved as `Code`, with deterministic GUIDs for each seed
row. Parent references use the parent's unique, indexed `Code`, never its GUID
`Id`. Deletes are restricted while children reference a record. Parent/name
indexes support filtering provinces, municipalities and barangays. Names are not
globally unique: different parents can have places with the same name.

## Seed batches and deployment

The original MySQL inserts are converted to SQL Server Unicode literals, including
escaped apostrophes and names such as `Las Piñas City`. No MySQL commands are
executed. All codes, names, region descriptions and relationships reflect the
supplied files, including their historical region labels; this migration does not
update the dataset to a newer geographic classification.

Versioned seed SQL is embedded in `EByahe.Infrastructure` from
`Persistence/Migrations/Scripts/Geography/`. The deployment does not need files from
Downloads or a separate data import step. Keep these resources unchanged once the
migration is deployed; use a new migration for future updates.

The migration creates the tables and executes **91 separate seed commands**, each
inserting at most **500 rows**. This stays below SQL Server's 1,000-row INSERT
limit. Resources contain `GO` section separators; the migration splits these before
execution, so no `GO` is sent to SQL Server. Each command enables and disables
`IDENTITY_INSERT` for its own table, preserving supplied codes while allowing later
inserts to use the next identity value. Regions load before provinces, then
municipalities, then barangays, with foreign keys enabled throughout.

EF applies the migration transactionally and records it in `__EFMigrationsHistory`;
subsequent starts do not import the data again. A failure rolls back the migration
rather than keeping a partial seed. Rollback drops the four new tables in child to
parent order; it also removes any records subsequently added to those tables.
Existing locations, terminals, buses and bookings are not changed or linked by
this migration.

Apply through the service's existing startup migration mechanism, or explicitly:

```powershell
dotnet ef database update --project EByahe.Infrastructure --startup-project EByahe.Application -- --environment Development
```

For deployment with a reviewed SQL script:

```powershell
dotnet ef migrations script 20261118030605_AddBusPartnerFleetPlateUniqueness 20261118032912_AddCodeDecodeGeography --idempotent --project EByahe.Infrastructure --startup-project EByahe.Application --output geography-migration.sql -- --environment Development
```

The target database must already have the preceding migrations. The earlier audit
user migration supplies the `System` reference required by these seeds.

## Verification

Migration tests compare every seeded code, parent, name and region description
against SHA-256 fingerprints derived from the source rows. They verify audit
defaults, Unicode and apostrophe preservation, repeat application without duplicate
rows, rollback/reapplication, GUID stability, trusted foreign keys, code uniqueness,
identity generation and rejection of unknown creators. Batch tests check the row
limit and transaction settings for all 91 seed commands.

Original source file SHA-256 hashes:

| Source | SHA-256 |
| --- | --- |
| `region.sql` | `84927c09e6540c6bef82b4ab82a68695a474d597399d54070a52437cfce338e9` |
| `province.sql` | `ecd3025cb417e26f3b711db7d89a30ba428b19e5207a8d1466516bcd3fa2b789` |
| `municipality.sql` | `0fd5ffba2b10cf2bf8550fc30f4b8a30c70bbdce1a629a4e26fd171c11b52057` |
| `barangay.sql` | `fba47f34741b5a29a8baea462e9273e1bd38bfa99d70e674ccd86a26217a9fb0` |
