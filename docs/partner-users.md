# Partner users

`Getitdone.Ebyahe.dbo.PartnerUsers` links eByahe's `Partners` and `RefUsers`.
A partner can have many users, and a user can belong to many partners. This
association belongs to eByahe; Account remains the authority for user identities.

| Column | Type | Purpose |
| --- | --- | --- |
| `Id` | GUID | Membership primary key |
| `PartnerId` | GUID | Foreign key to `Partners.Id` |
| `RefUserId` | GUID | Foreign key to eByahe `RefUsers.Id` |
| `IsActive` | bit, default true | Deactivate membership without deleting its relationship |
| `CreatedAtUtc` | datetimeoffset | Original membership creation time in UTC |

The unique index on `(PartnerId, RefUserId)` prevents duplicate membership even
when a row is inactive. Reactivate the same row when a user rejoins. Both foreign
keys restrict parent deletion, preventing orphan memberships. A user lookup index
supports querying memberships by `RefUserId` and active status.

`RefUserId` is the local GUID, **not** the Account/Keycloak subject. Resolve the
signed-in user's `sub`, or an application's `CreatedBy`, through
`RefUsers.ExternalUserId` first. Do not join using email. Account roles are
independent of membership; joining a partner does not make the user a global admin.

## Approval workflow

The approval/member-management endpoints and screens are still pending. The
schema supports this future transaction:

1. An authorized reviewer approves the application and creates its `Partner`.
2. Resolve the applicant's existing local reference using the application's
   `CreatedBy`. If projection has not arrived, sync/retry instead of inventing a user.
3. Insert the first `PartnerUser` using the new partner GUID and local user GUID
   in the same transaction as approval and partner creation.
4. Authorized partner management can add additional members or deactivate them.

For example, inside the authorized approval transaction:

```csharp
var applicant = await database.RefUsers.SingleAsync(
  user => user.ExternalUserId == application.CreatedBy, cancellationToken);
database.PartnerUsers.Add(new PartnerUser
{
  Partner = approvedPartner,
  RefUserId = applicant.Id
});
await database.SaveChangesAsync(cancellationToken);
```

Membership does not itself grant API permissions. Future partner operations must
check the authenticated user's active membership **and** `Partner.Status ==
"Approved"`. Suspended partners can retain membership rows, while those checks
prevent operations until the partner is approved again. Unowned sample operators
do not receive arbitrary user memberships. This schema change adds no public
self-enrollment endpoint and does not implement approval or member authorization.

`Partner.OwnerUserId` remains the original external owner metadata for backwards
compatibility. Use `PartnerUsers` for the multi-user relationship. The current
notification directory still projects that owner; consuming the complete member
list is a separate notification integration change.

## Migration

`AddPartnerUsers` creates the table and maps every existing non-null
`Partners.OwnerUserId` through `RefUsers.ExternalUserId` to one active membership.
It preserves owner subjects, partner IDs, user IDs, applications, bookings and
inventory. Unowned partners remain without memberships. If an owner has no local
reference, the migration stops before creating the table; sync the missing
Account user and retry. No user is fabricated or matched by email.

Back up the database before deployment. Startup applies the migration using the
existing initialization pipeline, or apply it separately:

```powershell
dotnet ef database update --project EByahe.Infrastructure --startup-project EByahe.Application -- --environment Development
```

The context exposes `PartnerUsers`, `Partner.UserMemberships` and
`RefUser.PartnerMemberships`. The SQL integration tests cover multiple members,
multiple partners per user, inactive/re-activated membership, duplicate and orphan
prevention, exact owner identity backfill, migration retry and preservation.
