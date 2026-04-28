using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Model;
using System.Collections.ObjectModel;
using XpemFinancial.Utils;
using XpemFinancial.Views;

namespace XpemFinancial.VMs
{
    public partial class TransactionEditVM : ObservableObject, IQueryAttributable
    {
        private string transactionTypeColor;

        public string TransactionTypeColor
        {
            get => transactionTypeColor;
            set
            {
                if (transactionTypeColor != value)
                {
                    SetProperty(ref transactionTypeColor, value);
                }
            }
        }

        [ObservableProperty]
        private DateTime transactionDate;

        [ObservableProperty]
        private string description;

        [ObservableProperty]
        private string amount;

        [ObservableProperty]
        private SelectableCategory selectedCategory;

        [ObservableProperty]
        private List<string> categories;

        public ObservableCollection<SelectableCategory> FlattenedCategories { get; set; } = new();


        [ObservableProperty]
        private string selectedTransactionCategory;

        // Esta propriedade apenas facilita a exibição no botão/label da View
        // Ela será atualizada sempre que a SelectedCategory mudar
        public string CategoryDisplayName => selectedCategory?.Name ?? "Selecionar Categoria";

        [ObservableProperty]
        private string selectedCategoryName;

        [RelayCommand]
        async Task OpenCategoryPicker()
        {
            var navigationParameter = new Dictionary<string, object>
            {        { "SelectedCategory", selectedCategory }    };

            await Shell.Current.GoToAsync(nameof(CategoryPicker), true, navigationParameter);
        }

        public TransactionEditVM()
        {
            transactionTypeColor = "#f75c5c";//Color.FromArgb("#2bbf69"); // Cor padrão para transações de entrada
            transactionDate = DateTime.Now;
        }

        // Este método é chamado automaticamente quando a navegação volta para cá
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("SelectedCategory") &&
                query["SelectedCategory"] is SelectableCategory selected)
            {
                SelectedCategory = selected;

                SelectedCategoryName = selectedCategory.Name;
                // Limpa o dicionário para evitar re-processamento indesejado
                query.Clear();
            }
        }
    }
}
