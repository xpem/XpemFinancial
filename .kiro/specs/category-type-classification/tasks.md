# Implementation Plan: Category Type Classification

## Overview

This plan implements the `CategoryType` enum and `Type` property across client (XpemFinancial/.NET MAUI) and server (UniqueServer) codebases. Tasks are organized to build foundational types first, then server-side changes, followed by client-side model/service changes, and finally UI integration and sync wiring.

## Tasks

- [x] 1. Define CategoryType enum and extend data models
  - [x] 1.1 Create the `CategoryType` enum in the client Model project
    - Create file `Model/DTO/CategoryType.cs` with enum values: `Income = 0`, `Expense = 1`, `Both = 2`
    - Namespace: `Model.DTO`
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 1.2 Add `Type` property to `CategoryDTO` on the client
    - In `Model/DTO/CategoryDTO.cs`, add `public CategoryType Type { get; set; } = CategoryType.Both;`
    - _Requirements: 2.1, 2.3_

  - [x] 1.3 Add `Type` field to `CategoryReq` (client push payload)
    - In `Model/Req/CategoryReq.cs`, add `public int? Type { get; set; }` (nullable for backward compatibility)
    - _Requirements: 8.1_

  - [x] 1.4 Add `Type` field to `TransactionCategoryApiRes` (client pull response)
    - In `Model/Res/Api/TransactionCategoryApiRes.cs`, add `public int? Type { get; set; }` (nullable for old servers)
    - _Requirements: 9.1, 9.3, 9.4_

  - [x] 1.5 Write property test for CategoryType enum serialization round-trip
    - **Property 1: CategoryType enum serialization round-trip**
    - Create `RecurringTests/CategoryTests/CategoryTypeSerializationPropertyTests.cs`
    - For any valid CategoryType, casting to int and back produces the original member
    - **Validates: Requirements 1.3**

- [x] 2. Server-side model and migration changes (UniqueServer)
  - [x] 2.1 Add `Type` property to `TransactionCategoryDTO` on the server
    - In `FinancialService/Model/DTO/TransactionCategoryDTO.cs`, add `public int Type { get; set; } = 2;`
    - _Requirements: 2.2, 2.3_

  - [x] 2.2 Add `Type` field to `TransactionCategoryReq` on the server
    - In `FinancialService/Model/Req/TransactionCategoryReq.cs`, add `public int? Type { get; set; }` (nullable for backward compat)
    - _Requirements: 8.2, 11.1_

  - [x] 2.3 Add `Type` field to `TransactionCategoryRes` on the server
    - In `FinancialService/Model/Res/TransactionCategoryRes.cs`, add `public int Type { get; set; }`
    - _Requirements: 9.1_

  - [x] 2.4 Create EF Core migration to add `Type` column to `TransactionCategory` table
    - Add a migration in `FinancialService/Migrations/` that adds non-nullable `Type` column of type `int` with default value 2
    - All pre-existing rows get `Type = 2`
    - _Requirements: 3.1, 3.2_

  - [x] 2.5 Update `TransactionCategoryService.GetByUid` to include `Type` in response mapping
    - In `FinancialService/Service/TransactionCategoryService.cs`, map `Type = c.Type` in the `GetByUid` select
    - _Requirements: 9.1_

  - [x] 2.6 Update `TransactionCategoryService.UpsertAsync` with Type validation and persistence
    - If `req.Type` has a value: validate it is in {0, 1, 2} (reject with error if not), then assign to DTO
    - If `req.Type` is null: preserve existing value on update, default to 2 on insert
    - _Requirements: 8.2, 8.3, 11.1, 11.3_

  - [x] 2.7 Write property test for server rejecting invalid Type values
    - **Property 8: Server rejects invalid Type values**
    - Test that any integer not in {0, 1, 2} causes UpsertAsync to throw
    - **Validates: Requirements 8.3**

  - [x] 2.8 Write property test for server preserving existing Type when request omits it
    - **Property 9: Server preserves existing Type when request omits it**
    - Test that null Type in request preserves existing category Type, new categories default to 2
    - **Validates: Requirements 11.1, 11.3**

- [x] 3. Checkpoint - Server changes complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Client database migration and sync integration
  - [x] 4.1 Increment `CurrentDbVersion` in `BuildDbService` (22 → 23)
    - In `Services/BuildDbService.cs`, change `CurrentDbVersion = 22` to `CurrentDbVersion = 23`
    - Configure `CategoryDTO.Type` default value in `DbCtx.OnModelCreating` if needed
    - _Requirements: 4.1, 4.2, 4.3_

  - [x] 4.2 Update `CategoryService.PullAsync` to map `Type` from API response
    - Map `Type` from `TransactionCategoryApiRes.Type` to `CategoryDTO.Type` using defensive parsing
    - Add static helper `SafeParseCategoryType(int?)` that defaults null or out-of-range to `CategoryType.Both`
    - Apply to both update and insert paths
    - _Requirements: 9.2, 9.3, 9.4, 11.2_

  - [x] 4.3 Update `CategoryService.PushAsync` to include `Type` in `CategoryReq`
    - In the `PostCategoryAsync` call, set `Type = (int)category.Type`
    - _Requirements: 8.1_

  - [x] 4.4 Write property test for Push/Pull sync round-trip preserving Type
    - **Property 7: Push/Pull sync round-trip preserves Type**
    - For any CategoryDTO with valid Type, push then pull yields same Type value
    - **Validates: Requirements 8.1, 8.2, 9.1, 9.2**

- [x] 5. Subcategory type inheritance logic
  - [x] 5.1 Add `UpdateMainCategoryTypeAsync` method to `CategoryService`
    - Update the MainCategory's Type and cascade to all active subcategories (Inactive = false) with matching `ParentExternalId`
    - Set `UpdatedAt = DateTime.UtcNow` on all affected records
    - _Requirements: 5.2_

  - [x] 5.2 Enforce type inheritance on subcategory creation and re-parenting
    - When a subcategory is created, assign `Type` from the parent MainCategory resolved via `ParentExternalId`
    - When a subcategory's parent changes, update its `Type` to match the new parent
    - Prevent save if parent cannot be resolved
    - _Requirements: 5.1, 5.3, 5.5_

  - [x] 5.3 Write property test for subcategory inheriting parent Type on creation
    - **Property 2: Subcategory inherits parent Type on creation**
    - For any MainCategory with any CategoryType, a new subcategory under it has the same Type
    - **Validates: Requirements 2.5, 5.1**

  - [x] 5.4 Write property test for MainCategory type change cascading to active subcategories
    - **Property 3: MainCategory type change cascades to active subcategories**
    - For any MainCategory with active subcategories, changing its Type updates all active children
    - **Validates: Requirements 5.2**

  - [x] 5.5 Write property test for re-parenting subcategory updating its Type
    - **Property 4: Re-parenting subcategory updates its Type**
    - For any subcategory moved to a new parent, its Type matches the new parent's Type
    - **Validates: Requirements 5.3**

- [x] 6. CategoryPicker filtering by transaction type
  - [x] 6.1 Add `TransactionType` parameter and filtering logic to `CategoryPickerVM`
    - Accept `TransactionType?` as a parameter when opening the picker
    - Implement `FilterByTransactionType` logic: Income → show Income + Both; Expense → show Expense + Both; Transfer/Adjustment/null → show all
    - Display empty state message when filter yields zero results
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

  - [x] 6.2 Pass `TransactionType` context from `TransactionEditVM` to `CategoryPickerVM`
    - When opening the CategoryPicker from the transaction edit flow, provide the current transaction's type
    - _Requirements: 7.6_

  - [x] 6.3 Write property test for CategoryPicker filtering Income/Expense contexts
    - **Property 5: CategoryPicker filters by compatible Type for Income and Expense contexts**
    - For any list of active categories, Income context returns only Income + Both; Expense context returns only Expense + Both
    - **Validates: Requirements 7.1, 7.2**

  - [x] 6.4 Write property test for CategoryPicker showing all categories for Transfer/Adjustment/null
    - **Property 6: CategoryPicker shows all active categories for Transfer, Adjustment, or null context**
    - For any list of active categories, Transfer/Adjustment/null returns all of them
    - **Validates: Requirements 7.3, 7.4, 11.4**

- [x] 7. Checkpoint - Core logic complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Category Edit UI integration
  - [x] 8.1 Add Type selector to `CategoryEditVM` for MainCategory create/edit
    - Show a required Type picker with options: Receita (Income), Despesa (Expense), Ambos (Both)
    - No pre-selection on create; pre-filled with current value on edit
    - Prevent save if Type is not selected (validation message: "Selecione um tipo para a categoria")
    - On type change for existing MainCategory, call `UpdateMainCategoryTypeAsync` to cascade
    - _Requirements: 6.1, 6.2, 6.4, 2.4_

  - [x] 8.2 Show read-only inherited Type for subcategories in `CategoryEditVM`
    - Display the parent's CategoryType as a non-editable label
    - Show empty and read-only if parent cannot be resolved
    - _Requirements: 6.3, 5.4, 6.5_

  - [x] 8.3 Write property test for MainCategory creation requiring Type selection
    - **Property 10: MainCategory creation requires Type selection**
    - Save is rejected when no Type is selected; succeeds when valid Type is provided
    - **Validates: Requirements 2.4, 6.4**

- [x] 9. SystemDefault category type assignments (server)
  - [x] 9.1 Create a data migration or seed update on the server to assign Type values to SystemDefault categories
    - Set Type values per the mapping: Receita → Income(0); Alimentação, Carro, Casa, Educação, Doações, Eletrônicos, Presentes, Pessoais, Impostos, Lazer, Saúde, Seguro, Transporte, Investimentos → Expense(1); Sem categoria, Outros → Both(2)
    - Bump `UpdatedAt` on affected rows so clients pick up changes on next pull
    - Apply inheritance to subcategories of each SystemDefault MainCategory
    - _Requirements: 10.1, 10.2, 10.3, 10.4_

- [x] 10. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design
- Unit tests validate specific examples and edge cases
- The client uses a drop-and-recreate migration strategy (version bump), so no ALTER TABLE is needed on SQLite
- Server migration uses EF Core standard migration tooling
- Both codebases are separate repositories; server tasks (group 2, 9) target UniqueServer, all others target XpemFinancial

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1"] },
    { "id": 1, "tasks": ["1.2", "1.3", "1.4", "2.2", "2.3", "2.4"] },
    { "id": 2, "tasks": ["1.5", "2.5", "2.6"] },
    { "id": 3, "tasks": ["2.7", "2.8", "4.1"] },
    { "id": 4, "tasks": ["4.2", "4.3"] },
    { "id": 5, "tasks": ["4.4", "5.1", "5.2"] },
    { "id": 6, "tasks": ["5.3", "5.4", "5.5", "6.1"] },
    { "id": 7, "tasks": ["6.2", "6.3", "6.4"] },
    { "id": 8, "tasks": ["8.1", "8.2"] },
    { "id": 9, "tasks": ["8.3", "9.1"] }
  ]
}
```
