# Requirements Document

## Introduction

This feature introduces a stable `TransactionId` (Guid) field to transactions across the XpemFinancial client and UniqueServer backend. The Guid is generated at creation time on the client and used as the canonical cross-device identifier, replacing the fragile heuristic-based deduplication (`FindDuplicateAsync` with a 5-minute window) and eliminating the sync loop caused by recurring-rule-generated transactions that fail to persist their `ExternalId`.

The migration is incremental: `TransactionId` coexists with `ExternalId` during a transition period, after which `ExternalId` can be deprecated.

## Glossary

- **Client**: The XpemFinancial .NET MAUI application with a local SQLite database.
- **Server**: The UniqueServer/FinancialService .NET API with a MySQL database.
- **TransactionId**: A stable Guid assigned to each transaction at creation time, used as the cross-device unique identifier.
- **ExternalId**: The existing nullable integer field on the client that stores the server's auto-increment Id after the first push.
- **Sync_Service**: The client-side singleton service responsible for periodic push/pull synchronization cycles.
- **Transaction_Service_Client**: The client-side service managing transaction CRUD and push/pull logic.
- **Transaction_Service_Server**: The server-side service managing transaction creation, update, and duplicate detection.
- **Transaction_Repo_Client**: The client-side SQLite repository for transactions.
- **Transaction_Repo_Server**: The server-side MySQL repository for transactions.
- **Recurring_Scheduler**: The client-side scheduler that generates recurring-rule occurrences as pending transactions.
- **Upsert**: An operation that inserts a record if it does not exist or updates it if it does, keyed by TransactionId.

## Requirements

### Requirement 1: TransactionId Field on Client DTO

**User Story:** As a developer, I want each transaction to carry a stable Guid from creation, so that it can be identified across devices without depending on the server's auto-increment Id.

#### Acceptance Criteria

1. THE Client SHALL include a `TransactionId` property of type `Guid` on `TransactionDTO`.
2. WHEN a new transaction is created locally (via AddAsync or AddOccurrenceAsync), THE Transaction_Service_Client SHALL assign a new Guid to `TransactionId` if it is `Guid.Empty`.
3. WHEN a transaction is pulled from the server, THE Transaction_Service_Client SHALL persist the `TransactionId` received from the API response.
4. THE Client SHALL store `TransactionId` in a non-nullable unique-indexed column in the local SQLite database.

### Requirement 2: TransactionId Field on Server DTO

**User Story:** As a developer, I want the server to store and return TransactionId, so that all devices can identify the same transaction by Guid.

#### Acceptance Criteria

1. THE Server SHALL include a `TransactionId` property of type `Guid` on the server-side `TransactionDTO`.
2. THE Server SHALL store `TransactionId` in a non-nullable unique-indexed column in the MySQL database.
3. THE Server SHALL include `TransactionId` in `TransactionReq` as a nullable `Guid?` field for backward compatibility with older clients.
4. THE Server SHALL include `TransactionId` in `TransactionRes` as a non-nullable `Guid` field so that pulling clients always receive the identifier.
5. IF `TransactionReq.TransactionId` is null, THEN THE Server SHALL generate a new `Guid` and assign it as the transaction's `TransactionId` before persisting.
6. THE Server database migration SHALL generate and assign a unique `Guid` to every existing transaction record that has a NULL `TransactionId`, so that all records have a valid identifier after deployment and any subsequent pull returns a non-empty `TransactionId`.

### Requirement 3: Client Push with TransactionId

**User Story:** As a user, I want my transactions to be pushed with their stable Guid, so that the server can identify them without heuristic matching.

#### Acceptance Criteria

1. WHEN the Transaction_Service_Client builds a `TransactionReq` for a POST or PUT push, THE Transaction_Service_Client SHALL include the local `TransactionId` (Guid) in the request payload as the `TransactionId` field.
2. WHEN the server responds successfully to a POST or PUT with a body containing an `ExternalId`, THE Transaction_Service_Client SHALL persist the returned `ExternalId` (server auto-increment Id) in the local transaction record.
3. IF the server response does not contain an `ExternalId` (e.g., server unavailable), THEN THE Transaction_Service_Client SHALL retain the transaction in a pending-sync state so it can be retried on the next sync cycle.

### Requirement 4: Server Upsert by TransactionId

**User Story:** As a developer, I want the server to upsert by Guid, so that duplicate transactions from multiple devices or retry pushes are handled deterministically.

#### Acceptance Criteria

1. WHEN a `TransactionReq` contains a non-null `TransactionId` (Guid), THE Transaction_Service_Server SHALL query for an existing transaction matching both `TransactionId` and the authenticated `UserId`.
2. WHEN an existing transaction is found by the `TransactionId` and `UserId` lookup, THE Transaction_Service_Server SHALL update that record's mutable fields from the request instead of inserting a new one, and SHALL set `UpdatedAt` to the current server UTC time.
3. WHEN no existing transaction is found by the `TransactionId` and `UserId` lookup, THE Transaction_Service_Server SHALL insert a new record persisting the provided `TransactionId` as a stored column alongside the auto-increment `Id`.
4. WHEN a `TransactionReq` contains a null `TransactionId`, THE Transaction_Service_Server SHALL fall back to the existing `FindDuplicateAsync` heuristic for deduplication.
5. WHEN the server performs an upsert (insert or update via `TransactionId`), THE Transaction_Service_Server SHALL return the server-side auto-increment `Id` in the response body, preserving the current POST response contract.
6. IF two concurrent requests arrive with the same `TransactionId` and `UserId`, THEN THE Transaction_Service_Server SHALL ensure exactly one record exists for that `TransactionId`-`UserId` pair by enforcing a unique database constraint on (`TransactionId`, `UserId`).
7. THE Transaction_Service_Server SHALL store `TransactionId` as a nullable Guid column on the transaction table, indexed with a unique constraint scoped to `UserId`.

### Requirement 5: Client Pull with TransactionId Matching

**User Story:** As a user, I want my device to match pulled transactions by Guid, so that records from other devices are correctly merged instead of duplicated.

#### Acceptance Criteria

1. WHEN the `TransactionRes` received during pull contains a non-null `TransactionId` (Guid), THE Transaction_Service_Client SHALL first attempt to find the local record by matching the `TransactionId` field.
2. WHEN a local record is found by `TransactionId` and the local record's `SyncStatus` is not `Pushing`, THE Transaction_Service_Client SHALL update it using last-writer-wins logic: apply the pulled data only if the pulled `UpdatedAt` is strictly greater than the local `UpdatedAt`.
3. WHEN a local record is found by `TransactionId` but its `SyncStatus` is `Pushing`, THE Transaction_Service_Client SHALL skip the update and retain the local record unchanged.
4. WHEN no local record is found by `TransactionId`, THE Transaction_Service_Client SHALL fall back to matching by `ExternalId` (server-side integer PK) using the existing lookup.
5. WHEN no local record is found by either `TransactionId` or `ExternalId`, THE Transaction_Service_Client SHALL insert a new local record with `SyncStatus` set to `Synced`.
6. IF the `TransactionRes` received during pull contains a null `TransactionId`, THEN THE Transaction_Service_Client SHALL match by `ExternalId` only, preserving current behavior.

### Requirement 6: Recurring Scheduler Generates TransactionId

**User Story:** As a user, I want recurring occurrences to carry a deterministic Guid from generation, so that the same occurrence generated on two devices is recognized as identical.

#### Acceptance Criteria

1. WHEN the Recurring_Scheduler generates an occurrence via `BuildOccurrence`, THE Recurring_Scheduler SHALL assign a deterministic `TransactionId` of type `Guid` derived exclusively from the `RecurringRuleId` (Guid) and the occurrence date (date-only, ignoring time component).
2. THE Recurring_Scheduler SHALL use a deterministic derivation method (e.g., GuidV5 with a fixed namespace or a SHA-256–based approach truncated to 16 bytes) so that any device generating the same `RecurringRuleId` + occurrence-date combination produces a byte-identical `TransactionId`.
3. WHEN an occurrence already exists locally with the same `TransactionId`, THE Recurring_Scheduler SHALL skip generation of that occurrence without modifying the existing record.
4. IF the `RecurringRuleId` is empty (`Guid.Empty`) or the occurrence date is `default(DateTime)`, THEN THE Recurring_Scheduler SHALL not generate an occurrence for that input and SHALL skip to the next scheduled date.

### Requirement 7: Eliminate Sync Loop for Recurring Occurrences

**User Story:** As a user, I want recurring transactions to stop being re-pushed every sync cycle, so that my sync performance is stable and no spurious server entries are created.

#### Acceptance Criteria

1. WHEN a recurring occurrence is pushed and the server responds with a valid `ExternalId` (greater than 0), THE Transaction_Service_Client SHALL persist the `ExternalId` on the local record within the same database operation that sets `SyncStatus` to `Synced`, ensuring the entity is attached to the active database context before saving.
2. WHEN a recurring occurrence already exists on the server (matched by `TransactionId`), THE Transaction_Service_Server SHALL return the existing record's Id instead of creating a duplicate.
3. WHEN a recurring occurrence is successfully pushed (server returned a valid `ExternalId` greater than 0), THE Transaction_Service_Client SHALL set the local `SyncStatus` to `Synced` so that `GetPendingPushAsync` excludes the record from subsequent sync cycles.
4. IF the server responds with `ExternalId` equal to 0 (server unavailable), THEN THE Transaction_Service_Client SHALL keep the local `SyncStatus` as `Pending` so the occurrence is retried on the next sync cycle without data loss.
5. IF persisting the `ExternalId` to the local database fails after a successful server push, THEN THE Transaction_Service_Client SHALL keep the local `SyncStatus` as `Pending` so the next sync cycle re-pushes the occurrence and the server deduplication logic returns the existing record's Id.

### Requirement 8: Backward Compatibility

**User Story:** As a developer, I want the system to remain functional for existing transactions that lack a TransactionId, so that no data loss or sync disruption occurs during the rollout.

#### Acceptance Criteria

1. WHILE `TransactionId` is `Guid.Empty` on a local transaction record, THE Transaction_Service_Client SHALL use `ExternalId` as the matching key for push and pull sync operations, following the same logic applied before `TransactionId` was introduced.
2. WHEN the server receives a sync request that does not include a `TransactionId` field (or includes `Guid.Empty`), THE Transaction_Service_Server SHALL process it using the existing insert-and-heuristic-dedup flow without returning an error.
3. WHEN the Transaction_Service_Client pulls a transaction whose response payload contains a non-empty `TransactionId` (not `Guid.Empty`), THE Transaction_Service_Client SHALL write that `TransactionId` value to the matching local record's `TransactionId` column within the same pull operation.
4. IF the Transaction_Service_Client pulls a transaction with a `TransactionId` but no matching local record is found by `ExternalId`, THEN THE Transaction_Service_Client SHALL insert the transaction as a new local record with the `TransactionId` value populated.
5. WHEN the local database schema is created or upgraded to include the `TransactionId` column, THE Client SHALL assign a default value of `Guid.Empty` to all existing rows so that no existing record is left with a null `TransactionId`.
6. WHILE `TransactionId` is `Guid.Empty` on a local record AND the record has a valid `ExternalId`, THE Transaction_Service_Client SHALL NOT treat the record as unsynced or trigger a duplicate push solely because `TransactionId` is empty.

### Requirement 9: Client Repository Lookup by TransactionId

**User Story:** As a developer, I want a repository method to find transactions by TransactionId, so that the service layer can perform Guid-based matching efficiently.

#### Acceptance Criteria

1. THE Transaction_Repo_Client SHALL expose a `GetByTransactionIdAsync(Guid transactionId)` method that returns a `TransactionDTO?`.
2. WHEN queried with a `TransactionId` that exists in the local database, THE Transaction_Repo_Client SHALL return the first matching record including inactive records.
3. WHEN queried with a `TransactionId` that does not exist in the local database, THE Transaction_Repo_Client SHALL return null.
4. IF `GetByTransactionIdAsync` is called with `Guid.Empty`, THEN THE Transaction_Repo_Client SHALL return null without querying the database.

### Requirement 10: Server Repository Lookup by TransactionId

**User Story:** As a developer, I want a server repository method to find transactions by TransactionId and UserId, so that the upsert logic can be implemented efficiently.

#### Acceptance Criteria

1. THE Transaction_Repo_Server SHALL expose a `FindByTransactionIdAsync(Guid transactionId, int userId)` method that returns a `TransactionDTO?`.
2. WHEN queried with a `TransactionId` and `UserId` that match an existing record, THE Transaction_Repo_Server SHALL return that record regardless of the record's `Inactive` status.
3. WHEN queried with a `TransactionId` that does not exist for the given user, THE Transaction_Repo_Server SHALL return null.
4. IF `transactionId` is `Guid.Empty`, THEN THE Transaction_Repo_Server SHALL return null without querying the database.
5. THE Transaction_Repo_Server SHALL use a composite index on `(TransactionId, UserId)` for lookup.
