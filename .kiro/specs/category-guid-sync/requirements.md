# Requirements Document

## Introduction

This feature introduces a stable `CategoryId` (Guid) field to categories across the XpemFinancial client and UniqueServer backend. The Guid is generated at creation time on the client and used as the canonical cross-device identifier, replacing the current reliance on `ExternalId` (server auto-increment PK) for sync matching.

The migration is incremental: `CategoryId` coexists with `ExternalId` during a transition period. Categories are simpler than transactions — no recurring rules, no installments, no SyncStatus state machine. The core sync pattern is: push new/updated categories to server, pull server categories to client, match by Guid to avoid duplicates.

Currently, categories only have a pull flow (server → client). This feature also introduces a push flow (client → server) so that categories created on the client can be synced to the server and across devices.

## Glossary

- **Client**: The XpemFinancial .NET MAUI application with a local SQLite database.
- **Server**: The UniqueServer/FinancialService .NET API with a MySQL database.
- **CategoryId**: A stable Guid assigned to each category at creation time, used as the cross-device unique identifier.
- **ExternalId**: The existing nullable integer field on the client that stores the server's auto-increment Id after the first push.
- **Category_Service_Client**: The client-side service managing category CRUD, push, and pull logic.
- **Category_Service_Server**: The server-side service managing category creation, update, and retrieval.
- **Category_Repo_Client**: The client-side SQLite repository for categories.
- **Category_Repo_Server**: The server-side MySQL repository for categories.
- **Category_Api_Repo**: The client-side HTTP repository that communicates with the server's category endpoints.
- **Sync_Service**: The client-side singleton service responsible for periodic push/pull synchronization cycles.
- **Upsert**: An operation that inserts a record if it does not exist or updates it if it does, keyed by CategoryId.

## Requirements

### Requirement 1: CategoryId Field on Client DTO

**User Story:** As a developer, I want each category to carry a stable Guid from creation, so that it can be identified across devices without depending on the server's auto-increment Id.

#### Acceptance Criteria

1. THE Client SHALL include a `CategoryId` property of type `Guid` on `CategoryDTO`.
2. WHEN a new category is created locally (via AddLocalAsync), THE Category_Service_Client SHALL assign a new Guid to `CategoryId` if it is `Guid.Empty`.
3. WHEN a category is pulled from the server, THE Category_Service_Client SHALL persist the `CategoryId` received from the API response.
4. THE Client SHALL store `CategoryId` in a non-nullable column with a default value of `Guid.Empty` in the local SQLite database.

### Requirement 2: CategoryId Field on Server DTO

**User Story:** As a developer, I want the server to store and return CategoryId, so that all devices can identify the same category by Guid.

#### Acceptance Criteria

1. THE Server SHALL include a `CategoryId` property of type `Guid?` (nullable) on the server-side `TransactionCategoryDTO`.
2. THE Server SHALL store `CategoryId` in a nullable unique-indexed column in the MySQL database, scoped to `UserId`.
3. THE Server SHALL include `CategoryId` in the category request model as a nullable `Guid?` field for backward compatibility with older clients.
4. THE Server SHALL include `CategoryId` in `TransactionCategoryRes` as a `Guid?` field so that pulling clients receive the identifier when present.
5. IF the request contains a non-null `CategoryId`, THEN THE Server SHALL persist it on the category record.
6. THE Server database migration SHALL generate and assign a unique Guid to every existing category record that has a NULL `CategoryId`, so that all records have a valid identifier after deployment and any subsequent pull returns a non-empty `CategoryId`.

### Requirement 3: Client Push with CategoryId

**User Story:** As a user, I want my categories created on this device to be pushed to the server, so that they appear on all my devices.

#### Acceptance Criteria

1. WHEN the Category_Service_Client builds a category push request, THE Category_Service_Client SHALL include the local `CategoryId` (Guid) in the request payload.
2. WHEN the server responds successfully to a category push with a body containing the server-side `Id`, THE Category_Service_Client SHALL persist the returned `Id` as `ExternalId` in the local category record.
3. IF the server response indicates failure (network error or non-success status), THEN THE Category_Service_Client SHALL retain the category in a pending state so it can be retried on the next sync cycle.
4. THE Category_Api_Repo SHALL expose a method to POST categories to the server endpoint.

### Requirement 4: Server Upsert by CategoryId

**User Story:** As a developer, I want the server to upsert by Guid, so that duplicate categories from multiple devices or retry pushes are handled deterministically.

#### Acceptance Criteria

1. WHEN a category request contains a non-null `CategoryId` (Guid), THE Category_Service_Server SHALL query for an existing category matching both `CategoryId` and the authenticated `UserId`.
2. WHEN an existing category is found by the `CategoryId` and `UserId` lookup, THE Category_Service_Server SHALL update that record's mutable fields from the request and SHALL set `UpdatedAt` to the current server UTC time.
3. WHEN no existing category is found by the `CategoryId` and `UserId` lookup, THE Category_Service_Server SHALL insert a new record persisting the provided `CategoryId` alongside the auto-increment `Id`.
4. WHEN a category request contains a null `CategoryId`, THE Category_Service_Server SHALL insert a new record and generate a new Guid for its `CategoryId` field before persisting.
5. WHEN the server performs an upsert (insert or update via `CategoryId`), THE Category_Service_Server SHALL return the server-side auto-increment `Id` in the response body.
6. IF two concurrent requests arrive with the same `CategoryId` and `UserId`, THEN THE Category_Service_Server SHALL ensure exactly one record exists for that `CategoryId`-`UserId` pair by enforcing a unique database constraint on (`CategoryId`, `UserId`).

### Requirement 5: Client Pull with CategoryId Matching

**User Story:** As a user, I want my device to match pulled categories by Guid, so that records from other devices are correctly merged instead of duplicated.

#### Acceptance Criteria

1. WHEN the category response received during pull contains a non-null `CategoryId` (Guid), THE Category_Service_Client SHALL first attempt to find the local record by matching the `CategoryId` field.
2. WHEN a local record is found by `CategoryId`, THE Category_Service_Client SHALL update it using last-writer-wins logic: apply the pulled data only if the pulled `UpdatedAt` is strictly greater than the local `UpdatedAt`.
3. WHEN no local record is found by `CategoryId`, THE Category_Service_Client SHALL fall back to matching by `ExternalId` (server-side integer PK) using the existing lookup.
4. WHEN no local record is found by either `CategoryId` or `ExternalId`, THE Category_Service_Client SHALL insert a new local record.
5. IF the category response received during pull contains a null or empty `CategoryId`, THEN THE Category_Service_Client SHALL match by `ExternalId` only, preserving current behavior.

### Requirement 6: Sync Service Integration

**User Story:** As a user, I want category push to happen automatically as part of the periodic sync cycle, so that categories I create are synced without manual action.

#### Acceptance Criteria

1. WHEN the Sync_Service executes a sync cycle, THE Sync_Service SHALL invoke category push before category pull so that locally-created categories reach the server before pulling updates.
2. WHEN the Category_Service_Client pushes pending categories, THE Category_Service_Client SHALL select all local categories that have `CategoryId` not equal to `Guid.Empty` and `ExternalId` equal to null.
3. WHEN a category push succeeds and the server returns a valid `ExternalId` (greater than 0), THE Category_Service_Client SHALL persist the `ExternalId` on the local record so it is excluded from subsequent push cycles.
4. IF a category push fails for a specific record, THEN THE Category_Service_Client SHALL continue pushing the remaining records without aborting the entire push batch.

### Requirement 7: Backward Compatibility

**User Story:** As a developer, I want the system to remain functional for existing categories that lack a CategoryId, so that no data loss or sync disruption occurs during the rollout.

#### Acceptance Criteria

1. WHILE `CategoryId` is `Guid.Empty` on a local category record, THE Category_Service_Client SHALL use `ExternalId` as the matching key for pull sync operations, following the same logic applied before `CategoryId` was introduced.
2. WHEN the server receives a category request that does not include a `CategoryId` field (or includes null), THE Category_Service_Server SHALL process it using the existing insert flow without returning an error.
3. WHEN the Category_Service_Client pulls a category whose response payload contains a non-empty `CategoryId`, THE Category_Service_Client SHALL write that `CategoryId` value to the matching local record within the same pull operation.
4. WHEN the local database schema is created or upgraded to include the `CategoryId` column, THE Client SHALL assign a default value of `Guid.Empty` to all existing rows so that no existing record is left with a null `CategoryId`.
5. WHILE `CategoryId` is `Guid.Empty` on a local record AND the record has a valid `ExternalId`, THE Category_Service_Client SHALL NOT treat the record as needing push solely because `CategoryId` is empty.

### Requirement 8: Client Repository Lookup by CategoryId

**User Story:** As a developer, I want a repository method to find categories by CategoryId, so that the service layer can perform Guid-based matching efficiently.

#### Acceptance Criteria

1. THE Category_Repo_Client SHALL expose a `GetByCategoryIdAsync(Guid categoryId)` method that returns a `CategoryDTO?`.
2. WHEN queried with a `CategoryId` that exists in the local database, THE Category_Repo_Client SHALL return the matching record.
3. WHEN queried with a `CategoryId` that does not exist in the local database, THE Category_Repo_Client SHALL return null.
4. IF `GetByCategoryIdAsync` is called with `Guid.Empty`, THEN THE Category_Repo_Client SHALL return null without querying the database.

### Requirement 9: Server Repository Lookup by CategoryId

**User Story:** As a developer, I want a server repository method to find categories by CategoryId and UserId, so that the upsert logic can be implemented efficiently.

#### Acceptance Criteria

1. THE Category_Repo_Server SHALL expose a `FindByCategoryIdAsync(Guid categoryId, int userId)` method that returns a `TransactionCategoryDTO?`.
2. WHEN queried with a `CategoryId` and `UserId` that match an existing record, THE Category_Repo_Server SHALL return that record regardless of the record's `Inactive` status.
3. WHEN queried with a `CategoryId` that does not exist for the given user, THE Category_Repo_Server SHALL return null.
4. IF `categoryId` is `Guid.Empty`, THEN THE Category_Repo_Server SHALL return null without querying the database.
5. THE Category_Repo_Server SHALL use a composite index on (`CategoryId`, `UserId`) for lookup efficiency.

### Requirement 10: Client Assigns CategoryId on Creation

**User Story:** As a user, I want categories I create on my device to have a stable identifier immediately, so that they can be synced reliably regardless of network availability.

#### Acceptance Criteria

1. WHEN a category is created via the UI (CategoryEditVM or equivalent), THE Category_Service_Client SHALL assign `Guid.NewGuid()` to `CategoryId` before persisting to the local database.
2. WHEN a category is inserted locally with `CategoryId` equal to `Guid.Empty`, THE Category_Service_Client SHALL assign `Guid.NewGuid()` to `CategoryId` before persisting.
3. THE Category_Service_Client SHALL assign the CategoryId in a single location (the service layer) to avoid inconsistent generation paths.
