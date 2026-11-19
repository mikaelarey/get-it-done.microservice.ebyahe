# Event types

CodeDecodeEventTypes uses the standard setup schema: GUID Id, identity Code starting at 1, a unique indexed Code alternate key, unique Decode (the exact event name), CreatedBy defaulting to System with a RefUsers.ExternalUserId foreign key, and UTC DateTimeCreated.

OutboxMessages and the append-only Outgoing delivery history reference Code via CodeDecodeEventType. Foreign keys restrict deleting referenced setup rows. The migration backfills all existing types before removing the text columns, and aborts transactionally if an unmapped name exists. Rollback restores the exact names from the lookup table. Payloads, topics, keys, message IDs and delivery state are not rewritten.

The setup seeds all 20 event types currently emitted by eByahe. Domain compatibility properties and the query interceptor preserve event-name comparisons and Kafka headers/payload contracts. New event producers must add their stable code, setup seed/migration and compatibility mapping together. Receiving an external event does not require adding its type unless eByahe persists it as a typed outbox event.
