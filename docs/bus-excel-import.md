# Bus imports

On the partner Buses page, select **Download template**, fill the **Buses** sheet,
then choose **Upload Excel**, select an approved bus operator workspace and import
the saved `.xlsx` file. The Instructions sheet contains examples which are not imported.

Keep row 1 unchanged, in this order:

| Column | Validation |
| --- | --- |
| Fleet number | Required, up to 64 characters, unique within the selected partner |
| Plate number | Required, up to 20 characters, letters/digits separated by spaces or hyphens, unique across all buses |
| Bus class | Required, up to 100 characters; must match a Decode in CodeDecodeBusClasses |
| Seat capacity | Whole number from 1 to 100 |

The importer trims values, capitalizes fleet/plate numbers, ignores blank rows,
and rejects formulas, Excel errors, duplicate values and malformed workbooks.
Each file accepts up to 500 buses and 5 MB. The partner is selected in the portal,
not supplied in spreadsheet cells. New buses are active.

`POST /api/partner/buses/import` accepts multipart form fields `partnerId` and `file`.
It requires the partner client role plus the eByahe `bus.read` and `bus.write`
permissions, an active membership and an approved bus operator application.
Permissions and membership are checked again inside the database transaction.

Success returns `{ "importedCount": 2, "errors": [] }`. Invalid files/rows return
400; existing fleet/plate conflicts return 409. Both report
`{ "importedCount": 0, "errors": [{ "row": 3, "field": "Plate number", "message": "..." }] }`.
Row numbers match Excel; row 0 identifies a file-level problem. Permission failures return 403.

All rows and one `buses.imported` summary activity commit together in
one transaction. Each active member of the selected partner receives one notification,
such as `Test Five successfully created 20 buses using file upload.` The activity
details include `recordCount` and `uploadId`; the link opens `/uploads/{uploadId}`.
Disjoint audience envelopes keep large partner groups within the Kafka payload limit
without notifying a member twice. Manual creation still uses `bus.created`.
Any error leaves the entire batch unsaved and sends no notification. Unique database
indexes also protect concurrent imports. Successful imports refresh the fleet
through the existing live update mechanism and the page's completion callback.
The `AddCodeDecodeBusClasses` migration seeds the ten template classes and
preserves other existing bus classes. Buses reference the unique indexed
`CodeDecodeBusClasses.Code` through `Buses.CodeDecodeBusClass`.
The Add bus dropdown loads `GET /api/partner/buses/classes` from this table.

Database uniqueness is enforced for `PlateNumber` globally, `(PartnerId, FleetNumber)`,
and `(PartnerId, FleetNumber, PlateNumber)`. The `AddBusPartnerFleetPlateUniqueness`
migration adds the explicit three-column unique index without changing existing bus
records. Duplicate manual submissions return 409, and Excel imports report the
affected rows without saving any buses in that upload.

`GET /api/partner/buses/template` requires the same bus read/write permissions as
imports. Each request reads the current `CodeDecodeBusClasses` entries and generates
an Excel workbook in memory. The class dropdown and Instructions sheet reflect the
latest setup values. Responses use `Cache-Control: no-store`.

The workbook layout is an embedded backend resource at
`EByahe.Application/Assets/Templates/bus-import-template.xlsx`. The frontend requests
the generated file with its access token and downloads the returned blob. Generated
files are not saved on the server or served from frontend assets.
