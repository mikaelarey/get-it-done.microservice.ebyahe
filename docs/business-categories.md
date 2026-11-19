# Business categories

CodeDecodeBusinessCategories follows the shared setup schema: GUID Id, identity Code starting at 1, unique Decode, CreatedBy (default System and foreign key to RefUsers.ExternalUserId), and DateTimeCreated (UTC default). The unique alternate key indexes Code.

Seeded values: 1 = Bus Operator; 2 = Hotel. PartnerApplications and PartnerMemberRequests reference Code through CodeDecodeBusinessCategory. Both foreign keys restrict deletes. The former BusinessCategory text columns are converted by AddBusinessCategoryCodeDecode before removal. Unknown legacy values abort and roll back the migration rather than assigning a different category. Rollback restores the original text values and refuses categories the older schema cannot represent.

API requests, responses, role selection and Kafka messages retain the identifiers bus-operator and hotel for compatibility. The domain compatibility property and query interceptor map these identifiers to codes. Setup Decode values are display labels. Supporting a new category also requires its onboarding and authorization behavior to be implemented; inserting a setup row alone does not enable it.
