# Requirements Document

## Introduction

This feature adds a dedicated category management interface to XpemFinancial, allowing users to view, edit, and inactivate their custom categories. The management page is accessible from the Flyout sidebar and is separate from the existing CategoryPicker (which remains focused on selection during transaction creation). System-default categories are displayed as read-only. Inactivation uses a soft-delete approach (toggling the Inactive flag) and changes sync via the existing push mechanism.

## Glossary

- **Category_Management_Page**: A dedicated page accessible from the Flyout sidebar that displays all categories and provides edit/inactivate actions for user-created categories.
- **Category_Edit_Page**: The existing page (CategoryEditPage) adapted to support both creation and editing of categories.
- **Category_Picker**: The existing selection-only page used during transaction creation to choose a category.
- **Category**: A CategoryDTO entity representing a transaction category, which can be a main category or a subcategory.
- **User_Category**: A Category where SystemDefault is false, created by the user.
- **System_Category**: A Category where SystemDefault is true, provided by the server and not editable by the user.
- **Inactive_Category**: A Category where the Inactive flag is true, representing a soft-deleted category.
- **Main_Category**: A Category where IsMainCategory is true; may have subcategories.
- **Subcategory**: A Category where IsMainCategory is false, linked to a Main_Category via ParentExternalId.
- **Sync_Push**: The existing mechanism (CategoryService.PushAsync) that synchronizes local category changes to the server.

## Requirements

### Requirement 1: Flyout Navigation Entry

**User Story:** As a user, I want to access category management from the sidebar, so that I can manage my categories without navigating through transaction creation.

#### Acceptance Criteria

1. WHILE the user is authenticated, THE AppShell SHALL display a FlyoutItem titled "Categorias" with the Tag icon (IconFont.Tag) in the sidebar menu, positioned after the existing "Contas" FlyoutItem
2. WHEN the user taps the "Categorias" FlyoutItem, THE AppShell SHALL navigate to the Category_Management_Page

### Requirement 2: Category List Display

**User Story:** As a user, I want to see all my categories organized by hierarchy, so that I can understand the structure and find the category I want to manage.

#### Acceptance Criteria

1. WHEN the Category_Management_Page loads, THE Category_Management_Page SHALL display all User_Category and System_Category items (both active and inactive) grouped hierarchically: each Main_Category followed by its Subcategories, sorted alphabetically by name within each group
2. THE Category_Management_Page SHALL display each Main_Category in bold and each Subcategory indented under its parent
3. THE Category_Management_Page SHALL display a visual indicator (label or badge) on each System_Category to distinguish it from User_Category items
4. THE Category_Management_Page SHALL display a visual indicator (reduced opacity) on each Inactive_Category
5. WHEN the Category_Management_Page loads, THE Category_Management_Page SHALL display Inactive_Category items at the end of their respective parent group
6. IF the user has no categories, THE Category_Management_Page SHALL display an empty state message indicating no categories exist

### Requirement 3: Category Inactivation

**User Story:** As a user, I want to inactivate categories I no longer use, so that they stop appearing in the Category_Picker without losing historical transaction data.

#### Acceptance Criteria

1. WHEN the user taps the inactivate action on a User_Category, THE Category_Management_Page SHALL display a confirmation dialog asking "Deseja inativar esta categoria?"
2. WHEN the user confirms inactivation, THE Category_Management_Page SHALL set the Category Inactive flag to true and UpdatedAt to the current UTC timestamp
3. WHEN the user cancels the confirmation dialog, THE Category_Management_Page SHALL take no action and return to the list
4. WHEN the user confirms inactivation of a Main_Category that has active Subcategories, THE Category_Management_Page SHALL also inactivate all its Subcategories (setting Inactive to true and UpdatedAt to the current UTC timestamp for each)
5. WHEN a Category is inactivated, THE Category_Management_Page SHALL trigger a Sync_Push to synchronize the change to the server
6. THE Category_Management_Page SHALL NOT display inactivate actions on System_Category items

### Requirement 4: Category Reactivation

**User Story:** As a user, I want to reactivate a previously inactivated category, so that I can use it again in new transactions.

#### Acceptance Criteria

1. WHEN the user taps the reactivate action on an Inactive_Category that is a User_Category, THE Category_Management_Page SHALL set the Category Inactive flag to false and UpdatedAt to the current UTC timestamp
2. WHEN a Subcategory is reactivated and its parent Main_Category is inactive, THE Category_Management_Page SHALL also reactivate the parent Main_Category (setting Inactive to false and UpdatedAt to the current UTC timestamp)
3. WHEN a Category is reactivated, THE Category_Management_Page SHALL trigger a Sync_Push to synchronize the change to the server
4. THE Category_Management_Page SHALL NOT display reactivate actions on System_Category items

### Requirement 5: Category Editing

**User Story:** As a user, I want to edit the name of my custom categories, so that I can correct typos or rename them as my financial organization evolves.

#### Acceptance Criteria

1. WHEN the user taps the edit action on a User_Category, THE Category_Management_Page SHALL navigate to the Category_Edit_Page passing the selected Category as a query parameter
2. WHEN the Category_Edit_Page receives an existing Category, THE Category_Edit_Page SHALL pre-populate the name field with the Category's current Name and the type field with the Category's current IsMainCategory value
3. WHEN the user taps save on the Category_Edit_Page, IF the name field is empty or contains only whitespace, THEN THE Category_Edit_Page SHALL display a validation message indicating the name is required and SHALL NOT persist the change
4. WHEN the user saves an edited Category with a valid name (trimmed of leading/trailing whitespace), THE Category_Edit_Page SHALL update the Category Name, UpdatedAt (to current UTC timestamp), and persist the change locally
5. WHEN an edited Category is saved, THE Category_Edit_Page SHALL trigger a Sync_Push to synchronize the change to the server
6. IF the edited Category is a Main_Category that has active Subcategories (Inactive is false), THEN THE Category_Edit_Page SHALL NOT allow changing its type to Subcategory
7. THE Category_Management_Page SHALL NOT display edit actions on System_Category items
8. WHEN a Category Name is updated, all existing transactions that reference that Category via CategoryId SHALL automatically reflect the new name (the reference is by ID, not by stored name copy)

### Requirement 6: Category Picker Filtering

**User Story:** As a user, I want the Category_Picker to hide inactive categories, so that I only see relevant options when creating transactions.

#### Acceptance Criteria

1. THE Category_Picker SHALL exclude from the displayed list all Category items where Inactive is true
2. IF a Main_Category is Inactive and has active Subcategories, THEN THE Category_Picker SHALL still exclude that parent Category from the displayed list while continuing to display its active Subcategories
3. WHILE a transaction references an Inactive_Category, THE transaction display SHALL continue to show the Category name as a non-editable label, preserving the stored category association unchanged
4. WHEN a user edits a transaction that references an Inactive_Category, THE Category_Picker SHALL not pre-select the Inactive_Category and SHALL require the user to select an active Category before saving

### Requirement 7: Sync Integration

**User Story:** As a user, I want my category changes to synchronize to the server, so that my data stays consistent across devices.

#### Acceptance Criteria

1. WHEN a User_Category is created locally or its Name or Inactive flag is modified, THE CategoryService SHALL set the Category's UpdatedAt to the current UTC timestamp so that the Category qualifies as pending push
2. WHEN PushAsync executes, THE CategoryService SHALL retrieve all categories where CategoryId is not empty and ExternalId is null, and include each in the push payload sent to the server API
3. WHEN the server responds successfully to a pushed Category, THE CategoryService SHALL store the returned ExternalId on the local Category record, removing it from the pending push set
4. IF a Sync_Push fails for a Category (network error or non-success API response), THEN THE CategoryService SHALL leave the Category's ExternalId as null so that it remains in the pending push set and is retried on the next sync cycle without blocking the remaining categories in the batch
