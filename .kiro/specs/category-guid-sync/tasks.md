# Implementation Plan: Category Guid Sync

## Overview

Introduce a stable `CategoryId` (Guid) across client and server to enable deterministic cross-device category matching, replacing reliance on `ExternalId`. Additionally, introduce a push flow (client → server) that does not exist today. Implementation proceeds bottom-up: data models first, then repositories, then service-layer push/pull logic, then wiring into the sync cycle.

## Tasks

- [x] 1. Add CategoryId to client data model and repository
  - [x] 1.1 Add `CategoryId` property to client `CategoryDTO`
    - Add `public Guid CategoryId { get; set; }` to `CategoryDTO` in `Model/DTO/CategoryDTO.cs`
    - Ensure `BuildDbService` migration adds the column with default `Guid.Empty` for existing rows
    - _Requirements: 1.1, 1.4, 7.4_

  - [x] 1.2 Implement `GetByCategoryIdAsync` in `CategoryRepo`
    - Add method `Task<CategoryDTO?> GetByCategoryIdAsync(Guid categoryId)` to `ICategoryRepo` and `CategoryRepo`
    - Return `null` immediately if `categoryId == Guid.Empty` (no DB query)
    - Query by `CategoryId` column for matching record
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

  - [x] 1.3 Implement `GetPendingPushAsync` in `CategoryRepo`
    - Add method `Task<List<CategoryDTO>> GetPendingPushAsync()` to `ICategoryRepo` and `CategoryRepo`
    - Return records where `CategoryId != Guid.Empty AND ExternalId == null`
    - _Requirements: 6.2_

  - [x] 1.4 Write property test for client repository lookup
    - **Property 7: Client Repository Lookup Correctness**
    - Verify matching record is returned for any valid Guid
    - Verify null is returned for non-existent Guid
    - Verify `Guid.Empty` returns null without DB query
    - **Validates: Requirements 8.2, 8.3, 8.4**

  - [x] 1.5 Write property test for push selection criteria
    - **Property 6: Push Selection Criteria**
    - Verify `GetPendingPushAsync` returns exactly records with `CategoryId != Guid.Empty AND ExternalId == null`
    - Verify records with `Guid.Empty` or valid `ExternalId` are excluded
    - **Validates: Requirements 6.2, 7.5**

- [x] 2. Add CategoryId to client API models
  - [x] 2.1 Add `CategoryId` to `TransactionCategoryApiRes`
    - Add `public Guid? CategoryId { get; set; }` to `TransactionCategoryApiRes` in `Model/Res/Api/TransactionCategoryApiRes.cs`
    - _Requirements: 1.3, 5.1_

  - [x] 2.2 Create `CategoryReq` request model
    - Create `Model/Req/CategoryReq.cs` with fields: `Guid? CategoryId`, `string Name`, `bool IsMainTransactionCategory`, `int? ParentTransactionCategoryId`, `bool Inactive`, `string? Color`
    - _Requirements: 3.1, 3.4_

  - [x] 2.3 Create `CategoryPushRes` response model
    - Create `Model/Res/Api/CategoryPushRes.cs` with field: `int Id`
    - _Requirements: 3.2_

  - [x] 2.4 Add `PostCategoryAsync` to `CategoryApiRepo`
    - Add method `Task<CategoryPushRes> PostCategoryAsync(CategoryReq req)` to `ICategoryApiRepo` and `CategoryApiRepo`
    - Use existing `AuthRequestAsync` pattern with `RequestsTypes.Post` to `POST /financial/category`
    - _Requirements: 3.4_

- [x] 3. Add CategoryId to server data model and repository
  - [x] 3.1 Add `CategoryId` property to server `TransactionCategoryDTO`
    - Add `public Guid? CategoryId { get; set; }` to `TransactionCategoryDTO` in `FinancialService/Model/DTO/TransactionCategoryDTO.cs`
    - Configure EF Core mapping: nullable column, composite unique index on `(CategoryId, UserId)` with filter `WHERE CategoryId IS NOT NULL`
    - _Requirements: 2.1, 2.2_

  - [x] 3.2 Create EF Core migration for CategoryId column
    - Generate migration that adds nullable `CategoryId` column (`CHAR(36)`)
    - Include SQL to backfill existing rows: `UPDATE TransactionCategory SET CategoryId = UUID() WHERE CategoryId IS NULL`
    - Add composite unique index on `(CategoryId, UserId)`
    - _Requirements: 2.2, 2.6_

  - [x] 3.3 Create `TransactionCategoryReq` request model on server
    - Create `FinancialService/Model/Req/TransactionCategoryReq.cs` with fields: `Guid? CategoryId`, `string Name`, `bool IsMainTransactionCategory`, `int? ParentTransactionCategoryId`, `bool Inactive`, `string? Color`
    - Inherit from `BaseReq` for validation support
    - _Requirements: 2.3_

  - [x] 3.4 Add `CategoryId` to server `TransactionCategoryRes`
    - Add `public Guid? CategoryId { get; set; }` to `TransactionCategoryRes` in `FinancialService/Model/Res/TransactionCategoryRes.cs`
    - _Requirements: 2.4_

  - [x] 3.5 Implement `FindByCategoryIdAsync` in server `TransactionCategoryRepo`
    - Add method `Task<TransactionCategoryDTO?> FindByCategoryIdAsync(Guid categoryId, int userId)` to `ITransactionCategoryRepo` and `TransactionCategoryRepo`
    - Return `null` immediately if `categoryId == Guid.Empty`
    - Query by composite index including inactive records
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5_

  - [x] 3.6 Add `AddAsync` and `UpdateAsync` to server `TransactionCategoryRepo`
    - Add `Task<TransactionCategoryDTO> AddAsync(TransactionCategoryDTO dto)` and `Task UpdateAsync(TransactionCategoryDTO dto)` to interface and implementation
    - _Requirements: 4.3, 4.2_

  - [x] 3.7 Write property test for server repository lookup
    - **Property 8: Server Repository Lookup Correctness**
    - Verify matching record is returned for valid Guid + UserId
    - Verify null for non-existent Guid or mismatched UserId
    - Verify `Guid.Empty` returns null without DB query
    - **Validates: Requirements 9.2, 9.3, 9.4**

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Implement server upsert logic and endpoint
  - [x] 5.1 Implement `UpsertAsync` in server `TransactionCategoryService`
    - Add `Task<TransactionCategoryUpsertRes> UpsertAsync(TransactionCategoryReq req, int uid)` to interface and implementation
    - If `req.CategoryId` is non-null and not `Guid.Empty`: call `FindByCategoryIdAsync`
    - If existing found: update mutable fields, set `UpdatedAt = DateTime.UtcNow`, return existing `Id`
    - If not found: insert with provided `CategoryId`, return new `Id`
    - If `req.CategoryId` is null: generate `Guid.NewGuid()` and insert
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

  - [x] 5.2 Create `TransactionCategoryUpsertRes` response model
    - Create `FinancialService/Model/Res/TransactionCategoryUpsertRes.cs` with field: `int Id`
    - _Requirements: 4.5_

  - [x] 5.3 Add `POST /financial/category` endpoint to `FinancialController`
    - Add action method `AddCategory([FromBody] TransactionCategoryReq req)` with route `category`
    - Validate request, call `UpsertAsync(req, Uid)`, return `Ok(result)`
    - _Requirements: 4.1, 4.5_

  - [x] 5.4 Map `CategoryId` in existing `GetByUid` response
    - Update the `Select` mapping in `TransactionCategoryService.GetByUid` to include `CategoryId = c.CategoryId`
    - _Requirements: 2.4_

  - [x] 5.5 Write property test for server upsert idempotence
    - **Property 2: Server Upsert Idempotence**
    - Push same CategoryId+UserId N times → exactly one record, same Id returned
    - Mutable fields reflect the last request's values
    - **Validates: Requirements 4.1, 4.2, 4.3, 4.5**

- [x] 6. Implement client push logic
  - [x] 6.1 Implement `PushAsync` in `CategoryService`
    - Add `Task PushAsync()` to `ICategoryService` and `CategoryService`
    - Call `GetPendingPushAsync()` to select pending categories
    - For each: build `CategoryReq` with `CategoryId`, POST via `PostCategoryAsync`
    - On success (`Id > 0`): persist returned `Id` as `ExternalId`
    - On failure: continue with remaining records (try/catch per record)
    - _Requirements: 3.1, 3.2, 3.3, 6.2, 6.3, 6.4_

  - [x] 6.2 Modify `AddLocalAsync` to assign `Guid.NewGuid()` to CategoryId
    - If `category.CategoryId == Guid.Empty`, assign `Guid.NewGuid()` before persisting
    - This is the single location for CategoryId generation (service layer)
    - _Requirements: 1.2, 10.1, 10.2, 10.3_

  - [x] 6.3 Write property test for Guid assignment on creation
    - **Property 1: Guid Assignment on Creation**
    - Verify any category created with `Guid.Empty` gets a non-empty Guid after `AddLocalAsync`
    - **Validates: Requirements 1.2, 10.1, 10.2**

  - [x] 6.4 Write property test for push round-trip identity preservation
    - **Property 3: Push Round-Trip Preserves Identity**
    - Verify CategoryId is included in request payload
    - Verify ExternalId is persisted on successful response
    - Verify record is excluded from subsequent `GetPendingPushAsync` calls
    - **Validates: Requirements 3.1, 3.2, 6.3**

- [x] 7. Implement client pull logic with CategoryId matching
  - [x] 7.1 Modify `PullAsync` to match by CategoryId first
    - Rewrite the pull loop in `CategoryService.PullAsync`:
      - If pulled `CategoryId` is non-null and non-empty: call `GetByCategoryIdAsync`
      - If local match found: apply last-writer-wins (update only if pulled `UpdatedAt > local.UpdatedAt`)
      - If no match by CategoryId: fall back to `GetByExternalIdAsync`
      - If no match by either: insert new record
      - Persist pulled `CategoryId` to local record during update
    - If pulled `CategoryId` is null/empty: match by `ExternalId` only (backward-compatible)
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 1.3, 7.1, 7.3_

  - [x] 7.2 Write property test for pull CategoryId matching with last-writer-wins
    - **Property 4: Pull CategoryId Matching with Last-Writer-Wins**
    - Verify update occurs only when pulled `UpdatedAt > local.UpdatedAt`
    - Verify local CategoryId equals pulled value after update
    - **Validates: Requirements 5.1, 5.2, 1.3, 7.3**

  - [x] 7.3 Write property test for pull fallback and insert
    - **Property 5: Pull Fallback and Insert**
    - Verify fallback to ExternalId when CategoryId doesn't match
    - Verify new record inserted when neither matches
    - Verify ExternalId-only matching when CategoryId is null/empty
    - **Validates: Requirements 5.3, 5.4, 5.5**

- [x] 8. Integrate push into sync cycle
  - [x] 8.1 Wire `PushAsync` into `SyncService`
    - Invoke `CategoryService.PushAsync()` before `CategoryService.PullAsync()` within the sync cycle
    - _Requirements: 6.1_

- [x] 9. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck + xUnit in the RecurringTests project
- Unit tests validate specific examples and edge cases
- The server migration (task 3.2) should be tested in a staging environment before production deployment
- All client SQLite schema changes are handled via `BuildDbService` migration logic
- Categories are simpler than transactions: no SyncStatus state machine, no recurring rules, no deterministic Guid derivation

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1", "2.2", "2.3", "3.1", "3.3", "3.4"] },
    { "id": 1, "tasks": ["1.2", "1.3", "2.4", "3.2", "3.5", "3.6", "5.2"] },
    { "id": 2, "tasks": ["1.4", "1.5", "3.7", "5.1", "5.4"] },
    { "id": 3, "tasks": ["5.3", "5.5", "6.1", "6.2"] },
    { "id": 4, "tasks": ["6.3", "6.4", "7.1"] },
    { "id": 5, "tasks": ["7.2", "7.3", "8.1"] }
  ]
}
```
