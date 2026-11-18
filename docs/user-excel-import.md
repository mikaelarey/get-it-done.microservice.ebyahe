# User Excel import

The partner Users page has Download template, Upload Excel and Add user. Recent requests is removed. Download and upload require member.create and the partner role, matching manual Add user.

## Workbook

Download User-import-template.xlsx from the Users page. The backend generates it; it is not a saved frontend asset. The Users sheet has First name, Last name and Email address. Enter one person per row, starting at row 2. All three fields are required. Permissions are not entered in the file. Bus operator users automatically receive booking.read, bus.read and terminal.read, plus the existing standard account roles and web partner role. Other partner categories receive only the applicable view permissions from this set. No write, confirm, scheduled-trip or user-management permissions are granted by an upload. Download a new template before uploading; older four-column templates are rejected. Manual Add user permission selection is unchanged.

The workbook uses normal 20-point data rows, a 24-point header and thin borders on every input cell through row 501. The header is frozen. Examples appear only on Instructions, leaving Users empty. Up to 500 rows and 5 MB are accepted. Only standard .xlsx files are supported; formulas, malformed workbook structures, extra data columns and Excel error/boolean values are rejected.

## Processing and tracking

The selected workspace must be approved and the uploader must be an active member. The whole file is validated before queueing requests. Missing fields, invalid email addresses, duplicate emails, existing active memberships and pending requests produce row-numbered errors; no requests from that file are queued when any row fails.

The upload attempt is stored in Uploads with UploadType Users. Accepted uploads create UserUploadBatches processing metadata and UploadMemberRequests links. No UploadId is added to RefUsers or other main record tables. Requests, encrypted temporary-password records and Kafka outbox entries are queued in one SQL transaction. HTTP 202 means accepted for asynchronous processing. Existing accounts keep their identity/profile/password and receive the default view roles (existing permissions are retained), matching manual Add user. New accounts receive a generated temporary password.

Each account-service reply creates or activates the membership and links its RefUser through UploadRecords in the same transaction. Requests cancelled after partner/membership changes do not create a record link. Once every request is completed or cancelled, one users.imported summary is queued for the successfully linked records. The notification reads {user} successfully created {count} users using file upload and links to /uploads/{uploadId}. Singular wording is used for one user. Zero successful records produce no creation notification. Replays do not create duplicate memberships, links or summary notifications.

The upload details page shows submitted, processing, completed and cancelled counts plus the completed records. It refreshes on live user events and offers Refresh progress while requests are pending. The upload modal also shows View upload after acceptance. Temporary passwords stay in user details and remain limited to the original uploader and the existing expiry/permission checks.

## APIs and deployment

GET /api/partner/users/template returns the workbook. POST /api/partner/users/import accepts multipart fields partnerId, uploadId (GUID) and file. The UI keeps uploadId for retries of the same submission and starts a new ID when the file or partner changes. Repeating an accepted submission with the same ID and normalized contents returns the original accepted result. Changed contents or a different actor cannot reuse an accepted upload ID.

Apply the AddUserUploadBatches EF migration before deploying the eByahe API. No account-service or notification-service changes are needed: the existing partner member provisioning and users.imported contracts are reused. Keep the account/eByahe Kafka consumers, outbox workers and live updates running. If downstream account provisioning is unavailable, requests remain Pending and retry through the existing worker.
