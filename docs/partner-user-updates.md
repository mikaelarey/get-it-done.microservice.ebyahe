# Partner user actions

The partner Users table contains Name, Email, Membership and Actions. View and Update use icon buttons with accessible labels. Partner and temporary-password columns are removed. The details dialog shows the temporary-password control; the existing creator-only, permission and expiry checks still apply. Passwords are requested only after clicking Show and cleared when the dialog closes.

## Update permissions and scope

View requires member.create or member.manage. Update requires the exact member.manage client role plus the web partner role and an active membership in an approved partner. The backend resolves the target membership within that workspace; clients cannot supply an arbitrary account ID. Partner owners and the current user's membership cannot be deactivated. Their existing member.create/member.manage permissions cannot be removed. Partner management cannot edit system administrator accounts.

Update supports first name, last name, email, active membership and category-appropriate permissions. Profile changes belong to the account service and synchronize with Keycloak. Permission changes replace the selected category's direct eByahe client roles while preserving other categories, unrelated roles and web/realm roles. These permissions are account-wide across partner workspaces. Membership status applies only to the selected partner. Existing access tokens retain their roles until refreshed. Changed emails follow the account service's email-verification policy.

## Configuration

Set PartnerAccounts:BaseUrl on the eByahe API to the account API origin, and PartnerMemberships:BaseUrl on the account API to the eByahe API origin. Development files use https://localhost:7017 and https://localhost:7170 respectively. Production must use HTTPS. No new service credentials are needed: the caller's bearer token is forwarded, redirects are disabled, and the account API independently verifies the membership through eByahe. Tokens must include both API audiences, as with other cross-service partner workflows.

Apply the TrackPartnerAccountUpdates migration before deploying the eByahe API. It adds a nullable LastAccountUpdateAtUtc to PartnerUsers for ordered, replay-safe projection. The local startup migrator applies pending migrations when enabled.

## Save and synchronization

GET /api/partner/users/{membershipId}/edit reads authoritative account details and current direct permissions. PUT /api/partner/users/{membershipId} forwards the bounded request to the account service. Required fields are requestId (GUID), firstName, lastName, email, isActive and roles.

The account service checks duplicate email, role dependencies and protected memberships. It serializes updates per account, updates Keycloak, and commits the account profile, UserHistories entry (including actor and permission snapshot) and a partner.member.updated outbox message in one database transaction. Failed saves attempt to restore the original identity and roles; restoration failure is reported for reconciliation. Reusing the same request ID and content returns the original acceptance; changed content with the same ID is rejected.

HTTP 202 means the account save and durable event are committed. Kafka delivers the event on the existing PartnerMembersTopic. eByahe applies the profile and partner membership, records the update timestamp, queues a partner-scoped notification, and publishes the users/dashboard live update. Older and repeated events do not overwrite newer membership state or create repeated notifications. The list can briefly show its previous state while Kafka processes the event. Keep both outbox and incoming workers running.

The account history records profile/permission changes; the eByahe incoming record and notification details preserve the membership transition, actor, time and original event. Existing temporary passwords are never included in update events.
