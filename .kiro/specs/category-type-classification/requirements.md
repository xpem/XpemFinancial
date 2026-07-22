# Requirements Document

## Introduction

This feature adds a "Type" property to categories, classifying them as Income, Expense, or Both. This enables context-aware filtering in the CategoryPicker when creating or editing transactions, so that only relevant categories appear for the selected transaction type (e.g., "Salário" only for Income, "Aluguel" only for Expense). The type classification spans both the client (SQLite) and server (MySQL) data layers and integrates with the existing Pull/Push sync flow.

## Glossary

- **Category**: A user-defined or system-default classification label applied to transactions. Represented by `CategoryDTO` on the client and `TransactionCategoryDTO` on the server.
- **CategoryType**: An enumeration with values: Income, Expense, Both. Indicates which transaction types a category is valid for.
- **MainCategory**: A top-level category (`IsMainCategory = true`) that may have subcategories.
- **Subcategory**: A category linked to a MainCategory via `ParentExternalId`.
- **CategoryPicker**: The UI component that presents a filterable list of categories when the user assigns a category to a transaction.
- **TransactionType**: An existing enumeration on transactions (Income, Expense, Transfer, Adjustment).
- **Sync_Flow**: The Pull/Push pattern that keeps client-side SQLite and server-side MySQL data consistent.
- **SystemDefault_Category**: A category marked `SystemDefault = true`, shared across all users and not owned by a specific user.

## Requirements

### Requirement 1: CategoryType Enum Definition

**User Story:** As a developer, I want a well-defined enum for category types, so that the classification is consistent across client and server.

#### Acceptance Criteria

1. THE CategoryType enum SHALL define exactly three values: Income, Expense, and Both.
2. THE CategoryType enum SHALL use integer backing values: Income = 0, Expense = 1, Both = 2.
3. THE CategoryType enum SHALL use the same value names and integer mappings on both client and server, such that serializing on one side and deserializing on the other produces the same enum member.

### Requirement 2: Category Data Model Extension

**User Story:** As a developer, I want the category data model to include a Type property, so that categories can be classified by transaction type.

#### Acceptance Criteria

1. THE CategoryDTO on the client SHALL include a property `Type` of type `CategoryType`.
2. THE TransactionCategoryDTO on the server SHALL include a column `Type` of type `int`.
3. THE `Type` property on both client and server models SHALL initialize to `Both` (value 2) as the default value, so that existing records without an explicit type are treated as `Both` at the application level.
4. WHEN a new MainCategory is created, THE System SHALL require a CategoryType value to be provided before persisting the record.
5. WHEN a new Subcategory is created, THE System SHALL assign the Type value inherited from the parent MainCategory as defined in Requirement 5.

### Requirement 3: Server Database Migration

**User Story:** As a developer, I want the server database schema updated to store the category type, so that the classification persists on the server.

#### Acceptance Criteria

1. THE Migration SHALL add a non-nullable `Type` column of type `int` to the `TransactionCategory` table with a default value of 2 (Both).
2. WHEN the migration runs on an existing database, THE Migration SHALL set `Type = 2` (Both) for all pre-existing rows.

### Requirement 4: Client Database Migration

**User Story:** As a developer, I want the client SQLite schema updated to store the category type locally, so that filtering works offline.

#### Acceptance Criteria

1. THE Client_Migration SHALL add a `Type` column of type `int` to the `Category` table with a default value of 2 (Both).
2. WHEN the migration runs on an existing local database that already contains Category rows, THE Client_Migration SHALL set `Type = 2` (Both) for all pre-existing rows that do not already have a `Type` value assigned.
3. WHEN the migration completes, THE Client_Migration SHALL increment the local schema version number so that the migration is not re-applied on subsequent application launches.
4. IF the migration fails (e.g., due to a disk-write error), THEN THE Client_Migration SHALL preserve the existing database state without data loss and allow the migration to be retried on the next application launch.

### Requirement 5: Subcategory Type Inheritance

**User Story:** As a user, I want subcategories to inherit the type from their parent category, so that I do not need to classify each subcategory individually.

#### Acceptance Criteria

1. WHEN a subcategory is created, THE System SHALL automatically assign the same CategoryType as the parent MainCategory identified by `ParentExternalId`.
2. WHEN the user changes a MainCategory type, THE System SHALL update the Type of all subcategories of that MainCategory where `Inactive = false` to match the new type.
3. WHEN the user changes the parent of an existing subcategory, THE System SHALL update that subcategory's Type to match the new parent MainCategory's Type.
4. WHILE creating or editing a subcategory, THE CategoryEdit_UI SHALL display the inherited type as a non-editable label showing the parent's current CategoryType value.
5. IF a subcategory is created or re-parented and the parent MainCategory cannot be resolved via `ParentExternalId`, THEN THE System SHALL prevent the save and display a validation message indicating that a valid parent category is required.

### Requirement 6: Category Edit UI

**User Story:** As a user, I want to select the type when creating or editing a main category, so that I can control which transactions the category applies to.

#### Acceptance Criteria

1. WHILE creating a new MainCategory, THE CategoryEdit_UI SHALL display a required Type selector with options: Receita (Income), Despesa (Expense), Ambos (Both), with no option pre-selected.
2. WHILE editing an existing MainCategory, THE CategoryEdit_UI SHALL display the Type selector pre-filled with the current Type value and allow changing it.
3. WHILE editing a subcategory, THE CategoryEdit_UI SHALL display the Type inherited from its parent MainCategory as a read-only label.
4. WHEN the user attempts to save a MainCategory without selecting a Type, THE CategoryEdit_UI SHALL prevent the save and display a validation message indicating that a type must be selected.
5. IF the subcategory's parent MainCategory cannot be found, THEN THE CategoryEdit_UI SHALL display the Type field as empty and read-only.

### Requirement 7: CategoryPicker Filtering

**User Story:** As a user, I want the CategoryPicker to show only categories relevant to the transaction type I am creating, so that irrelevant categories do not clutter the list.

#### Acceptance Criteria

1. WHEN the CategoryPicker opens for an Income transaction, THE CategoryPicker SHALL display only active categories whose Type is Income or Both.
2. WHEN the CategoryPicker opens for an Expense transaction, THE CategoryPicker SHALL display only active categories whose Type is Expense or Both.
3. WHEN the CategoryPicker opens for a Transfer or Adjustment transaction, THE CategoryPicker SHALL display all active categories regardless of Type.
4. IF no transaction type context is provided to the CategoryPicker, THEN THE CategoryPicker SHALL display all active categories regardless of Type.
5. IF the applied type filter results in zero matching active categories, THEN THE CategoryPicker SHALL display an empty state message indicating no categories are available for the selected transaction type.
6. WHEN the CategoryPicker opens, THE CategoryPicker SHALL determine the transaction type context from the transaction currently being created or edited.

### Requirement 8: Sync Flow - Push

**User Story:** As a developer, I want the Type property included in the push payload, so that the server receives the category classification.

#### Acceptance Criteria

1. WHEN a category is pushed to the server, THE Sync_Flow SHALL include the `Type` field in the `CategoryReq` payload with the integer value from the local `CategoryDTO.Type` property.
2. WHEN the server receives an upsert request with a valid `Type` field (0, 1, or 2), THE Server SHALL persist the Type value to the `TransactionCategory` record.
3. IF the server receives a category upsert request with a `Type` value outside the valid range (not 0, 1, or 2), THEN THE Server SHALL reject the request with an error response indicating an invalid category type.

### Requirement 9: Sync Flow - Pull

**User Story:** As a developer, I want the Type property included in the pull response, so that the client receives the category classification from the server.

#### Acceptance Criteria

1. WHEN the server returns categories during pull, THE Server SHALL include the `Type` field as an integer in the `TransactionCategoryRes` response, populated from the persisted `TransactionCategory.Type` column.
2. WHEN the client processes a pulled category that matches a local record (update path) or has no local match (insert path), THE Sync_Flow SHALL assign the server `Type` integer value directly to the local `CategoryDTO.Type` property.
3. IF the server returns a category where the `Type` field is null or missing from the JSON payload, THEN THE Client SHALL assign `Both` (value 2) to the local `CategoryDTO.Type` property.
4. IF the server returns a category with a `Type` value outside the valid range (not 0, 1, or 2), THEN THE Client SHALL assign `Both` (value 2) to the local `CategoryDTO.Type` property.

### Requirement 10: SystemDefault Categories

**User Story:** As a product owner, I want system-default categories to have appropriate types assigned, so that they appear in the correct context for all users.

#### Acceptance Criteria

1. THE System SHALL assign CategoryType to each SystemDefault MainCategory as follows: "Receita" → Income; "Alimentação", "Carro", "Casa", "Educação", "Doações", "Eletrônicos", "Presentes", "Pessoais", "Impostos", "Lazer", "Saúde", "Seguro", "Transporte", "Investimentos" → Expense; "Sem categoria", "Outros" → Both.
2. WHEN a SystemDefault MainCategory has its type assigned, THE System SHALL apply the same CategoryType to all SystemDefault subcategories of that MainCategory, following the subcategory inheritance rule defined in Requirement 5.
3. WHEN a SystemDefault category type is updated on the server, THE Sync_Flow SHALL propagate the updated type to all clients on the next pull by updating the category's UpdatedAt timestamp so that it qualifies as a changed record during pull synchronization.
4. IF a new SystemDefault category is added to the server in the future, THEN THE Server SHALL assign a CategoryType value before making it available for pull, defaulting to Both (value 2) if no explicit type is specified.

### Requirement 11: Backward Compatibility

**User Story:** As a developer, I want the system to handle clients and servers at different versions gracefully, so that the rollout does not break existing functionality.

#### Acceptance Criteria

1. IF an older client sends a category upsert request without the `Type` field, THEN THE Server SHALL persist the category with `Type` set to `Both` (value 2).
2. IF an older server responds without the `Type` field, THEN THE Client SHALL assign `Both` (value 2) to the local `CategoryDTO.Type` property before persisting.
3. IF an older client sends an upsert request without the `Type` field for a category that already has a non-default Type value on the server, THEN THE Server SHALL preserve the existing `Type` value and not overwrite it with the default.
4. IF no transaction type context is provided to the CategoryPicker, THEN THE CategoryPicker SHALL display all active categories without applying type-based filtering.
