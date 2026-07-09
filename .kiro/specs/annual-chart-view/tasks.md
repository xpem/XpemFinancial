# Implementation Plan: Annual Chart View

## Overview

This plan extends the existing `ChartPage` to support an annual viewing mode alongside the current monthly mode. The implementation progresses from data layer (repository + service), through ViewModel logic, to View/XAML updates, and finally testing. Each step builds incrementally on the previous one so there is no orphaned code.

## Tasks

- [x] 1. Extend data layer with `GetByYear` method
  - [x] 1.1 Add `GetByYear` to `ITransactionRepo` and implement in `TransactionRepo`
    - Add method signature `Task<IEnumerable<TransactionDTO>> GetByYear(int year, int? accountId = null)` to the `ITransactionRepo` interface in `Repo/TransactionRepo.cs`
    - Implement the method in `TransactionRepo` class: query transactions where `Date.Year == year`, optionally filtered by `accountId`
    - _Requirements: 9.1_

  - [x] 1.2 Add `GetByYear` to `ITransactionService` and implement in `TransactionService`
    - Add method signature `Task<IEnumerable<TransactionDTO>> GetByYear(int year, int? accountId = null)` to the `ITransactionService` interface in `Services/Transaction/TransactionService.cs`
    - Implement the method in `TransactionService`: delegate to the repo's `GetByYear`
    - _Requirements: 9.1_

- [x] 2. Extend `ChartVM` with annual mode support
  - [x] 2.1 Add mode state and Top 10 observable properties to `ChartVM`
    - Add `[ObservableProperty] private bool isAnnualMode;` field
    - Add `[ObservableProperty] private ObservableCollection<TransactionDTO> topExpenses = [];` field
    - Add `public int XAxisPointCount { get; private set; }` property (12 for annual, DaysInMonth for monthly)
    - Add `public string[]? XAxisLabels { get; private set; }` property (Portuguese month abbreviations for annual, null for monthly)
    - Add `private CancellationTokenSource? _loadCts;` field for race-condition prevention
    - _Requirements: 1.2, 1.3, 1.4, 3.4_

  - [x] 2.2 Implement `SetScope` command and annual data loading in `ChartVM`
    - Add `[RelayCommand] private async Task SetScope(bool annual)` that sets `IsAnnualMode`, updates `XAxisPointCount`/`XAxisLabels`, and triggers the appropriate load method
    - Implement `LoadAnnualChartAsync(DateTime date)` method: calls `transactionService.GetByYear(date.Year)`, computes cumulative monthly series (12 points each for income/expense), excludes Transfer type, ignores `IncludePreviousBalance`, sets `MaxValue >= 1`, calls `ComputeTop10`, fires `DataChanged`
    - Update existing `LoadChartAsync` to also set `XAxisPointCount = DaysInMonth`, `XAxisLabels = null`, and call `ComputeTop10`
    - Add cancellation token logic to both load methods: cancel previous `_loadCts`, create new one, check token before applying results
    - _Requirements: 1.3, 1.4, 1.5, 3.1, 3.2, 3.3, 3.5, 5.4, 7.1, 9.2, 9.3, 9.4_

  - [x] 2.3 Rename navigation commands and implement period-aware navigation
    - Rename `LoadPreviousMonth` → `LoadPreviousPeriod` and `LoadNextMonth` → `LoadNextPeriod`
    - In `LoadPreviousPeriod`: if `IsAnnualMode`, subtract 1 year and call `LoadAnnualChartAsync`; else subtract 1 month and call `LoadChartAsync`
    - In `LoadNextPeriod`: if `IsAnnualMode`, add 1 year and call `LoadAnnualChartAsync`; else add 1 month and call existing logic (including scheduler)
    - Update `MonthYearDisplay`: use `date.Year.ToString()` for annual mode, keep `date.ToString("MMMM/yyyy")` for monthly mode
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 7.2_

  - [x] 2.4 Implement `ComputeTop10` method in `ChartVM`
    - Add `private void ComputeTop10(IEnumerable<TransactionDTO> transactions)` method
    - Filter to `Type == Expense` and `!Inactive`, order by `Math.Abs(Amount)` descending, then by `Date` descending as secondary sort, take 10
    - Assign result to `TopExpenses` observable collection
    - Call `ComputeTop10` at the end of both `LoadChartAsync` and `LoadAnnualChartAsync`
    - _Requirements: 6.2, 6.3, 6.4, 6.5, 6.7, 6.10_

  - [x] 2.5 Write property tests for annual series computation (Properties 1, 2, 4, 7, 12)
    - **Property 1: Annual series always produce exactly 12 data points**
    - **Property 2: Annual cumulative values are monotonically non-decreasing**
    - **Property 4: Transfer transactions are excluded from chart series**
    - **Property 7: Annual mode ignores previous balance**
    - **Property 12: MaxValue is always at least 1**
    - Extract the annual aggregation logic into a testable static/pure method (e.g., `ComputeAnnualSeries`)
    - Use FsCheck.Xunit with `Arb<List<TransactionDTO>>` generators
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.5, 4.4, 5.4, 9.2, 9.3, 9.4**

  - [x] 2.6 Write property tests for Top 10 computation (Properties 8, 9, 10, 11)
    - **Property 8: Top 10 list contains at most 10 items sorted by descending absolute amount**
    - **Property 9: Top 10 list only includes Expense-type transactions**
    - **Property 10: Top 10 list filters by active period**
    - **Property 11: Top 10 secondary sort uses date descending for equal amounts**
    - Use FsCheck.Xunit with generators for mixed transaction types
    - **Validates: Requirements 6.2, 6.3, 6.4, 6.5, 6.7, 6.10**

- [x] 3. Generalize `LineChartDrawable` for both modes
  - [x] 3.1 Replace `DaysInMonth` with `XAxisPointCount` and add `XAxisLabels` support
    - In `LineChartDrawable`, replace the `DaysInMonth` property with `public int XAxisPointCount { get; set; } = 30;`
    - Add `public string[]? XAxisLabels { get; set; }` property
    - Rename `DayToX` to `PointToX` and update formula to use `XAxisPointCount` instead of `DaysInMonth`
    - Update X-axis label rendering: when `XAxisLabels` is null, use existing step-based numeric labels (preserve current logic); when `XAxisLabels` is not null, render each label at the corresponding X position
    - Update all internal references from `DaysInMonth` to `XAxisPointCount`
    - _Requirements: 3.4, 4.1, 9.4_

  - [x] 3.2 Write property tests for monthly chart behavior (Properties 3, 4)
    - **Property 3: Monthly points appear only on days with transactions**
    - **Property 4: Transfer transactions are excluded from chart series (monthly mode)**
    - Extract monthly series computation into a testable method
    - **Validates: Requirements 4.2, 4.3, 4.4**

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Update `ChartPage.xaml` and code-behind
  - [x] 5.1 Add Scope Toggle control to `ChartPage.xaml`
    - Add a segmented control (two-button `HorizontalStackLayout` or `RadioButton` group) with options "Mensal" and "Anual", positioned above the chart area and below the navigation controls
    - Bind to `SetScopeCommand` passing `true` for annual, `false` for monthly
    - Default selection: "Mensal"
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [x] 5.2 Update navigation bindings and conditional visibility
    - Update `Command="{Binding LoadPreviousMonthCommand}"` → `Command="{Binding LoadPreviousPeriodCommand}"`
    - Update `Command="{Binding LoadNextMonthCommand}"` → `Command="{Binding LoadNextPeriodCommand}"`
    - Wrap the "Incluir saldo anterior" `HorizontalStackLayout` with `IsVisible` binding to the negation of `IsAnnualMode` (visible only in monthly mode)
    - _Requirements: 2.1, 2.2, 5.1, 5.2, 5.3, 5.5_

  - [x] 5.3 Replace the transaction list section with Top 10 list
    - Replace the existing "Transações" `CollectionView` section with a "Top 10 Maiores Despesas" section
    - Bind `ItemsSource` to `TopExpenses` and `SelectedItem` to `SelectedTransaction`
    - Display each item with: description, date (dd/MM), amount, and recurring icon when `RecurringRuleId` is set (reuse existing `DataTemplate` pattern)
    - Set `EmptyView` to show message: "Nenhuma despesa encontrada para o período selecionado."
    - _Requirements: 6.1, 6.6, 6.7, 6.8, 6.9_

  - [x] 5.4 Update `ChartPage.xaml.cs` code-behind to pass new drawable properties
    - In `OnDataChanged`, update to also pass `XAxisPointCount` and `XAxisLabels` from the VM to the drawable
    - Replace `_drawable.DaysInMonth = _vm.DaysInMonth;` with `_drawable.XAxisPointCount = _vm.XAxisPointCount;` and add `_drawable.XAxisLabels = _vm.XAxisLabels;`
    - _Requirements: 3.4, 4.1_

- [x] 6. Wire navigation and mode-switch property tests
  - [x] 6.1 Write property tests for navigation (Property 5)
    - **Property 5: Year navigation advances or retreats by exactly 1**
    - Test that N consecutive forward navigations in annual mode produce year Y + N
    - Test that period label is 4-digit year string in annual mode
    - **Validates: Requirements 2.1, 2.2, 2.3**

  - [x] 6.2 Write property test for IncludePreviousBalance preservation (Property 6)
    - **Property 6: IncludePreviousBalance round-trip across mode switches**
    - Test that switching Monthly → Annual → Monthly preserves the persisted value
    - **Validates: Requirements 5.3, 5.5**

- [x] 7. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The existing `RecurringTests` project already has FsCheck.Xunit 3.2.0, xUnit, and NSubstitute configured
- The `ChartPoint` record remains unchanged; in annual mode `Day` represents the month index (1–12)
- The `ComputeTop10` and `ComputeAnnualSeries` logic should be extracted into testable pure methods to enable property-based testing

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1"] },
    { "id": 1, "tasks": ["1.2", "3.1"] },
    { "id": 2, "tasks": ["2.2", "2.3", "2.4"] },
    { "id": 3, "tasks": ["2.5", "2.6", "3.2", "5.4"] },
    { "id": 4, "tasks": ["5.1", "5.2", "5.3"] },
    { "id": 5, "tasks": ["6.1", "6.2"] }
  ]
}
```
