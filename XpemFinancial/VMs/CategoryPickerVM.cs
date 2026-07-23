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
    // Declared static previously, which caused stale data after logout/login with a different account.
    private List<CategoryDTO> _cachedCategories = [];
    // Pre-computed normalised names to avoid running RemoveDiacritics on every keystroke.
    private List<string> _cachedNormalizedNames = [];
    private const int BatchSize = 20;
    private List<CategoryDTO> _currentSource = [];
    private int _loadedCount = 0;
    private bool _isLoadingMore = false;
    private CancellationTokenSource _searchCts = new();

    public event Action<CategoryDTO>? ScrollToItemRequested;

    [ObservableProperty] private RangeObservableCollection<CategoryDTO> categories = new();
    [ObservableProperty] private CategoryDTO selectedItem;
    [ObservableProperty] private string searchText;
    [ObservableProperty] private int remainingItemsThreshold = 5;

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

        IsSearching = true;

        Task.Run(async () =>
        {
            try
            {
                // Debounce: aguarda 150ms para evitar filtrar a cada tecla
                await Task.Delay(150, token);

                var filtered = string.IsNullOrWhiteSpace(newValue)
                    ? _cachedCategories
                    : FilterCategories(newValue);

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    if (token.IsCancellationRequested) return;
                    await ReloadSourceAsync(filtered, showBusy: false);
                    IsSearching = false;
                });
            }
            catch (OperationCanceledException)
            {
                // Busca cancelada por nova digitação — não faz nada,
                // a próxima chamada vai resetar IsSearching ao concluir
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

        await ReloadSourceAsync(_cachedCategories);
    }

    /// <summary>
    /// Filters categories based on the transaction type context.
    /// Income → show Income + Both; Expense → show Expense + Both;
    /// Transfer/Adjustment/null → show all.
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

        return categories
            .Where(c => c.Type == targetType || c.Type == null)
            .ToList();
    }

    // ReloadSourceAsync — recria a coleção para forçar o CollectionView a re-renderizar no Android
    private async Task ReloadSourceAsync(List<CategoryDTO> source, bool showBusy = true)
    {
        _currentSource = source;
        _loadedCount = 0;
        if (showBusy) IsBusy = true;

        var firstBatch = _currentSource.Take(BatchSize).ToList();
        _loadedCount = firstBatch.Count;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            // Trocar a referência da coleção força o CollectionView a re-renderizar completamente,
            // contornando o bug do Android onde eventos de Reset/Add não invalidam o layout.
            Categories = new RangeObservableCollection<CategoryDTO>(firstBatch);
        });

        if (showBusy) IsBusy = false;
    }

    [RelayCommand]
    private async Task LoadMore()
    {
        if (_isLoadingMore || _loadedCount >= _currentSource.Count) return;
        _isLoadingMore = true;
        RemainingItemsThreshold = -1;

        var anchorItem = Categories.ElementAtOrDefault(_loadedCount - 1);

        try
        {
            await LoadNextBatchAsync();
        }
        finally
        {
            if (anchorItem is not null)
                ScrollToItemRequested?.Invoke(anchorItem);

            _isLoadingMore = false;
            RemainingItemsThreshold = 5;
        }
    }

    private async Task LoadNextBatchAsync()
    {
        var batch = _currentSource.Skip(_loadedCount).Take(BatchSize).ToList();
        _loadedCount += batch.Count;
        await MainThread.InvokeOnMainThreadAsync(() => Categories.AddRange(batch));
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
        if (query.TryGetValue("SelectedCategory", out var val) && val is CategoryDTO selected)
        {
            SelectedItem = selected;
        }

        if (query.TryGetValue("TransactionType", out var typeVal) && typeVal is TransactionType transactionType)
        {
            _transactionTypeFilter = transactionType;
        }

        query.Clear();
    }
}
