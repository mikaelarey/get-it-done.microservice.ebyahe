# Partner application review

`GET /api/admin/partner-applications` reads `PartnerApplications` for the admin
Applications page. It is separate from `/api/partner-applications`, whose customer
list/details/document endpoints continue checking the authenticated owner.

The admin endpoint requires a signed, unexpired access token with the configured
eByahe issuer and audience, plus **both** exact client roles:

- `resource_access["get-it-done-web"].roles`: `admin`
- `resource_access["get-it-done-ebyahe-api"].roles`: `partner.review`

An invalid/missing token returns 401; a valid token missing either permission
returns 403. Realm roles, Account profile roles, and similarly named roles on
other clients do not grant access. Responses use `Cache-Control: no-store`.

Example:

```text
GET /api/admin/partner-applications?page=1&pageSize=5&search=Manila&category=bus-operator&status=Submitted
Authorization: Bearer <access token>
```

| Parameter | Meaning |
| --- | --- |
| `page` | 1–1,000,000; default 1 |
| `pageSize` | 1–50; default 5 |
| `search` | Optional trimmed substring, at most 254 characters, across business name, tracking reference, representative name/email and address |
| `category` | Optional exact database business category, at most 32 characters |
| `status` | Optional exact database application status, at most 32 characters |

Queries use SQL Server's configured text collation and parameterized EF queries.
Results sort by submission time descending, then GUID descending for stable ties.
Filtering and pagination run in SQL rather than loading all applications in the
browser. Invalid paging/oversized filters return 400; unmatched filters return an
empty successful page. No data is seeded by this endpoint, and no migration is needed.

The response contains:

- `items`: GUID, tracking ID, business name/category, address, representative
  name/email, submission timestamp and actual application status;
- `totalCount`: number matching the current filters, plus `page`/`pageSize`;
- `summary`: global `totalApplications`, `awaitingReview` (`Submitted` plus
  `UnderReview`) and `approved` (`Approved`) counts;
- `categories` and `statuses`: distinct values across all applications, so
  filters remain available when the current query returns no matches.

All applicants' submissions are included, even older rows without `CreatedBy`.
The list projects only its displayed fields: payout accounts, tax IDs, phone
numbers, document/storage references and full application records are excluded.

Each list row links to `/application-details/:id` in the admin shell. The admin
page reuses the customer details layout and preview modal with reviewer access:

- `GET /api/admin/partner-applications/{id}` reads any applicant's submitted
  details, including document metadata and a masked payout account number.
- `GET /api/admin/partner-applications/{id}/documents/{documentId}` previews or
  downloads a document belonging to that application, including bank account
  proof. Private storage bytes are verified against the saved length and SHA256;
  missing application/document returns 404, unavailable or corrupt storage 503.
  Content supports byte ranges and uses `private, no-store` and `nosniff` headers.
- `POST /api/admin/partner-applications/{id}/review` starts a fresh `Submitted`
  application's review and saves `UnderReview` (displayed as **Under Review**).
- `POST /api/admin/partner-applications/{id}/approve` saves `Approved` with
  optional remarks.
- `POST /api/admin/partner-applications/{id}/decline` saves `Rejected`, the existing
  database status used by the Decline action. Nonblank remarks are required.

Approve and Decline accept a JSON body such as
`{"remarks":"Please submit a valid business permit."}`. Remarks are trimmed,
limited to 2,000 characters, and saved in `PartnerApplicationApprovals.Remarks`
with the decision. Missing/blank decline remarks or oversized remarks return 400
without changing the status or adding an audit record. Approval may omit its body
or remarks. Review takes no body.

All routes require both reviewer roles. Approve and Decline are accepted only
from `UnderReview`; calling either directly on a fresh submission returns 409.
The page shows only Review for `Submitted`, then expands the same review container
with a full-width remarks textarea and Approve/Decline buttons for `UnderReview`. Finalized
applications have no review actions. Status, the immutable approval record (including
the authenticated reviewer's subject), and the application history snapshot
commit in one transaction. Concurrent decisions are serialized per application;
repeating the same decision returns the current details without another audit
entry, while the opposite decision returns 409. Missing applications return 404.
The customer page continues to show the saved status without review actions and
retains its owner-only API checks.

Each successful review transition also queues an applicant-only notification on
`Kafka:NotificationTopic` (default `get-it-done.notifications.v1`):

| Saved status | Event | Message |
| --- | --- | --- |
| `UnderReview` | `partner-application.under-review` | Started reviewing a partner application. |
| `Approved` | `partner-application.approved` | Approved a partner application. |
| `Rejected` | `partner-application.declined` | Declined a partner application. |

The reviewer remains `CreatedBy` and the Kafka key, while an explicit `User`
audience targets the original application's `CreatedBy`. Neither the reviewer nor
the administrator group is implicitly added to these status notifications.
Links are relative customer detail paths `/partner-application-details/{id}`.
The event ID is the immutable approval entry's GUID. Status, approval, history,
and notification outbox insertion commit together; retries and conflicting
decisions add no event. Kafka delivery retries use the existing outbox worker,
and Notification deduplicates redelivery. Older applications without an authenticated
creator cannot receive a user notification until their ownership is resolved.
The compact payload includes only the application ID/reference and old/new
statuses; remarks and private bank/document data remain in eByahe.

Approval also creates the `Partners` record with `OwnerUserId` set to the authenticated
application creator and adds that owner's active `PartnerUsers` membership. An exact
local RefUser is required; missing references return 409 with synchronization guidance.
The partner, approval, history and `partner.approved` outbox message commit together.
The existing unique application link prevents duplicate partners on retries.
The new `ExpandPartnerDisplayName` migration preserves business names up to 255 characters.

The role event goes to `Kafka:PartnerApprovalTopic` (default
`get-it-done.ebyahe.partners.v1`), keyed by the owner ID. Account consumes it to add
web `admin`, eByahe `booking.confirm`, `booking.read`, `member.create`, and the
appropriate `bus.read`/`bus.write` or `hotel.read`/`hotel.write` roles in Keycloak.
The approval response confirms the queued event; role assignment is asynchronous
and appears in browser sessions after token refresh. Decline never creates a partner
or role event. Kafka failures retry through the existing outbox worker.

SQL/HTTP tests cover current and legacy applicants, empty tables, filtering,
pagination, counts/options, minimal response fields, unchanged customer ownership
checks, private document integrity/ranges, review-start and decision audit entries,
required/optional remarks and length validation, concurrent review starts and
decisions, JWT validation and exact client-role requirements.
