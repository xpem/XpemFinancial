using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model;
using Model.DTO;
using Service;
using System.Collections.ObjectModel;
using XpemFinancial.Views;

namespace XpemFinancial.VMs
{
    public partial class CategoryPickerVM(ICategoryService categoryService, IUserSessionService userSessionService) : VMBase
    {
        private static List<CategoryDTO> _cachedCategories;
        private const int BatchSize = 20;
        private List<CategoryDTO> _currentSource = [];
        private int _loadedCount = 0;

        [ObservableProperty]
        private ObservableCollection<CategoryDTO> categories = new();

        [ObservableProperty]
        private CategoryDTO selectedItem;

        [ObservableProperty]
        private string searchText;

        /// <summary>
        /// filtra a lista de categorias conforme o texto de busca é alterado, utilizando o valor antigo para evitar buscas desnecessárias quando o texto é modificado para o mesmo valor ou para null/empty.
        /// </summary>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        partial void OnSearchTextChanged(string? oldValue, string newValue)
        {
            if ((newValue != null) && (oldValue != newValue))
            {
                var filtered = string.IsNullOrWhiteSpace(newValue)
                    ? _cachedCategories
                    : _cachedCategories.Where(x => x.Name.Contains(newValue, StringComparison.OrdinalIgnoreCase)).ToList();

                _ = ReloadSourceAsync(filtered);
            }
        }


        public async Task InitializeAsync()
        {
            var user = await userSessionService.GetCurrentUserAsync();

            if (_cachedCategories == null)
                _cachedCategories = await categoryService.GetAllAsync();

            if (Categories.Count > 0) return;

            await ReloadSourceAsync(_cachedCategories);
        }

        private async Task ReloadSourceAsync(List<CategoryDTO> source)
        {
            _currentSource = source;
            _loadedCount = 0;
            IsBusy = true;
            Categories.Clear();
            await LoadNextBatchAsync();
            IsBusy = false;
        }

        [RelayCommand]
        private async Task LoadMore()
        {
            if (_loadedCount >= _currentSource.Count) return;
            await LoadNextBatchAsync();
        }

        private async Task LoadNextBatchAsync()
        {
            var batch = _currentSource.Skip(_loadedCount).Take(BatchSize).ToList();
            foreach (var item in batch)
                Categories.Add(item);

            _loadedCount += batch.Count;
            await Task.Yield();
        }

        partial void OnSearchTextChanged(string value)
        {
            var filtered = string.IsNullOrWhiteSpace(value)
                ? _cachedCategories
                : _cachedCategories.Where(x => x.Name.Contains(value, StringComparison.OrdinalIgnoreCase)).ToList();

            _ = ReloadSourceAsync(filtered);
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
}
