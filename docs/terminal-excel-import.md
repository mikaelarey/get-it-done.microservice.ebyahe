# Terminal Excel imports

On the partner **Terminals** page, use **Download template**, complete the
**Terminals** worksheet, then select **Upload Excel**. Choose the approved bus
operator partner that will own every terminal in the file.

The template is a static frontend asset at
`public/assets/templates/terminal-import-template.xlsx`. Downloading it makes no
API or database request and does not generate a workbook at runtime. It includes
17 regions, 82 provinces, 1,634 municipalities and 42,046 barangays from the
supplied geography seed files. Replace the published asset when the address setup
changes. Upload validation always uses the current database records.

## Worksheet

Keep the seven headers on row 1 unchanged:

| Column | Required value |
| --- | --- |
| Terminal name | 1–254 characters, unique for the partner and selected location |
| Region | Select a region option |
| Province | Select within the region |
| City / Municipality | Select within the province |
| Barangay | Select within the city/municipality |
| Street / Building address | 1–500 characters; street, building or nearest landmark |
| Location | Automatic `Province - City / Municipality` preview; not imported |

Choose the four address dropdowns from left to right. Options include a numeric
code and decoded name, such as `3 - Region I`. Keep the complete option. When a
parent selection changes, clear and reselect its descendants: ordinary Excel data
validation does not clear existing child values. The server rejects mismatched
hierarchies, including values pasted over dropdown validation.

Reference worksheets and the Instructions examples are not imported. The input
sheet starts blank and supports up to 500 terminals. Blank input rows are ignored,
including rows containing only the Location preview formula. Enter plain values
in the six input columns; formulas there are rejected. Location formulas and their
cached results are ignored. The backend computes the name from the database.

## API and database behavior

`POST /api/partner/terminals/import` accepts multipart fields `partnerId` and `file`.
Only non-empty `.xlsx` files up to 5 MB are accepted, with expanded-workbook size
and XML-part limits. The endpoint uses the existing terminal write policy and
checks active partner membership, approval and bus-operator category before
reading the file and again within the save transaction.

All codes, parent relationships, location availability, duplicate names and
required fields are checked before saving. Codes are authoritative; stored names
come from the current setup tables rather than spreadsheet labels. Duplicate
terminal names are checked within the file and against existing records.

Success returns `{ "importedCount": 2, "errors": [] }`. Invalid rows/files return
400; existing terminal conflicts return 409. Both include
`{ "importedCount": 0, "errors": [{ "row": 3, "field": "Province", "message": "..." }] }`.
Row numbers match Excel; row 0 identifies a file-level problem. All rows must pass.

The entire batch, newly created Locations and one `terminals.imported` summary
activity commit in one transaction. Each active member of the selected partner
receives one notification, such as `Test Five successfully created 20 terminals using file upload.`
Its link opens `/uploads/{uploadId}` to display that batch's linked records. The activity
details include `recordCount`. Disjoint audience envelopes keep large partner groups
within the Kafka payload limit without notifying a member twice. Manual creation
still uses `terminal.created`. Any failure leaves the batch unsaved and sends no notification.
Locations are created/reused by barangay with the same four geography references
as manual creation. Location locks are acquired in code order, and database unique
indexes protect concurrent submissions. Success refreshes the terminal list through
the existing record-change event and the page completion callback.

The `LinkLocationsToCodeDecodeGeography` migration defines the four code references
on `Locations`. Composite foreign keys enforce the complete parent chain, with an
all-or-none geography check and a unique index for linked barangays. Existing
locations retain null geography codes until explicitly linked. The displayed
location name is `Province - City / Municipality`, while the terminal Address holds
the street/building description. Terminal responses in the partner portal and
admin directory append any available barangay, city/municipality, province and
region to that description. Legacy records without geography retain their street
address and existing location label.

Location labels are saved in province-first order, for example `Pangasinan - Alcala`, for manual creation, updates, and Excel imports. The `FormatLocationNamesProvinceFirst` migration updates existing geography-linked locations, including inactive locations, without changing their IDs or terminal relationships. Unlinked legacy locations keep their existing labels.
