using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.DTO;
using Service;
using Service.Category;
using XpemFinancial.Utils;
using XpemFinancial.Views;

namespace XpemFinancial.VMs;

public partial class CategoryPickerVM(ICategoryService categoryService, IUserSessionService userSessionService) : VMBase, IQueryAttributable
{
    // Instance-level cache: scoped to this navigation instance.
    private List<CategoryDTO> _cachedCategories = [];
    // Pre-computed normalised names to avoid running RemoveDiacritics on every keystroke.
    private List<string> _cachedNormalizedNames = [];
    private CancellationTokenSource _searchCts = new();

    /// <summary>
    /// Grouped categories for the accordion (Expander) view.
    /// </summary>
    [ObservableProperty] private List<CategoryGroup> categoryGroups = [];

    /// <summary>
    /// Flat filtered list shown during search.
    /// </summary>
    [ObservableProperty] private List<CategoryDTO> filteredCategories = [];

    /// <summary>
    /// Whether the search bar has text (controls which view is visible).
    /// </summary>
    [ObservableProperty] private bool isSearchActive;

    [ObservableProperty] private string searchText;

    // Separado do IsBusy para não esconder a lista nem desabilitar o SearchBar durante a busca
    [ObservableProperty] private bool isSearching;

    /// <summary>
    /// Empty state message shown when the type filter yields zero results.
    /// </summary>
    [ObservableProperty] private string? emptyStateMessage;

    /// <summary>
    /// Transaction type context used to filter categories by compatible CategoryType.
    /// Set via navigation parameters when opening the picker.
    /// </summary>
    private TransactionType? _transactionTypeFilter;

    // Sinaliza que o cache deve ser recarregado na próxima navegação (ex: após criar categoria)
    private bool _needsRefresh = false;

    /// <summary>
    /// Filtra a lista de categorias conforme o texto de busca é alterado.
    /// O filtro roda em background para não bloquear a UI, com debounce via CancellationToken.
    /// </summary>
    partial void OnSearchTextChanged(string? oldValue, string newValue)
    {
        if (newValue == null || oldValue == newValue) return;

        _searchCts.Cancel();
        _searchCts.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        if (string.IsNullOrWhiteSpace(newValue))
        {
            IsSearchActive = false;
            IsSearching = false;
            return;
        }

        IsSearching = true;
        IsSearchActive = true;

        Task.Run(async () =>
        {
            try
            {
                // Debounce: aguarda 150ms para evitar filtrar a cada tecla
                await Task.Delay(150, token);

                var filtered = FilterCategories(newValue);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (token.IsCancellationRequested) return;
                    FilteredCategories = filtered;
                    IsSearching = false;
                });
            }
            catch (OperationCanceledException)
            {
                // Busca cancelada por nova digitação — não faz nada
            }
            catch (Exception)
            {
                MainThread.BeginInvokeOnMainThread(() => IsSearching = false);
            }
        });
    }

    private List<CategoryDTO> FilterCategories(string searchText)
    {
        var normalizedSearch = RemoveDiacritics(searchText);
        var result = new List<CategoryDTO>(_cachedCategories.Count);
        for (int i = 0; i < _cachedCategories.Count; i++)
        {
            if (_cachedNormalizedNames[i].Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                result.Add(_cachedCategories[i]);
        }
        return result;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    public async Task InitializeAsync()
    {
        if (_cachedCategories.Count == 0 || _needsRefresh)
        {
            IsBusy = true;

            var allCategories = await categoryService.GetAllAsync();
            // Filter out inactive categories — each category is evaluated by its own Inactive flag only,
            // so active subcategories of inactive parents remain visible.
            var activeCategories = allCategories.Where(c => !c.Inactive).ToList();
            // Apply transaction type filter to show only compatible categories.
            _cachedCategories = FilterByTransactionType(activeCategories, _transactionTypeFilter);
            // Pre-compute normalised names once per navigation instance.
            _cachedNormalizedNames = _cachedCategories.Select(x => RemoveDiacritics(x.Name)).ToList();
            _needsRefresh = false;
        }

        // Show empty state message when filter yields zero results
        EmptyStateMessage = _cachedCategories.Count == 0
            ? "Nenhuma categoria disponível para este tipo de transação"
            : null;

        // Build grouped data for accordion
        CategoryGroups = BuildGroups();

        IsBusy = false;
    }

    /// <summary>
    /// Builds category groups: each parent with its children.
    /// Orphan subcategories (parent not in the list) are shown as standalone groups.
    /// </summary>
    private List<CategoryGroup> BuildGroups()
    {
        var parentIds = new HashSet<int>(_cachedCategories
            .Where(c => c.IsMainCategory && c.ExternalId.HasValue)
            .Select(c => c.ExternalId!.Value));

        var groups = new List<CategoryGroup>();

        foreach (var cat in _cachedCategories)
        {
            if (cat.IsMainCategory)
            {
                var children = _cachedCategories
                    .Where(c => !c.IsMainCategory && c.ParentExternalId == cat.ExternalId)
                    .ToList();

                groups.Add(new CategoryGroup { Parent = cat, Children = children });
            }
            // Orphan subcategory (parent not present after filtering)
            else if (!cat.ParentExternalId.HasValue || !parentIds.Contains(cat.ParentExternalId.Value))
            {
                groups.Add(new CategoryGroup { Parent = cat, Children = [] });
            }
        }

        return groups;
    }

    /// <summary>
    /// Filters categories based on the transaction type context.
    /// Income → show Income + Both; Expense → show Expense + Both;
    /// Transfer/Adjustment/null → show all.
    /// Subcategories without an explicit type inherit the type from their parent.
    /// </summary>
    public static List<CategoryDTO> FilterByTransactionType(
        List<CategoryDTO> categories,
        TransactionType? transactionType)
    {
        if (transactionType is null
            || transactionType == TransactionType.Transfer
            || transactionType == TransactionType.Adjustment)
            return categories;

        var targetType = transactionType == TransactionType.Income
            ? CategoryType.Income
            : CategoryType.Expense;

        // Index parents by ExternalId for quick lookup
        var parentLookup = categories
            .Where(c => c.IsMainCategory && c.ExternalId.HasValue)
            .ToDictionary(c => c.ExternalId!.Value);

        return categories
            .Where(c =>
            {
                // Category has explicit compatible type
                if (c.Type == targetType) return true;

                // Subcategory without type → inherit from parent
                if (c.Type == null && !c.IsMainCategory && c.ParentExternalId.HasValue)
                {
                    return parentLookup.TryGetValue(c.ParentExternalId.Value, out var parent)
                        && (parent.Type == targetType || parent.Type == null);
                }

                // Main category without type → show (type-agnostic)
                return c.Type == null;
            })
            .ToList();
    }

    [RelayCommand]
    private async Task AddCategory()
    {
        _needsRefresh = true;
        await Shell.Current.GoToAsync(nameof(Views.CategoryEditPage));
    }

    [RelayCommand]
    private async Task SelectItem(CategoryDTO item)
    {
        if (item == null) return;

        string displayName = item.Name;
        if (!item.IsMainCategory && item.ParentExternalId.HasValue)
        {
            var parent = _cachedCategories?.FirstOrDefault(c => c.ExternalId == item.ParentExternalId.Value);
            if (parent != null)
                displayName = $"{parent.Name} / {item.Name}";
        }

        var navigationParameter = new Dictionary<string, object>
        {
            { "SelectedCategory", item },
            { "SelectedCategoryDisplayName", displayName }
        };
        await Shell.Current.GoToAsync("..", true, navigationParameter);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("TransactionType", out var typeVal) && typeVal is TransactionType transactionType)
        {
            _transactionTypeFilter = transactionType;
        }

        query.Clear();
    }
}
