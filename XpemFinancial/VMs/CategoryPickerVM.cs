using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model;
using System.Collections.ObjectModel;
using XpemFinancial.Views;

namespace XpemFinancial.VMs
{
    public partial class CategoryPickerVM : VMBase
    {
        private static readonly List<Category> _cachedCategories = BuildCategories();
        private const int BatchSize = 20;
        private List<Category> _currentSource = [];
        private int _loadedCount = 0;

        [ObservableProperty]
        private ObservableCollection<Category> categories = new();

        [ObservableProperty]
        private Category selectedItem;

        [ObservableProperty]
        private string searchText;

        public CategoryPickerVM() { }

        public async Task InitializeAsync()
        {
            if (Categories.Count > 0) return;
            await ReloadSourceAsync(_cachedCategories);
        }

        private async Task ReloadSourceAsync(List<Category> source)
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

        private static List<Category> BuildCategories()
        {
            var result = new List<Category>();
            foreach (var cat in Model.Categories.TransactionCategories.LoadTransactionCategories())
            {
                result.Add(new Category { Name = cat.Category, IsCategory = true });
                if (cat.Subcategories != null)
                    foreach (var sub in cat.Subcategories)
                        result.Add(new Category { Name = sub, ParentId = cat.Id, IsCategory = false });
            }
            return result;
        }

        partial void OnSearchTextChanged(string value)
        {
            var filtered = string.IsNullOrWhiteSpace(value)
                ? _cachedCategories
                : _cachedCategories.Where(x => x.Name.Contains(value, StringComparison.OrdinalIgnoreCase)).ToList();

            _ = ReloadSourceAsync(filtered);
        }

        [RelayCommand]
        private async Task SelectItem(Category item)
        {
            if (item == null) return;
            var navigationParameter = new Dictionary<string, object> { { "SelectedCategory", item } };
            await Shell.Current.GoToAsync("..", true, navigationParameter);
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("SelectedCategory", out var val) && val is Category selected)
            {
                SelectedItem = selected;
                query.Clear();
            }
        }
    }
}
