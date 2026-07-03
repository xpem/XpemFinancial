# Implementation Plan: Account Guid Sync

## Overview

Introduce a stable `AccountId` (Guid) across client and server to enable deterministic cross-device account matching, replacing reliance on `ExternalId`. Unlike categories (which had no push before), accounts already have a bidirectional push/pull flow with a POST/PUT split based on `ExternalId` presence. This feature replaces the POST/PUT split with a single upsert-by-Guid approach on the server for accounts with a valid AccountId, while maintaining backward compatibility for legacy records with `Guid.Empty`. Implementation proceeds bottom-up: data models first, then repositories, then service-layer push/pull logic, then wiring.

## Tasks

- [x] 1. Add AccountId to client data model and repository
  - [x] 1.1 Add `AccountId` property to client `AccountDTO`
    - Add `public Guid AccountId { get; set; }` to `AccountDTO` in `Model/DTO/AccountDTO.cs`
    - Ensure `BuildDbService` migration adds the column with default `Guid.Empty` for existing rows
    - Add filtered index on `AccountId` excluding `Guid.Empty` for efficient lookups
    - _Requirements: 1.1, 1.4, 7.4_

  - [x] 1.2 Implement `GetByAccountIdAsync` in `AccountRepo`
    - Add method `Task<AccountDTO?> GetByAccountIdAsync(Guid accountId)` to `IAccountRepo` and `AccountRepo`
    - Return `null` immediately if `accountId == Guid.Empty` (no DB query)
    - Query by `AccountId` column for matching record
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

  - [x] 1.3 Write property test for client repository lookup
    - **Property 9: Client Repository Lookup Correctness**
    - Verify matching record is returned for any valid Guid
    - Verify null is returned for non-existent Guid
    - Verify `Guid.Empty` returns null without DB query
    - **Validates: Requirements 8.2, 8.3, 8.4**

- [x] 2. Add AccountId to client API models
  - [x] 2.1 Add `AccountId` to `AccountReq`
    - Add `public Guid? AccountId { get; set; }` to `AccountReq` in `Model/Req/AccountReq.cs`
    - _Requirements: 3.1, 3.4_

  - [x] 2.2 Add `AccountId` to `AccountApiRes`
    - Add `public Guid? AccountId { get; set; }` to `AccountApiRes` in `Model/Res/Api/AccountApiRes.cs`
    - _Requirements: 1.3, 5.1_

- [x] 3. Add AccountId to server data model and repository
  - [x] 3.1 Add `AccountId` property to server `AccountDTO`
    - Add `public Guid? AccountId { get; set; }` to `AccountDTO` in `FinancialService/Model/DTO/AccountDTO.cs`
    - Configure EF Core mapping: nullable column, composite unique index on `(AccountId, UserId)` with filter `WHERE AccountId IS NOT NULL`
    - _Requirements: 2.1, 2.2_

  - [x] 3.2 Create EF Core migration for AccountId column
    - Generate migration that adds nullable `AccountId` column (`CHAR(36)`)
    - Include SQL to backfill existing rows: `UPDATE Account SET AccountId = UUID() WHERE AccountId IS NULL`
    - Add composite unique index on `(AccountId, UserId)`
    - _Requirements: 2.2, 2.7_

  - [x] 3.3 Add `AccountId` to server `AccountReq`
    - Add `public Guid? AccountId { get; set; }` to the server-side `AccountReq` for backward compatibility with older clients
    - _Requirements: 2.3_

  - [x] 3.4 Add `AccountId` to server `AccountRes`
    - Add `public Guid? AccountId { get; set; }` to `AccountRes` in `FinancialService/Model/Res/AccountRes.cs`
    - _Requirements: 2.4_

  - [x] 3.5 Implement `FindByAccountIdAsync` in server `AccountRepo`
    - Add method `Task<AccountDTO?> FindByAccountIdAsync(Guid accountId, int userId)` to `IAccountRepo` and `AccountRepo`
    - Return `null` immediately if `accountId == Guid.Empty`
    - Query by composite index including inactive records
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5_

  - [x] 3.6 Write property test for server repository lookup
    - **Property 10: Server Repository Lookup Correctness**
    - Verify matching record is returned for valid Guid + UserId
    - Verify null for non-existent Guid or mismatched UserId
    - Verify `Guid.Empty` returns null without DB query
    - **Validates: Requirements 9.2, 9.3, 9.4**

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Implement server upsert logic
  - [x] 5.1 Modify server `AccountService.CreateAsync` to implement upsert by AccountId
    - If `req.AccountId` is non-null and not `Guid.Empty`: call `FindByAccountIdAsync(req.AccountId.Value, uid)`
    - If existing record found: update mutable fields (`Name`, `Type`, `IncludeInGeneralBalance`, `Inactive`), set `UpdatedAt = DateTime.UtcNow`, return existing `Id` + `AccountId`
    - If not found: insert with `req.AccountId`, return new `Id` + `AccountId`
    - If `req.AccountId` is null or `Guid.Empty`: generate `Guid.NewGuid()` before inserting, return new `Id` + generated `AccountId`
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 2.5, 2.6_

  - [x] 5.2 Map `AccountId` in existing account response (GET endpoint)
    - Update the mapping in the server's account retrieval logic to include `AccountId` in responses
    - _Requirements: 2.4_

  - [x] 5.3 Write property test for server upsert idempotence
    - **Property 2: Server Upsert Idempotence**
    - Push same AccountId+UserId N times → exactly one record, same Id returned
    - Mutable fields reflect the last request's values
    - Response always contains the same auto-increment Id and persisted AccountId
    - **Validates: Requirements 4.1, 4.2, 4.3, 4.5, 2.5**

  - [x] 5.4 Write property test for server Guid generation when none provided
    - **Property 11: Server Generates Guid When None Provided**
    - Verify that requests with null or Guid.Empty AccountId result in a new unique Guid persisted
    - Verify the response contains the generated AccountId and it is not Guid.Empty
    - **Validates: Requirements 4.4, 2.6**

- [x] 6. Implement client push logic with AccountId
  - [x] 6.1 Modify `AccountService` push to include AccountId in request
    - When building `AccountReq` for push, set `AccountId` from the local record
    - The existing POST/PUT decision continues for backward compatibility when `AccountId == Guid.Empty`:
      - POST when `ExternalId` is null
      - PUT when `ExternalId` is non-null
    - When `AccountId != Guid.Empty`: always use POST (server upserts by AccountId)
    - After successful response with `Id > 0`: persist returned `Id` as `ExternalId`
    - After successful response containing a non-empty `AccountId`: persist it to local record
    - On failure: leave local record unchanged, continue with remaining records
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 6.2, 6.3, 6.4, 7.5, 7.6_

  - [x] 6.2 Modify `CreateAsync` to assign `Guid.NewGuid()` to AccountId
    - If `account.AccountId == Guid.Empty`, assign `Guid.NewGuid()` before persisting
    - If `account.AccountId != Guid.Empty`, preserve the provided value
    - _Requirements: 1.2, 10.1, 10.2, 10.3_

  - [x] 6.3 Modify `EnsureDefaultAccountAsync` to assign `Guid.NewGuid()` to AccountId
    - Apply same Guid assignment logic when creating the default account
    - _Requirements: 10.4_

  - [x] 6.4 Ensure `UpdateAsync` does NOT overwrite existing AccountId
    - When updating an account locally, the service SHALL NOT change a non-empty `AccountId` to a different value
    - _Requirements: 10.5_

  - [x] 6.5 Write property test for Guid assignment on creation
    - **Property 1: Guid Assignment on Creation**
    - Verify any account created with `Guid.Empty` gets a non-empty Guid after `CreateAsync` or `EnsureDefaultAccountAsync`
    - Verify any account created with a pre-existing non-empty AccountId retains the original value
    - **Validates: Requirements 1.2, 10.1, 10.2, 10.3, 10.4**

  - [x] 6.6 Write property test for push round-trip identity preservation
    - **Property 3: Push Round-Trip Preserves Identity**
    - Verify AccountId is included in request payload for non-empty AccountId
    - Verify ExternalId is persisted on successful response
    - **Validates: Requirements 3.1, 3.2, 6.3**

  - [x] 6.7 Write property test for push failure leaves record unchanged
    - **Property 4: Push Failure Leaves Record Unchanged**
    - Verify local record retains pre-push ExternalId and AccountId on failure
    - Verify record continues to match pending-push selection criteria
    - **Validates: Requirements 3.3, 6.4**

  - [x] 6.8 Write property test for AccountId immutability after assignment
    - **Property 8: AccountId Immutability After Assignment**
    - Verify subsequent UpdateAsync calls do NOT change a non-empty AccountId
    - **Validates: Requirements 10.5**

- [x] 7. Implement client pull logic with AccountId matching
  - [x] 7.1 Modify `PullAsync` to match by AccountId first
    - Rewrite the pull loop in `AccountService.PullAsync`:
      - If pulled `AccountId` is non-null and non-empty: call `GetByAccountIdAsync`
      - If local match found: apply last-writer-wins (update only if pulled `UpdatedAt > local.UpdatedAt`)
      - If no match by AccountId: fall back to `ExternalId` lookup
      - If match found by ExternalId and response contains non-empty `AccountId`: persist it locally
      - If no match by either: insert new record with both `AccountId` and `ExternalId`
    - If pulled `AccountId` is null/empty: match by `ExternalId` only (backward-compatible)
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 1.3, 7.1, 7.3_


  - [x] 7.2 Write property test for pull AccountId matching with last-writer-wins
    - **Property 5: Pull AccountId Matching with Last-Writer-Wins**
    - Verify update occurs only when pulled `UpdatedAt > local.UpdatedAt`
    - Verify local AccountId equals pulled value after update
    - Verify AccountId is persisted on ExternalId-fallback match
    - **Validates: Requirements 5.1, 5.2, 5.3, 5.4, 1.3, 7.3**

  - [x] 7.3 Write property test for pull inserting new records
    - **Property 6: Pull Inserts New Records**
    - Verify new record inserted when neither AccountId nor ExternalId matches
    - Verify both AccountId and ExternalId are persisted from response
    - **Validates: Requirements 5.5**

  - [x] 7.4 Write property test for backward compatibility
    - **Property 7: Backward Compatibility — Guid.Empty Falls Back to ExternalId**
    - Verify ExternalId is used as matching key when AccountId is empty
    - Verify POST/PUT decision logic continues for Guid.Empty records
    - Verify successful push/pull with non-empty AccountId persists both ExternalId and AccountId
    - **Validates: Requirements 7.1, 7.5, 7.6, 5.6**

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.



## Notes

- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck + xUnit in the RecurringTests project
- Unit tests validate specific examples and edge cases
- The server migration (task 3.2) should be tested in a staging environment before production deployment
- All client SQLite schema changes are handled via `BuildDbService` migration logic
- Accounts are simpler than transactions: no SyncStatus state machine, no recurring rules, no deterministic Guid derivation
- The existing POST/PUT split is retained for backward compatibility with `Guid.Empty` records; only non-empty AccountId records use the upsert path
- The existing cursor-based pending-push selection (`ExternalId == null OR UpdatedAt > lastSyncCursor`) remains unchanged — AccountId-based upsert on the server makes re-pushes safe

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1", "2.2", "3.1", "3.3", "3.4"] },
    { "id": 1, "tasks": ["1.2", "3.2", "3.5"] },
    { "id": 2, "tasks": ["1.3", "3.6", "5.1", "5.2"] },
    { "id": 3, "tasks": ["5.3", "5.4", "6.1", "6.2", "6.3", "6.4"] },
    { "id": 4, "tasks": ["6.5", "6.6", "6.7", "6.8", "7.1"] },
    { "id": 5, "tasks": ["7.2", "7.3", "7.4"] }
  ]
}
```
