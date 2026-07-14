# Implementation Plan: Category Management

## Overview

This plan implements a dedicated Category Management page accessible from the Flyout sidebar, allowing users to view their full category hierarchy, edit user-created categories, and toggle the Inactive flag with cascading logic. The implementation reuses existing infrastructure (CategoryService, CategoryEditPage, CategoryPicker) and follows the AccountsPage pattern for the new management page.

## Tasks

- [x] 1. Service and data layer extensions
  - [x] 1.1 Add `UpdateLocalAsync` method to `ICategoryService` and `CategoryService`
    - Add `Task UpdateLocalAsync(CategoryDTO category)` to `ICategoryService` interface in `Services/Category/CategoryService.cs`
    - Implement `UpdateLocalAsync` in `CategoryService` — set `UpdatedAt = DateTime.UtcNow`, call `categoryRepo.UpdateAsync(category)`
    - _Requirements: 7.1_

  - [x] 1.2 Add `GetAllGroupedAsync` method to `ICategoryService` and `CategoryService`
    - Add `Task<List<CategoryDTO>> GetAllGroupedAsync()` to `ICategoryService`
    - Implement hierarchical sorting: main categories alphabetically (active first, then inactive), each followed by its subcategories (active alphabetically, then inactive alphabetically), orphan subcategories at the end
    - _Requirements: 2.1, 2.5_

  - [x] 1.3 Create `CategoryDisplayItem` display model
    - Create `CategoryDisplayItem.cs` in `XpemFinancial/VMs/` or a shared Models folder
    - Properties: `CategoryDTO Category`, computed `IsMainCategory`, `IsInactive`, `IsSystemDefault`, `CanEdit`, `CanInactivate`, `CanReactivate`
    - _Requirements: 2.3, 3.6, 4.4, 5.7_

- [x] 2. CategoryManagementVM implementation
  - [x] 2.1 Create `CategoryManagementVM` ViewModel
    - Create `XpemFinancial/VMs/CategoryManagementVM.cs`
    - Constructor injection: `ICategoryService`, `IUserSessionService`
    - Observable properties: `List<CategoryDisplayItem> Categories`, `bool HasNoCategories`
    - Implement `InitializeAsync` — call `GetAllGroupedAsync`, map to `CategoryDisplayItem` list, set `HasNoCategories` if empty
    - _Requirements: 2.1, 2.6_

  - [x] 2.2 Implement `InactivateCategory` command in `CategoryManagementVM`
    - Show confirmation dialog "Deseja inativar esta categoria?"
    - On confirm: set `Inactive = true` and `UpdatedAt = DateTime.UtcNow` on the category
    - If main category with active subcategories: also inactivate all subcategories (cascade)
    - Call `UpdateLocalAsync` for each affected category, then `PushAsync`
    - Refresh the list after operation
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

  - [x] 2.3 Implement `ReactivateCategory` command in `CategoryManagementVM`
    - Set `Inactive = false` and `UpdatedAt = DateTime.UtcNow` on the category
    - If subcategory whose parent is inactive: also reactivate the parent (cascade up)
    - Call `UpdateLocalAsync` for each affected category, then `PushAsync`
    - Refresh the list after operation
    - _Requirements: 4.1, 4.2, 4.3_

  - [x] 2.4 Implement `EditCategory` command in `CategoryManagementVM`
    - Navigate to `CategoryEditPage` passing `categoryId` query parameter (the local `Id` or `CategoryId`)
    - _Requirements: 5.1_

- [x] 3. CategoryManagementPage (View)
  - [x] 3.1 Create `CategoryManagementPage.xaml` and code-behind
    - Create `XpemFinancial/Views/CategoryManagementPage.xaml` and `.xaml.cs`
    - Follow `AccountsPage` pattern: Shell.TitleView with Tag icon and "Categorias" title
    - CollectionView bound to `Categories` property
    - Display main categories in bold, subcategories indented (use Margin or converter)
    - Show "Sistema" badge/label on `SystemDefault` items
    - Apply `Opacity="0.6"` on inactive items
    - Swipe actions or context menu for Edit/Inactivate/Reactivate (conditionally visible via `CanEdit`, `CanInactivate`, `CanReactivate`)
    - Empty state label when `HasNoCategories` is true
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 3.6, 4.4, 5.7_

  - [x] 3.2 Register `CategoryManagementPage` in DI and Shell routing
    - Add `services.AddTransientWithShellRoute<CategoryManagementPage, CategoryManagementVM>(nameof(CategoryManagementPage))` in `MauiProgram.ShellRoutes`
    - _Requirements: 1.2_

  - [x] 3.3 Add "Categorias" FlyoutItem to `AppShell.xaml`
    - Add a new `FlyoutItem` with `Title="Categorias"` and `Icon="{x:Static icons:IconFont.Tag}"` after the "Contas" FlyoutItem
    - ShellContent pointing to `CategoryManagementPage`
    - _Requirements: 1.1, 1.2_

- [x] 4. Checkpoint - Ensure the management page loads and displays categories
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. CategoryEditVM — Edit mode support
  - [x] 5.1 Extend `CategoryEditVM` to support edit mode via query parameter
    - In `ApplyQueryAttributes`, accept a `categoryId` parameter (string or Guid)
    - When `categoryId` is present: load the existing category from service, populate `Name`, `IsMainCategory`, `ParentCategory`, `ParentCategoryName`
    - Add a boolean `IsEditMode` property to distinguish create vs edit in the UI
    - _Requirements: 5.1, 5.2_

  - [x] 5.2 Modify `Save` command in `CategoryEditVM` for edit mode
    - In edit mode: trim name, validate non-empty (show "Informe um nome para a categoria"), update existing category's `Name` and `UpdatedAt`
    - Call `categoryService.UpdateLocalAsync(category)` then `PushAsync`
    - Navigate back on success with message "Categoria atualizada com sucesso"
    - _Requirements: 5.3, 5.4, 5.5_

  - [x] 5.3 Block type change when main category has active subcategories
    - In edit mode, if category `IsMainCategory` and has active subcategories, disable the type toggle (set a `CanChangeType` property to false)
    - Show validation message if user attempts to change type when blocked
    - _Requirements: 5.6_

  - [x] 5.4 Update `CategoryEditPage.xaml` for edit mode UI
    - Change page title to "Editar Categoria" when in edit mode
    - Conditionally disable type selector when `CanChangeType` is false
    - _Requirements: 5.2, 5.6_

- [x] 6. CategoryPickerVM — Inactive filtering
  - [x] 6.1 Filter inactive categories from `CategoryPickerVM`
    - In `InitializeAsync`, after loading `_cachedCategories`, filter out items where `Inactive == true`
    - Active subcategories of inactive parents remain visible (filter only by own `Inactive` flag)
    - _Requirements: 6.1, 6.2_

  - [x] 6.2 Handle inactive category reference in transaction editing
    - In `TransactionEditVM` (or CategoryPicker navigation), do not pre-select an inactive category when editing a transaction that references one
    - Require user to pick a new active category before saving
    - _Requirements: 6.3, 6.4_

- [x] 7. Checkpoint - Ensure edit, inactivate, reactivate, and picker filtering work end-to-end
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Property-based tests (FsCheck.Xunit)
  - [x] 8.1 Write property test for hierarchical sorting (Property 1)
    - **Property 1: Hierarchical sorting preserves grouping and ordering invariants**
    - **Validates: Requirements 2.1, 2.5**
    - Create `RecurringTests/CategoryTests/CategoryGroupingPropertyTests.cs`
    - Generate arbitrary lists of `CategoryDTO` with varying names, hierarchy, inactive flags
    - Assert: each main category immediately precedes its subcategories; active before inactive within each group; groups sorted alphabetically by main category name

  - [x] 8.2 Write property test for cascading inactivation (Property 2)
    - **Property 2: Cascading inactivation**
    - **Validates: Requirements 3.2, 3.4**
    - For any active main category with N active subcategories, inactivating it results in all N+1 items having `Inactive == true` and `UpdatedAt` updated

  - [x] 8.3 Write property test for cascading reactivation (Property 3)
    - **Property 3: Cascading reactivation**
    - **Validates: Requirements 4.1, 4.2**
    - For any inactive subcategory with inactive parent, reactivating the subcategory also reactivates the parent

  - [x] 8.4 Write property test for whitespace-only name rejection (Property 4)
    - **Property 4: Whitespace-only names are rejected**
    - **Validates: Requirements 5.3**
    - For any string composed entirely of whitespace characters, save is rejected

  - [x] 8.5 Write property test for name trimming on save (Property 5)
    - **Property 5: Name trimming on save**
    - **Validates: Requirements 5.4**
    - For any valid name with leading/trailing whitespace, persisted name equals trimmed name

  - [x] 8.6 Write property test for type change blocked (Property 6)
    - **Property 6: Type change blocked for parents with active children**
    - **Validates: Requirements 5.6**
    - For any main category with at least one active subcategory, changing `IsMainCategory` to false is prevented

  - [x] 8.7 Write property test for picker excludes inactive (Property 7)
    - **Property 7: Category Picker excludes all inactive categories**
    - **Validates: Requirements 6.1, 6.2**
    - For any set of categories, picker result contains only items where `Inactive == false`

  - [x] 8.8 Write property test for pending push query (Property 8)
    - **Property 8: Pending push query correctness**
    - **Validates: Requirements 7.2**
    - For any set of categories, pending push returns exactly those where `CategoryId != Guid.Empty` AND `ExternalId == null`

  - [x] 8.9 Write property test for push outcome (Property 9)
    - **Property 9: Push outcome correctly updates ExternalId**
    - **Validates: Requirements 7.3, 7.4**
    - For a batch where some succeed and some fail, successful ones get ExternalId set, failed ones retain null

- [x] 9. Unit tests
  - [x] 9.1 Write unit tests for CategoryManagementVM
    - Create `RecurringTests/CategoryTests/CategoryManagementVMTests.cs`
    - Test: confirmation dialog on inactivation (3.1), cancel leaves state unchanged (3.3), system categories don't show actions (3.6, 4.4, 5.7)
    - _Requirements: 3.1, 3.3, 3.6, 4.4, 5.7_

  - [x] 9.2 Write unit tests for CategoryEditVM edit mode
    - Test: navigation with query param (5.1), pre-population of fields (5.2), empty name rejection (5.3), type change blocked (5.6)
    - _Requirements: 5.1, 5.2, 5.3, 5.6_

  - [x] 9.3 Write unit tests for CategoryPickerVM inactive filtering
    - Test: inactive categories excluded (6.1), active subcategories of inactive parent visible (6.2), transaction display preserves inactive category name (6.3), picker doesn't pre-select inactive (6.4)
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [x] 9.4 Write integration tests for sync push triggers
    - Test: sync push triggered after inactivation/reactivation/edit (3.5, 4.3, 5.5)
    - _Requirements: 3.5, 4.3, 5.5_

- [x] 10. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The project uses FsCheck.Xunit 3.2.0 with NSubstitute 5.3.0 for mocking
- Test files go in `RecurringTests/CategoryTests/` following the existing folder-per-domain convention

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["2.2", "2.3", "2.4", "3.1"] },
    { "id": 3, "tasks": ["3.2", "3.3", "5.1"] },
    { "id": 4, "tasks": ["5.2", "5.3", "5.4", "6.1"] },
    { "id": 5, "tasks": ["6.2"] },
    { "id": 6, "tasks": ["8.1", "8.2", "8.3", "8.4", "8.5", "8.6", "8.7", "8.8", "8.9"] },
    { "id": 7, "tasks": ["9.1", "9.2", "9.3", "9.4"] }
  ]
}
```
