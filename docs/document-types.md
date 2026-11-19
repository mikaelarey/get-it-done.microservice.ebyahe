# Document types

CodeDecodeDocumentTypes follows the existing setup schema: GUID Id, identity Code starting at 1 with a unique indexed alternate key, unique Decode, CreatedBy (System by default; FK to RefUsers.ExternalUserId), and DateTimeCreated (UTC default).

| Code | Decode | Existing API identifier |
| --- | --- | --- |
| 1 | Registration | registration |
| 2 | BIR | bir |
| 3 | Permit | permit |
| 4 | Accreditation | accreditation |
| 5 | Bank Account Proof | bankAccountProof |

PartnerApplicationDocuments.CodeDecodeDocumentType references Code with restricted deletion. The unique (PartnerApplicationId, CodeDecodeDocumentType) index preserves one file per document type per application.

AddDocumentTypeCodeDecode backfills existing text values before dropping DocumentType. Files, metadata and historical JSON remain unchanged. Unknown legacy types abort the migration. Rollback restores the text identifiers and refuses types the previous schema cannot represent. The domain compatibility property and query interceptor preserve current API responses, filters and document upload fields.
