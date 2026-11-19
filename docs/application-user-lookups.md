# Application and user setup values

CodeDecodeEntityTypes, CodeDecodeBanks, CodeDecodeSettlementFrequencies and CodeDecodeRoles follow the shared setup schema: GUID Id, identity Code starting at 1 with a unique indexed alternate key, unique Decode, CreatedBy (System for migration rows; FK to RefUsers.ExternalUserId), and UTC DateTimeCreated.

PartnerApplications references Code through CodeDecodeEntityType, CodeDecodeBank and CodeDecodeSettlementFrequency. RefUsers references CodeDecodeRoles.Code through CodeDecodeRole (default 1, Normal; 2 is Administrator). Role authorization continues to use the validated Keycloak roles as before.

The banks migration includes the user's 28 supplied bank entries and eight existing short bank names. Existing labels are preserved; matching short/full names are not silently merged. Any additional bank names found in legacy applications are registered during migration under System. Empty legacy bank names use a null FK. New submissions require a configured bank and reject other text before document uploads. GET /api/partner-applications/banks returns authenticated database options, and the form renders a dropdown. Bank lookups use database values, so configured additions need no frontend deployment.

Compatibility properties preserve the existing entity type, settlement frequency and role API values. The migration backfills before dropping the text columns and restores them on rollback without modifying account numbers, user profiles, or historical JSON.
