# Terminal updates

The partner Terminals table has an Actions column with View and Update buttons.
Update requires `terminal.read` and `terminal.write` and an active membership in
the terminal's approved bus operator partner. Readers retain View access.

`GET /api/partner/terminals/{id}` returns the editable terminal, its fixed
`partnerId`, raw street/building `address`, `isActive` and the four nullable
geography codes. This avoids copying the formatted full address into the street
field. It uses the terminal write policy and returns 404 for inaccessible records.

`PUT /api/partner/terminals/{id}` accepts `name`, `address`, `isActive` and
`codeDecodeRegion`, `codeDecodeProvince`, `codeDecodeMunicipality`,
`codeDecodeBarangay`. The partner cannot be reassigned. Names and addresses use
the creation rules; duplicate names at the same partner/location return 409.
The four codes must form a complete valid hierarchy and resolve to an active
location. A legacy terminal without geography may omit all four to retain its
existing location. A linked terminal cannot clear its geography.

The terminal's location reference changes when a new address is selected.
Other terminals sharing the previous location retain their references. The
terminal, any new location and one `terminal.updated` outbox activity commit
together. Failures roll back the complete change. Successful updates publish
terminal/scheduled-trip live refreshes, and the modal refreshes the terminal list.
The notification service displays `{user} updated a terminal.` to active members
of that partner, using the existing explicit-user audience policy.

This change needs no database migration. Deploy the updated notification consumer
before the eByahe API and frontend. CORS now permits PUT from configured origins.
