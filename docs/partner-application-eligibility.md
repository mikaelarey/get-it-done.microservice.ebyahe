# Partner application eligibility

An authenticated user can have one active application across all business categories. `Submitted` and `UnderReview` are active; `Approved` and `Rejected` (displayed as Declined) permit another submission. Ownership is the exact, case-sensitive JWT `sub`, not a supplied email or multipart field. Historical applications remain available.

`GET /api/partner-applications/eligibility` requires bearer authentication and returns a private, non-cacheable response:

```json
{
  "canSubmit": false,
  "activeApplication": {
    "id": "11111111-2222-3333-4444-555555555555",
    "trackingId": "APP-EXAMPLE",
    "status": "UnderReview"
  }
}
```

When eligible, `canSubmit` is true and `activeApplication` is null. The check covers every application, regardless of list pagination.

`POST /api/partner-applications` checks before processing documents and returns HTTP 409 with ProblemDetails code `active_partner_application` for an active application. A transaction-owned SQL application lock serializes submissions for the same creator across API instances. The filtered unique index `UX_PartnerApplications_ActiveCreator` is the final database safeguard. Application records, documents, initial approval history, JSON history, and notification outbox commit together. Losing concurrent requests clean up their own uncommitted uploads.

The submission page checks eligibility before exposing the form, links to an existing active application, and supports Refresh status after a decision. My applications shows New application only after a successful eligibility check. A stale form receiving a 409 checks eligibility again. A failed check keeps submission unavailable and offers retry.

Startup applies the `LimitActivePartnerApplications` migration. It adds the unique index without changing application statuses or deleting history. An existing database with multiple active applications for one non-null creator must have those applications reviewed before the migration can succeed; the migration does not make business decisions automatically. Check before deployment:

```sql
SELECT CreatedBy, COUNT(*) AS ActiveCount
FROM PartnerApplications
WHERE CreatedBy IS NOT NULL AND Status IN ('Submitted', 'UnderReview')
GROUP BY CreatedBy
HAVING COUNT(*) > 1;
```
