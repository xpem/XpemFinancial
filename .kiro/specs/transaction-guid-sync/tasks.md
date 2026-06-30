# Implementation Plan: Transaction Guid Sync

## Overview

Introduce a stable `TransactionId` (Guid) across client and server to replace heuristic-based deduplication. Implementation proceeds bottom-up: shared utilities first, then data models and repositories, then service-layer sync logic, and finally wiring and integration.

## Tasks

- [x] 1. Implement deterministic Guid utility
  - [x] 1.1 Create `DeterministicGuid` static class
    - Create a new file (e.g., `Services/Transaction/DeterministicGuid.cs`) with a static class containing:
      - A fixed namespace Guid constant
      - A `FromRecurringRule(Guid recurringRuleId, DateTime occurrenceDate)` method that derives a deterministic Guid using SHA-256 truncated to 16 bytes with UUID v5 version/variant bits
    - Guard against `Guid.Empty` ruleId and `default(DateTime)` date — return `Guid.Empty` for invalid inputs
    - _Requirements: 6.1, 6.2, 6.4_

  - [x] 1.2 Write property test for deterministic Guid derivation
    - **Property 7: Deterministic Recurring TransactionId**
    - Verify that the same `(RecurringRuleId, date)` always produces the same Guid
    - Verify that distinct `(RecurringRuleId, date)` pairs produce distinct Guids
    - Verify `Guid.Empty` ruleId or `default(DateTime)` returns `Guid.Empty`
    - **Validates: Requirements 6.1, 6.2**

- [x] 2. Add TransactionId to client data model and repository
  - [x] 2.1 Add `TransactionId` property to client `TransactionDTO`
    - Add `public Guid TransactionId { get; set; }` to the `TransactionDTO` class in the Model project
    - sqlite-net-pcl will add the column automatically on table creation; for existing databases, the `BuildDbService` migration logic should handle adding the column with default `Guid.Empty`
    - Add a unique index attribute or create the index in `BuildDbService` (excluding `Guid.Empty` rows)
    - _Requirements: 1.1, 1.4, 8.5_

  - [x] 2.2 Implement `GetByTransactionIdAsync` in `TransactionRepo`
    - Add method `Task<TransactionDTO?> GetByTransactionIdAsync(Guid transactionId)` to `TransactionRepo`
    - Return `null` immediately if `transactionId == Guid.Empty`
    - Query including inactive records
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

  - [x] 2.3 Write property test for client repository lookup
    - **Property 11: Client Repository Lookup Correctness**
    - Verify matching record is returned for any valid Guid
    - Verify null is returned for non-existent Guid
    - Verify `Guid.Empty` returns null without DB query
    - **Validates: Requirements 9.2, 9.3, 9.4**

- [x] 3. Add TransactionId to client API models
  - [x] 3.1 Add `TransactionId` to `TransactionReq` (client model)
    - Add `public Guid? TransactionId { get; set; }` to the request model used for push
    - _Requirements: 3.1_

  - [x] 3.2 Add `TransactionId` to `TransactionApiRes` (client model)
    - Add `public Guid? TransactionId { get; set; }` to the response model used for pull
    - _Requirements: 1.3, 5.1_

- [x] 4. Add TransactionId to server data model and repository
  - [x] 4.1 Add `TransactionId` property to server `TransactionDTO`
    - Add `public Guid? TransactionId { get; set; }` to the server-side TransactionDTO in the FinancialService project
    - Configure EF Core mapping: nullable column, composite unique index on `(TransactionId, UserId)` with filter `WHERE TransactionId IS NOT NULL`
    - _Requirements: 2.1, 2.2, 4.7_

  - [x] 4.2 Create EF Core migration for TransactionId column
    - Generate migration that adds nullable `TransactionId` column
    - Include SQL to backfill existing rows: `UPDATE Transaction SET TransactionId = UUID() WHERE TransactionId IS NULL`
    - Add composite unique index on `(TransactionId, UserId)`
    - _Requirements: 2.2, 2.6_

  - [x] 4.3 Add `TransactionId` to server `TransactionReq`
    - Add `public Guid? TransactionId { get; set; }` for backward compatibility with older clients
    - _Requirements: 2.3_

  - [x] 4.4 Add `TransactionId` to server `TransactionRes`
    - Add `public Guid TransactionId { get; set; }` (non-nullable) so pulling clients always receive the identifier
    - Map from the stored nullable column (server guarantees non-null after backfill)
    - _Requirements: 2.4_

  - [x] 4.5 Implement `FindByTransactionIdAsync` in server `TransactionRepo`
    - Add method `Task<TransactionDTO?> FindByTransactionIdAsync(Guid transactionId, int userId)`
    - Return `null` immediately if `transactionId == Guid.Empty`
    - Query by composite index including inactive records
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_

  - [x] 4.6 Write property test for server repository lookup
    - **Property 12: Server Repository Lookup Correctness**
    - Verify matching record is returned for valid Guid + UserId
    - Verify null for non-existent Guid or mismatched UserId
    - Verify `Guid.Empty` returns null without DB query
    - **Validates: Requirements 10.2, 10.3, 10.4**

- [x] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Implement server upsert logic
  - [x] 6.1 Modify server `TransactionService.AddAsync` to implement upsert by TransactionId
    - If `req.TransactionId` is non-null and not `Guid.Empty`: call `FindByTransactionIdAsync(req.TransactionId.Value, userId)`
    - If existing record found: update mutable fields, set `UpdatedAt = DateTime.UtcNow`, return existing `Id`
    - If not found: insert with `req.TransactionId`, return new `Id`
    - If `req.TransactionId` is null or `Guid.Empty`: fall through to existing `FindDuplicateAsync` heuristic
    - If `req.TransactionId` is null on insert: generate `Guid.NewGuid()` before persisting
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 2.5_

  - [x] 6.2 Write property test for server upsert idempotence
    - **Property 2: Server Upsert Idempotence**
    - Push same TransactionId+UserId N times → exactly one record, same Id returned
    - Mutable fields reflect the last request's values
    - **Validates: Requirements 4.2, 4.3, 4.5**

- [x] 7. Implement client push logic with TransactionId
  - [x] 7.1 Modify `TransactionService` push to include TransactionId in request
    - When building `TransactionReq` for POST or PUT, set `TransactionId` from the local record
    - After successful response with `ExternalId > 0`: persist `ExternalId` and set `SyncStatus = Synced` atomically (ensure entity is attached to context before saving)
    - After response with `ExternalId = 0`: keep `SyncStatus = Pending`
    - _Requirements: 3.1, 3.2, 3.3, 7.1, 7.3, 7.4_

  - [x] 7.2 Modify `AddAsync` / `AddOccurrenceAsync` to assign Guid on creation
    - If `TransactionId == Guid.Empty`, assign `Guid.NewGuid()` before persisting
    - _Requirements: 1.2_

  - [x] 7.3 Write property test for push round-trip identity preservation
    - **Property 3: Push Round-Trip Preserves Identity**
    - Verify TransactionId is included in request payload
    - Verify ExternalId is persisted on successful response
    - **Validates: Requirements 3.1, 3.2**

  - [x] 7.4 Write property test for Guid assignment on creation
    - **Property 1: Guid Assignment on Creation**
    - Verify any transaction created with `Guid.Empty` gets a non-empty Guid
    - **Validates: Requirements 1.2, 2.5**

- [x] 8. Implement client pull logic with TransactionId matching
  - [x] 8.1 Modify `TransactionService` pull (ApplyFromApiAsync) to match by TransactionId first
    - If pulled `TransactionId` is non-empty: call `GetByTransactionIdAsync`
    - If local match found and `SyncStatus != Pushing`: apply last-writer-wins (update only if pulled `UpdatedAt > local UpdatedAt`)
    - If local match found and `SyncStatus == Pushing`: skip update
    - If no match by TransactionId: fall back to `ExternalId` lookup
    - If no match by either: insert new record with `SyncStatus = Synced`
    - Persist the pulled `TransactionId` to local record during pull
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 1.3, 8.3_

  - [x] 8.2 Write property test for pull TransactionId matching with last-writer-wins
    - **Property 4: Pull TransactionId Matching with Last-Writer-Wins**
    - Verify update occurs only when pulled `UpdatedAt > local UpdatedAt`
    - **Validates: Requirements 5.2, 1.3, 8.3**

  - [x] 8.3 Write property test for pull skipping Pushing records
    - **Property 5: Pull Skips Records in Pushing State**
    - Verify local record is unchanged when SyncStatus is Pushing
    - **Validates: Requirements 5.3**

  - [x] 8.4 Write property test for pull inserting new records
    - **Property 6: Pull Inserts New Records**
    - Verify new record is inserted with SyncStatus = Synced when no match exists
    - **Validates: Requirements 5.5**

- [x] 9. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Implement recurring scheduler deterministic Guid and deduplication
  - [x] 10.1 Modify `BuildOccurrence` in RecurringScheduler to use deterministic TransactionId
    - Call `DeterministicGuid.FromRecurringRule(recurringRuleId, occurrenceDate)` to derive TransactionId
    - Skip generation if `RecurringRuleId == Guid.Empty` or `occurrenceDate == default(DateTime)`
    - Before inserting, call `GetByTransactionIdAsync` — if record exists, skip generation
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [x] 10.2 Write property test for recurring occurrence deduplication
    - **Property 8: Recurring Occurrence Deduplication**
    - Verify that generating the same rule+date twice results in exactly one record
    - **Validates: Requirements 6.3**

  - [x] 10.3 Write property test for atomic ExternalId persist after push
    - **Property 9: Atomic ExternalId Persist After Push**
    - Verify ExternalId and SyncStatus = Synced are set together after successful push
    - Verify record is excluded from `GetPendingPushAsync` after push
    - **Validates: Requirements 7.1, 7.3**

- [x] 11. Implement backward compatibility safeguards
  - [x] 11.1 Ensure Guid.Empty transactions use ExternalId-based sync
    - Verify that push/pull logic falls back to ExternalId when `TransactionId == Guid.Empty`
    - Ensure records with `Guid.Empty` and valid `ExternalId` and `SyncStatus = Synced` are NOT returned by `GetPendingPushAsync`
    - _Requirements: 8.1, 8.2, 8.6_

  - [x] 11.2 Write property test for backward compatibility
    - **Property 10: Backward Compatibility — Guid.Empty Falls Back to ExternalId**
    - Verify ExternalId is used as matching key when TransactionId is empty
    - Verify no spurious push triggered by empty TransactionId
    - **Validates: Requirements 8.1, 8.6**

- [x] 12. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck + xUnit in the RecurringTests project
- Unit tests validate specific examples and edge cases
- The server migration (task 4.2) should be tested in a staging environment before production deployment
- All client SQLite schema changes are handled by sqlite-net-pcl's auto-migration or manual migration in `BuildDbService`

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1", "3.1", "3.2", "4.1"] },
    { "id": 1, "tasks": ["1.2", "2.2", "4.2", "4.3", "4.4"] },
    { "id": 2, "tasks": ["2.3", "4.5"] },
    { "id": 3, "tasks": ["4.6", "6.1"] },
    { "id": 4, "tasks": ["6.2", "7.1", "7.2"] },
    { "id": 5, "tasks": ["7.3", "7.4", "8.1"] },
    { "id": 6, "tasks": ["8.2", "8.3", "8.4", "10.1"] },
    { "id": 7, "tasks": ["10.2", "10.3", "11.1"] },
    { "id": 8, "tasks": ["11.2"] }
  ]
}
```
