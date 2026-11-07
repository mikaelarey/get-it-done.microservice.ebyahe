# Partner user management

The partner Users page has Add user and Recent requests actions only when the signed-in user has both `get-it-done-web/partner` and `get-it-done-ebyahe-api/member.create`. Every management endpoint enforces these roles. Database checks also require a current active membership in an approved partner. The partner selector contains only eligible partners with a supported approved application category.

`POST /api/partner/users` accepts `requestId` (GUID), `partnerId` (GUID), `firstName`, `lastName`, and `email`. The server derives the category from that partner's application; caller-supplied roles or categories are ignored. It atomically saves a `PartnerMemberRequests` row, an encrypted `TemporaryPasswords` row and an outbox request. Response is 202; no user membership is shown optimistically. A repeated request ID cannot change its identity fields. Pending requests for the same partner/email are deduplicated.

The dedicated `get-it-done.ebyahe.partner-members.v1` topic carries `partner.member.requested` from eByahe and `partner.member.provisioned` from Account. Both consumers acknowledge their own event types without generating loops. Account performs an exact email lookup, verifies any persisted Account identity against Keycloak, creates a new identity only if necessary, and adds a fixed set of roles. Existing profiles, passwords and unrelated roles are preserved. It saves Account Users/history, registration events when applicable, and a correlated reply in one SQL transaction. A reply outbox entry keyed by request ID prevents duplicate provisioning on Kafka redelivery.

Members receive web `partner`, eByahe `booking.read` and `booking.confirm`, and `default-roles-<realm>`. Bus operators add `bus.read` and `bus.write`; hotels add `hotel.read` and `hotel.write`. Members are not newly granted `admin`, `partner.review` or `member.create`. Existing independently assigned roles are retained.

eByahe validates the reply's key, request hash, creator, partner, email, source and version before creating/reactivating the unique `PartnerUsers` membership and projecting a missing RefUser. If the inviter's membership is revoked or the partner suspended before completion, the request is cancelled and its temporary password removed. Replays do not duplicate users or memberships. Incoming/outgoing receipt histories retain encrypted request payloads and never plaintext passwords.

## Temporary passwords

New Keycloak identities receive a cryptographically random 10-character password containing uppercase, lowercase, a digit and punctuation, with `temporary=true` and required action `UPDATE_PASSWORD`. The existing email-verification policy is respected. No SMTP delivery is required for local onboarding. Existing users never have their password replaced.

`TemporaryPasswords` has GUID `Id` and `RequestId`, `ProtectedPassword`, `CanReveal`, `CreatedAtUtc` and `AvailableUntilUtc`. AES-256-GCM binds the ciphertext to the request ID. The same encrypted value goes through Kafka; Account decrypts it only in memory for the Keycloak create request. An admin-only Keycloak profile attribute, `partnerMemberRequest`, makes retries recognize identities created by the same request without ever resetting a credential.

Passwords are available through the creator's Recent requests and the Users table's Temporary password column for seven days after creation, only after Account confirms that a new temporary credential was assigned. Show/Hide controls fetch passwords on demand using `GET /api/partner/users/requests/{id}/temporary-password`, with current role and active-partner checks and `Cache-Control: no-store`. The users list includes only a retrievable request ID, never plaintext; this ID is restricted to the original creator with `member.create` and matched to both the user and partner. Existing accounts and unavailable credentials show “Not available”. Other users, even other partner members, cannot retrieve a creator's password. Password values are cleared from the UI on hide, close, account switch, permission loss and component destruction. The seven days limit retrieval from this application; Keycloak forces a change at first login. It is not a credential expiry setting. Database owners should schedule retention cleanup for expired ciphertext and keep the encryption key secure until retained requests are no longer needed.

## Local setup

```powershell
# eByahe solution directory; configure matching keys without printing them
./scripts/Configure-LocalPartnerMemberEncryption.ps1
# Account solution directory; ensure role scopes, service permissions and profile marker
./scripts/Configure-LocalKeycloakPartnerRoles.ps1
# Kafka project directory
./Import-Topics.ps1 -Environment development
```

Apply eByahe migrations and restart both APIs. `Database:MigrateOnStartup` applies the additive tables on startup. For hosted environments, configure the same secret `PartnerMembers:PasswordEncryptionKey` (base64, 32 random bytes) independently in both service deployments. Preserve it across restarts and back it up securely; changing it while old requests exist requires a planned rotation. Never commit the key. Deploy the updated Keycloak definitions or configure the `partnerMemberRequest` attribute with admin-only view/edit, and grant the Account service `manage-users`, `view-clients` and `view-realm`. Restrict the new topic's producers and consumers to Account and eByahe service principals.

Management endpoints also include `GET users/workspaces`, `GET users/requests` (the creator's latest 50 accessible requests), and `GET users/requests/{id}`. The normal partner users list remains available to signed-in partners without `member.create`.
