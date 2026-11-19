# Kafka topic setup

CodeDecodeTopics follows the shared schema: GUID Id, identity Code starting at 1 with a unique indexed alternate key, unique case-sensitive Decode (the Kafka topic name, at most 249 characters), CreatedBy default System with a RefUsers.ExternalUserId foreign key, and UTC DateTimeCreated.

OutboxMessages, Outgoing and Incoming reference Code through CodeDecodeTopic. Topic names remain unchanged in Kafka publishing, consumer receipts and compatibility properties. Incoming deduplication remains unique on (ConsumerGroup, CodeDecodeTopic, Partition, Offset). Foreign keys restrict removing referenced setup rows.

The migration seeds the five default eByahe topics and registers every additional legacy topic before replacing text columns. Rollback restores exact topic names. Runtime configuration overrides continue to work: valid names are registered once under System, in the same transaction as the message or receipt, with locks preventing duplicate registration. This creates a database lookup row, not a Kafka broker topic; broker auto-creation remains disabled.
