using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model;
using System.Collections.ObjectModel;
using XpemFinancial.Views;

namespace XpemFinancial.VMs
{
    public partial class CategoryPickerVM : VMBase
    {
        private static readonly List<SelectableCategory> _cachedCategories = BuildCategories();
        private const int BatchSize = 20;
        private List<SelectableCategory> _currentSource = [];
        private int _loadedCount = 0;

        [ObservableProperty]
        private ObservableCollection<SelectableCategory> categories = new();

        [ObservableProperty]
        private SelectableCategory selectedItem;

        [ObservableProperty]
        private string searchText;

        public CategoryPickerVM() { }

        public async Task InitializeAsync()
        {
            if (Categories.Count > 0) return;
            await ReloadSourceAsync(_cachedCategories);
        }

        private async Task ReloadSourceAsync(List<SelectableCategory> source)
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

        private static List<SelectableCategory> BuildCategories()
        {
            var result = new List<SelectableCategory>();
            foreach (var cat in TransactionCategories.LoadTransactionCategories())
            {
                result.Add(new SelectableCategory { Name = cat.Category, IsCategory = true });
                if (cat.Subcategories != null)
                    foreach (var sub in cat.Subcategories)
                        result.Add(new SelectableCategory { Name = sub, ParentCategory = cat.Category, IsCategory = false });
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
        private async Task SelectItem(SelectableCategory item)
        {
            if (item == null) return;
            var navigationParameter = new Dictionary<string, object> { { "SelectedCategory", item } };
            await Shell.Current.GoToAsync("..", true, navigationParameter);
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("SelectedCategory", out var val) && val is SelectableCategory selected)
            {
                SelectedItem = selected;
                query.Clear();
            }
        }
    }
}
