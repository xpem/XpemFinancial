# Requirements Document

## Introduction

This feature evolves the existing monthly chart page (ChartPage) to support an Annual View through a scope toggle control. The annual view provides users with a strategic long-term perspective on cash flow, allowing them to identify the most expensive months and largest individual expenses of the year. Both monthly and annual modes display a "Top 10 Maiores Despesas" section replacing the full transaction list.

## Glossary

- **Chart_Page**: The existing page (ChartPage) that renders a cumulative line chart and transaction list for a given time period.
- **Scope_Toggle**: A segmented control displayed on Chart_Page that allows the user to switch between "Mensal" (monthly) and "Anual" (annual) viewing modes.
- **Monthly_Mode**: The chart scope where the X axis represents days of the selected month and navigation moves month-by-month.
- **Annual_Mode**: The chart scope where the X axis represents the 12 months (Jan–Dec) of the selected year and navigation moves year-by-year.
- **Cumulative_Income_Series**: The green line series showing running total of Income transactions over the selected period.
- **Cumulative_Expense_Series**: The red line series showing running total of Expense transactions (absolute values) over the selected period.
- **Top_10_List**: A ranked list of the 10 largest individual Expense transactions (excluding Transfer type) within the selected period.
- **Previous_Balance_Checkbox**: The "Incluir saldo anterior" checkbox that adds the balance from prior periods to the chart starting point.
- **Period_Navigator**: The arrow-based control for moving backward and forward through time periods (months or years).
- **Transaction_Service**: The service layer (ITransactionService) responsible for retrieving transaction data.
- **Loading_Indicator**: The ActivityIndicator shown during asynchronous data loading operations.

## Requirements

### Requirement 1: Scope Toggle Control

**User Story:** As a user, I want to switch between monthly and annual views on the chart page, so that I can analyze my finances at different time scales.

#### Acceptance Criteria

1. THE Chart_Page SHALL display a Scope_Toggle with two options labeled "Mensal" and "Anual", positioned above the chart area and below the navigation controls.
2. WHEN the Chart_Page loads, THE Scope_Toggle SHALL default to the "Mensal" option.
3. WHEN the user selects "Anual" on the Scope_Toggle, THE Chart_Page SHALL switch to Annual_Mode, reload chart data for the year of the currently selected date, and display cumulative income and expense series with months (1–12) on the X-axis.
4. WHEN the user selects "Mensal" on the Scope_Toggle, THE Chart_Page SHALL switch to Monthly_Mode, reload chart data for the month of the currently selected date, and display cumulative income and expense series with days (1–DaysInMonth) on the X-axis.
5. WHILE data is being reloaded after a Scope_Toggle selection, THE Chart_Page SHALL display the existing loading indicator until the data load completes.

### Requirement 2: Period Navigation in Annual Mode

**User Story:** As a user, I want to navigate between years when in annual mode, so that I can compare annual cash flow across different years.

#### Acceptance Criteria

1. WHILE the Chart_Page is in Annual_Mode, WHEN the user taps the forward arrow, THE Period_Navigator SHALL advance the selected date by one year and reload the chart data for the new year.
2. WHILE the Chart_Page is in Annual_Mode, WHEN the user taps the backward arrow, THE Period_Navigator SHALL move the selected date back by one year and reload the chart data for the new year.
3. WHILE the Chart_Page is in Annual_Mode, THE Period_Navigator SHALL display only the four-digit year (e.g., "2025") as the period label.
4. WHILE the Chart_Page is in Monthly_Mode, THE Period_Navigator SHALL maintain the existing month-by-month navigation behavior and "MMMM/yyyy" label format.

### Requirement 3: Annual Chart Rendering

**User Story:** As a user, I want to see cumulative income and expenses across all 12 months of a year, so that I can identify seasonal patterns and the most expensive months.

#### Acceptance Criteria

1. WHILE the Chart_Page is in Annual_Mode, THE Cumulative_Income_Series SHALL display exactly 12 data points on the X axis, one for each month from January through December of the selected year.
2. WHILE the Chart_Page is in Annual_Mode, THE Cumulative_Expense_Series SHALL display exactly 12 data points on the X axis, one for each month from January through December of the selected year.
3. WHILE the Chart_Page is in Annual_Mode, THE Chart_Page SHALL compute each monthly data point as the running cumulative sum of all Income or Expense transactions from the start of the selected year through the end of that month, such that a month with no new transactions carries forward the cumulative value of the previous month unchanged.
4. WHILE the Chart_Page is in Annual_Mode, THE Chart_Page SHALL label the X axis with month abbreviations (Jan, Fev, Mar, Abr, Mai, Jun, Jul, Ago, Set, Out, Nov, Dez).
5. IF the Chart_Page is in Annual_Mode and the selected year contains no Income or Expense transactions, THEN THE Chart_Page SHALL render both series with 12 data points each having a value of zero.

### Requirement 4: Monthly Chart Behavior Preservation

**User Story:** As a user, I want the monthly chart to continue working exactly as before, so that my existing workflow is not disrupted.

#### Acceptance Criteria

1. WHILE the Chart_Page is in Monthly_Mode, THE Chart_Page SHALL display the X axis with a range from 1 through the total number of days in the selected month.
2. WHILE the Chart_Page is in Monthly_Mode, THE Cumulative_Income_Series SHALL plot a data point only on each day that has at least one Income transaction, with the value equal to the running total of all Income transaction amounts from day 1 through that day.
3. WHILE the Chart_Page is in Monthly_Mode, THE Cumulative_Expense_Series SHALL plot a data point only on each day that has at least one Expense transaction, with the value equal to the running total of all Expense transaction absolute amounts from day 1 through that day.
4. WHILE the Chart_Page is in Monthly_Mode, THE Chart_Page SHALL exclude transactions of type Transfer from both the Cumulative_Income_Series and the Cumulative_Expense_Series.

### Requirement 5: Previous Balance Checkbox Visibility

**User Story:** As a user, I want the "Incluir saldo anterior" option to be available only in monthly mode, so that the annual view remains focused on the year's own data.

#### Acceptance Criteria

1. WHILE the Chart_Page is in Monthly_Mode, THE Previous_Balance_Checkbox SHALL be visible and enabled, allowing the user to toggle it and affecting chart rendering as per existing behavior.
2. WHILE the Chart_Page is in Annual_Mode, THE Chart_Page SHALL hide the Previous_Balance_Checkbox so that it is not visible or interactable to the user.
3. WHEN the user switches from Monthly_Mode to Annual_Mode, THE Chart_Page SHALL preserve the persisted IncludePreviousBalance value in the user profile without modification.
4. WHILE the Chart_Page is in Annual_Mode, THE Chart_Page SHALL render the chart without applying the previous balance, regardless of the persisted IncludePreviousBalance value.
5. WHEN the user switches from Annual_Mode back to Monthly_Mode, THE Chart_Page SHALL display the Previous_Balance_Checkbox with its persisted state and resume applying it to chart rendering accordingly.

### Requirement 6: Top 10 Largest Expenses List

**User Story:** As a user, I want to see the 10 largest individual expenses for the selected period, so that I can quickly identify where my money is going.

#### Acceptance Criteria

1. THE Chart_Page SHALL display a "Top 10 Maiores Despesas" section below the chart in both Monthly_Mode and Annual_Mode.
2. THE Top_10_List SHALL contain at most 10 items, ranked by absolute amount in descending order.
3. THE Top_10_List SHALL include only transactions where Type equals Expense.
4. WHILE the Chart_Page is in Monthly_Mode, THE Top_10_List SHALL filter expenses to the selected month.
5. WHILE the Chart_Page is in Annual_Mode, THE Top_10_List SHALL filter expenses to the selected year.
6. WHEN the user taps an item in the Top_10_List, THE Chart_Page SHALL navigate to TransactionEditPage for the selected transaction.
7. WHEN the selected period has fewer than 10 Expense transactions, THE Top_10_List SHALL display only the available transactions.
8. WHEN the selected period has zero Expense transactions, THE Top_10_List SHALL display a text message indicating that no expenses exist for the selected period.
9. THE Top_10_List SHALL display each item showing the transaction description, the date formatted as dd/MM, the amount, and a recurring icon when the transaction has a RecurringRuleId.
10. WHEN two or more expenses share the same absolute amount, THE Top_10_List SHALL use transaction date (most recent first) as the secondary sort order.

### Requirement 7: Loading Indicator During Mode Transitions

**User Story:** As a user, I want visual feedback when data is loading, so that I know the app is processing my request.

#### Acceptance Criteria

1. WHEN the user changes the Scope_Toggle selection, THE Loading_Indicator SHALL be displayed until data loading completes.
2. WHEN the user navigates to a different period via the Period_Navigator, THE Loading_Indicator SHALL be displayed until data loading completes.
3. WHEN data loading completes or fails, THE Loading_Indicator SHALL be hidden.

### Requirement 8: Chart Legend Consistency

**User Story:** As a user, I want the chart legend to remain consistent across both modes, so that I can always understand what the chart series represent.

#### Acceptance Criteria

1. THE Chart_Page SHALL display the legend with exactly two entries: "Entradas acumuladas" indicated by a green (#2bbf69) color marker, and "Saídas acumuladas" indicated by a red (#f75c5c) color marker.
2. WHEN the user switches between Monthly_Mode and Annual_Mode, THE Chart_Page SHALL preserve the legend text, color markers, and ordering without modification.

### Requirement 9: Annual Data Retrieval

**User Story:** As a user, I want the annual view to aggregate data from all 12 months efficiently, so that the chart loads in a reasonable time.

#### Acceptance Criteria

1. WHEN the Chart_Page enters Annual_Mode or navigates to a different year, THE Transaction_Service SHALL retrieve all transactions for the selected year (January through December).
2. WHILE the Chart_Page is in Annual_Mode, THE Chart_Page SHALL aggregate transactions by month, computing cumulative income (running sum of Income-type transaction amounts from January through December) and cumulative expense (running sum of absolute Expense-type transaction amounts from January through December), producing one data point per month (12 points per series).
3. THE Chart_Page SHALL exclude transactions of type Transfer from chart series computations in both Monthly_Mode and Annual_Mode.
4. WHILE the Chart_Page is in Annual_Mode, THE Chart_Page SHALL set the X-axis to 12 fixed points (one per calendar month, January through December) and scale the Y-axis based on the maximum cumulative value across both the income and expense series, using a minimum Y-axis ceiling of 1 to prevent division by zero.
