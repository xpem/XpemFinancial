using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.DTO;
using Service;
using Service.Category;
using XpemFinancial.Utils;

namespace XpemFinancial.VMs;

public partial class CategoryPickerVM(ICategoryService categoryService, IUserSessionService userSessionService) : VMBase
{
    private static List<CategoryDTO> _cachedCategories;
    private const int BatchSize = 20;
    private List<CategoryDTO> _currentSource = [];
    private int _loadedCount = 0;
    private bool _isLoadingMore = false;

    public event Action<CategoryDTO>? ScrollToItemRequested;

    [ObservableProperty] private RangeObservableCollection<CategoryDTO> categories = new();
    [ObservableProperty] private CategoryDTO selectedItem;
    [ObservableProperty] private string searchText;
    [ObservableProperty] private int remainingItemsThreshold = 5;

    /// <summary>
    /// filtra a lista de categorias conforme o texto de busca é alterado, utilizando o valor antigo para evitar buscas desnecessárias quando o texto é modificado para o mesmo valor ou para null/empty.
    /// </summary>
    /// <param name="oldValue"></param>
    /// <param name="newValue"></param>
    partial void OnSearchTextChanged(string? oldValue, string newValue)
    {
        if (newValue != null && oldValue != newValue)
        {
            var filtered = string.IsNullOrWhiteSpace(newValue)
                ? _cachedCategories
                : _cachedCategories.Where(x => x.Name.Contains(newValue, StringComparison.OrdinalIgnoreCase)).ToList();

            // Dispatch asynchronously to avoid mutating Categories
            // during an active CollectionChanged notification.
            MainThread.BeginInvokeOnMainThread(async () =>
                await ReloadSourceAsync(filtered, showBusy: false));
        }
    }

    public async Task InitializeAsync()
    {
        _cachedCategories ??= await categoryService.GetAllAsync();

        if (Categories.Count > 0) return;

        await ReloadSourceAsync(_cachedCategories);
    }

    // ReloadSourceAsync — usa ReplaceAll (Reset), aceitável pois é uma recarga total
    private async Task ReloadSourceAsync(List<CategoryDTO> source, bool showBusy = true)
    {
        _currentSource = source;
        _loadedCount = 0;
        if (showBusy) IsBusy = true;

        var firstBatch = _currentSource.Take(BatchSize).ToList();
        _loadedCount = firstBatch.Count;

        await MainThread.InvokeOnMainThreadAsync(() => Categories.ReplaceAll(firstBatch));

        if (showBusy) IsBusy = false;
    }

    [RelayCommand]
    private async Task LoadMore()
    {
        if (_isLoadingMore || _loadedCount >= _currentSource.Count) return;
        _isLoadingMore = true;
        RemainingItemsThreshold = -1; // desabilita o threshold durante o load

        // Salva o último item visível ANTES de carregar mais
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
            RemainingItemsThreshold = 5; // reabilita após o scroll
        }
    }

    // LoadNextBatchAsync — usa AddRange (Action.Add), preserva scroll
    private async Task LoadNextBatchAsync()
    {
        var batch = _currentSource.Skip(_loadedCount).Take(BatchSize).ToList();
        _loadedCount += batch.Count;
        await MainThread.InvokeOnMainThreadAsync(() => Categories.AddRange(batch));
    }

    [RelayCommand]
    private async Task SelectItem(CategoryDTO item)
    {
        if (item == null) return;
        var navigationParameter = new Dictionary<string, object> { { "SelectedCategory", item } };
        await Shell.Current.GoToAsync("..", true, navigationParameter);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("SelectedCategory", out var val) && val is CategoryDTO selected)
        {
            SelectedItem = selected;
            query.Clear();
        }
    }
}
