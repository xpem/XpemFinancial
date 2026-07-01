# Requirements Document

## Introduction

This feature introduces a stable `AccountId` (Guid) field to accounts across the XpemFinancial client and UniqueServer backend. The Guid is generated at creation time on the client and used as the canonical cross-device identifier, replacing the current reliance on `ExternalId` (server auto-increment PK) for sync matching.

The migration is incremental: `AccountId` coexists with `ExternalId` during a transition period. Unlike categories (which had no push flow before their Guid sync implementation), accounts already have a bidirectional push/pull flow. The existing push decides between POST (new) and PUT (update) based on whether `ExternalId` is null. This feature replaces the POST/PUT split with a single upsert-by-Guid approach on the server, simplifying the push logic and enabling deterministic cross-device matching.

The current push uses a cursor-based pending selection (`ExternalId == null OR UpdatedAt > lastSyncCursor`). With AccountId-based upsert, the server can safely handle re-pushes without creating duplicates, making the cursor-based approach optional for new records.

## Glossary

- **Client**: The XpemFinancial .NET MAUI application with a local SQLite database.
- **Server**: The UniqueServer/FinancialService .NET API with a MySQL database.
- **AccountId**: A stable Guid assigned to each account at creation time, used as the cross-device unique identifier.
- **ExternalId**: The existing nullable integer field on the client that stores the server's auto-increment Id after the first push.
- **Account_Service_Client**: The client-side service managing account CRUD, push, and pull logic.
- **Account_Service_Server**: The server-side service managing account creation, update, and retrieval.
- **Account_Repo_Client**: The client-side SQLite repository for accounts.
- **Account_Repo_Server**: The server-side MySQL repository for accounts.
- **Account_Api_Repo**: The client-side HTTP repository that communicates with the server's account endpoints.
- **Sync_Service**: The client-side singleton service responsible for periodic push/pull synchronization cycles.
- **Upsert**: An operation that inserts a record if it does not exist or updates it if it does, keyed by AccountId.

## Requirements

### Requirement 1: AccountId Field on Client DTO

**User Story:** As a developer, I want each account to carry a stable Guid from creation, so that it can be identified across devices without depending on the server's auto-increment Id.

#### Acceptance Criteria

1. THE Client SHALL include an `AccountId` property of type `Guid` on `AccountDTO`, initialized to `Guid.Empty` by default.
2. WHEN a new account is created locally (via CreateAsync), THE Account_Service_Client SHALL assign `Guid.NewGuid()` to `AccountId` if the current value is `Guid.Empty`, and SHALL preserve the existing value if it is not `Guid.Empty`.
3. WHEN an account is pulled from the server and the response contains a non-null, non-empty `AccountId`, THE Account_Service_Client SHALL persist that `AccountId` value to the corresponding local record.
4. THE Client SHALL store `AccountId` in a non-nullable column with a default value of `Guid.Empty` in the local SQLite database.

### Requirement 2: AccountId Field on Server DTO

**User Story:** As a developer, I want the server to store and return AccountId, so that all devices can identify the same account by Guid.

#### Acceptance Criteria

1. THE Server SHALL include an `AccountId` property of type `Guid?` (nullable) on the server-side `AccountDTO`.
2. THE Server SHALL store `AccountId` in a nullable column in the MySQL database with a composite unique index on (`AccountId`, `UserId`), allowing multiple NULL values but preventing duplicate non-null `AccountId` for the same user.
3. THE Server SHALL include `AccountId` in the account request model as a nullable `Guid?` field for backward compatibility with older clients.
4. THE Server SHALL include `AccountId` in `AccountRes` as a `Guid?` field so that pulling clients receive the identifier when present.
5. IF the request contains a non-null `AccountId` that is not `Guid.Empty`, THEN THE Server SHALL persist it on the account record.
6. IF the request contains an `AccountId` equal to `Guid.Empty`, THEN THE Server SHALL treat it as null and SHALL NOT persist `Guid.Empty` on the account record.
7. THE Server database migration SHALL generate and assign a unique Guid to every existing account record that has a NULL `AccountId`, so that all records have a valid identifier after deployment and any subsequent pull returns a non-empty `AccountId`.

### Requirement 3: Client Push with AccountId

**User Story:** As a user, I want my accounts to be pushed with their stable Guid, so that the server can identify them deterministically without relying on the POST/PUT split.

#### Acceptance Criteria

1. WHEN the Account_Service_Client builds an account push request, THE Account_Service_Client SHALL include the local `AccountId` (Guid) in the request payload.
2. WHEN the server responds successfully to an account push and the response body contains a server-side `Id` greater than 0, THE Account_Service_Client SHALL persist the returned `Id` as `ExternalId` in the local account record.
3. IF the server response indicates failure (network error, non-success status, or the response body is missing or contains an `Id` of 0), THEN THE Account_Service_Client SHALL leave the local account record unchanged so that it continues to match the pending-push selection criteria (`ExternalId == null` or `UpdatedAt > lastSyncCursor`) and is retried on the next sync cycle.
4. THE Account_Api_Repo SHALL include `AccountId` in the serialized request payload when calling POST or PUT account endpoints, ensuring the field is present in the HTTP request body sent to the server.

### Requirement 4: Server Upsert by AccountId

**User Story:** As a developer, I want the server to upsert by Guid, so that duplicate accounts from multiple devices or retry pushes are handled deterministically.

#### Acceptance Criteria

1. WHEN an account request contains a non-null `AccountId` (Guid) that is not `Guid.Empty`, THE Account_Service_Server SHALL query for an existing account matching both `AccountId` and the authenticated `UserId`.
2. WHEN an existing account is found by the `AccountId` and `UserId` lookup, THE Account_Service_Server SHALL update that record's mutable fields (`Name`, `Type`, `IncludeInGeneralBalance`, `Inactive`) from the request and SHALL set `UpdatedAt` to the current server UTC time.
3. WHEN no existing account is found by the `AccountId` and `UserId` lookup, THE Account_Service_Server SHALL insert a new record persisting the provided `AccountId` alongside the auto-increment `Id`.
4. WHEN an account request contains a null `AccountId` or `Guid.Empty`, THE Account_Service_Server SHALL insert a new record and generate a new Guid for its `AccountId` field before persisting.
5. WHEN the server performs an upsert (insert or update via `AccountId`), THE Account_Service_Server SHALL return both the server-side auto-increment `Id` and the persisted `AccountId` in the response body.
6. IF two concurrent requests arrive with the same `AccountId` and `UserId`, THEN THE Account_Service_Server SHALL enforce a unique database constraint on (`AccountId`, `UserId`) so that exactly one record exists for that pair, and the request that loses the race SHALL retry the lookup and perform an update instead of failing.
7. IF a database constraint violation occurs during insert due to a duplicate `AccountId`-`UserId` pair, THEN THE Account_Service_Server SHALL treat the operation as an update to the existing record rather than returning an error to the caller.

### Requirement 5: Client Pull with AccountId Matching

**User Story:** As a user, I want my device to match pulled accounts by Guid, so that records from other devices are correctly merged instead of duplicated.

#### Acceptance Criteria

1. WHEN the account response received during pull contains a non-null `AccountId` (Guid), THE Account_Service_Client SHALL first attempt to find the local record by matching the `AccountId` field.
2. WHEN a local record is found by `AccountId` and the pulled `UpdatedAt` is strictly greater than the local `UpdatedAt`, THE Account_Service_Client SHALL overwrite the local record's mutable fields with the pulled data and persist the pulled `UpdatedAt`.
3. WHEN a local record is found by `AccountId` and the pulled `UpdatedAt` is less than or equal to the local `UpdatedAt`, THE Account_Service_Client SHALL skip the update and leave the local record unchanged.
4. WHEN no local record is found by `AccountId`, THE Account_Service_Client SHALL fall back to matching by `ExternalId` (server-side integer PK) using the existing lookup and, if found, apply the same last-writer-wins comparison before updating.
5. WHEN no local record is found by either `AccountId` or `ExternalId`, THE Account_Service_Client SHALL insert a new local record persisting both the `AccountId` and the server-side `Id` (as `ExternalId`) from the response.
6. IF the account response received during pull contains a null or empty `AccountId`, THEN THE Account_Service_Client SHALL match by `ExternalId` only, preserving current behavior.

### Requirement 6: Sync Service Integration

**User Story:** As a user, I want the account push to continue using the existing sync cycle flow, enhanced with Guid-based upsert, so that accounts sync reliably without manual intervention.

#### Acceptance Criteria

1. WHEN the Sync_Service executes a sync cycle, THE Sync_Service SHALL invoke account push before account pull, maintaining the existing order.
2. WHEN the Account_Service_Client pushes pending accounts, THE Account_Service_Client SHALL select accounts using the existing cursor-based logic (accounts with `ExternalId == null` or `UpdatedAt > lastSyncCursor`).
3. WHEN an account push succeeds and the server returns a valid `ExternalId` (greater than 0), THE Account_Service_Client SHALL persist the `ExternalId` on the local record so that the push type switches from POST to PUT in subsequent cycles.
4. IF an account push fails for a specific record (any exception during the HTTP call or response processing), THEN THE Account_Service_Client SHALL continue pushing the remaining records without aborting the entire push batch.

### Requirement 7: Backward Compatibility

**User Story:** As a developer, I want the system to remain functional for existing accounts that lack an AccountId, so that no data loss or sync disruption occurs during the rollout.

#### Acceptance Criteria

1. WHILE `AccountId` is `Guid.Empty` on a local account record, THE Account_Service_Client SHALL use `ExternalId` as the matching key for pull sync operations, following the same logic applied before `AccountId` was introduced.
2. WHEN the server receives an account request that does not include an `AccountId` field (or includes null), THE Account_Service_Server SHALL process it using the existing create or update flow without returning an error.
3. WHEN the Account_Service_Client pulls an account whose response payload contains a non-empty `AccountId`, THE Account_Service_Client SHALL write that `AccountId` value to the local record matched by `ExternalId` within the same pull operation.
4. WHEN the local database schema is created or upgraded to include the `AccountId` column, THE Client SHALL assign a default value of `Guid.Empty` to all existing rows so that no existing record is left with a null `AccountId`.
5. WHILE `AccountId` is `Guid.Empty` on a local record AND the record has a non-null `ExternalId`, THE Account_Service_Client SHALL continue using the existing POST/PUT decision logic (POST when `ExternalId` is null, PUT when `ExternalId` is non-null) for pushing that record.
6. WHILE `AccountId` is `Guid.Empty` on a local record AND `ExternalId` is null, THE Account_Service_Client SHALL push the record using POST, and upon a successful response containing a non-empty `AccountId`, SHALL persist both the returned `ExternalId` and `AccountId` to the local record.

### Requirement 8: Client Repository Lookup by AccountId

**User Story:** As a developer, I want a repository method to find accounts by AccountId, so that the service layer can perform Guid-based matching efficiently.

#### Acceptance Criteria

1. THE Account_Repo_Client SHALL expose a `GetByAccountIdAsync(Guid accountId)` method that returns an `AccountDTO?`.
2. WHEN queried with an `AccountId` that exists in the local database, THE Account_Repo_Client SHALL return the matching record.
3. WHEN queried with an `AccountId` that does not exist in the local database, THE Account_Repo_Client SHALL return null.
4. IF `GetByAccountIdAsync` is called with `Guid.Empty`, THEN THE Account_Repo_Client SHALL return null without querying the database.

### Requirement 9: Server Repository Lookup by AccountId

**User Story:** As a developer, I want a server repository method to find accounts by AccountId and UserId, so that the upsert logic can be implemented efficiently.

#### Acceptance Criteria

1. THE Account_Repo_Server SHALL expose a `FindByAccountIdAsync(Guid accountId, int userId)` method that returns an `AccountDTO?`.
2. WHEN queried with an `AccountId` and `UserId` that match an existing record, THE Account_Repo_Server SHALL return the full `AccountDTO` for that record regardless of the record's `Inactive` status.
3. WHEN queried with an `AccountId` that does not exist for the given user, THE Account_Repo_Server SHALL return null.
4. IF `accountId` is `Guid.Empty`, THEN THE Account_Repo_Server SHALL return null without querying the database.
5. THE Account_Repo_Server SHALL enforce a composite unique index on (`AccountId`, `UserId`) in the database schema to guarantee lookup correctness and efficiency.

### Requirement 10: Client Assigns AccountId on Creation

**User Story:** As a user, I want accounts I create on my device to have a stable identifier immediately, so that they can be synced reliably regardless of network availability.

#### Acceptance Criteria

1. WHEN an account is created via the UI (AccountEditVM or equivalent), THE Account_Service_Client SHALL assign `Guid.NewGuid()` to `AccountId` before persisting to the local database.
2. WHEN an account is inserted locally with `AccountId` equal to `Guid.Empty`, THE Account_Service_Client SHALL assign `Guid.NewGuid()` to `AccountId` before persisting.
3. IF CreateAsync is called with an `AccountId` that is not `Guid.Empty`, THEN THE Account_Service_Client SHALL preserve the provided `AccountId` without generating a new one.
4. THE Account_Service_Client SHALL assign the AccountId in the service layer (CreateAsync and any other account-creation path such as EnsureDefaultAccountAsync) so that every locally created account receives a non-empty `AccountId` before persistence.
5. WHEN an `AccountId` has been assigned and persisted for a local account record, THE Account_Service_Client SHALL NOT overwrite it with a different value on subsequent local updates.
