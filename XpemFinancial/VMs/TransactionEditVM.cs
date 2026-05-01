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
        [ObservableProperty]
        private string transactionTypeColor;

        [ObservableProperty]
        private DateTime transactionDate;

        [ObservableProperty]
        private string description;

        [ObservableProperty]
        private string amount;

        [ObservableProperty]
        private Category selectedCategory;

        [ObservableProperty]
        private List<string> categories;

        public ObservableCollection<Category> FlattenedCategories { get; set; } = new();

        // Esta propriedade apenas facilita a exibição no botão/label da View
        // Ela será atualizada sempre que a SelectedCategory mudar
        public string CategoryDisplayName => selectedCategory?.Name ?? "Selecionar Categoria";

        [ObservableProperty]
        private string selectedCategoryName;

        [ObservableProperty]
        private Repetition selectedRepetition;

        [ObservableProperty]
        private TransactionType selectedTransactionType;

        [ObservableProperty]
        private bool installmentPanelIsVisible = false;

        [ObservableProperty]
        private int numberOfInstallments;

        [ObservableProperty]
        private int initialInstallments;

        [ObservableProperty]
        private string totalAmountInstallments = "0,00";


        [ObservableProperty]
        private string note;


        partial void OnSelectedTransactionTypeChanged(TransactionType value)
        {
            // Atualiza a cor com base no tipo de transação
            TransactionTypeColor = value == TransactionType.Income ? "#2bbf69" : "#f75c5c";
        }


        [RelayCommand]
        async Task OpenCategoryPicker()
        {
            var navigationParameter = new Dictionary<string, object>
            {        { "SelectedCategory", SelectedCategory }    };

            await Shell.Current.GoToAsync(nameof(CategoryPicker), true, navigationParameter);
        }

        public async Task InitializeAsync()
        {
            TransactionTypeColor = "#f75c5c"; //Color.FromArgb("#2bbf69"); // Cor padrão para transações de entrada
            TransactionDate = DateTime.Now;
            SelectedTransactionType = TransactionType.Expense;
        }

        // Este método é chamado automaticamente quando a navegação volta para cá
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("SelectedCategory", out var val) && val is Category selected)
            {
                SelectedCategory = selected; // só atualiza se vier com item

                SelectedCategoryName = SelectedCategory.Name;
                query.Clear();
            }
            // sem item → mantém o valor anterior ✅
        }

        partial void OnNumberOfInstallmentsChanged(int oldValue, int newValue)
        {
            if (oldValue != newValue)
            {
                UpdateTotalAmountInstallments();
            }
        }

        partial void OnAmountChanged(string? oldValue, string newValue)
        {
            if (oldValue != newValue)
            {
                UpdateTotalAmountInstallments();
            }
        }

        partial void OnInitialInstallmentsChanged(int oldValue, int newValue)
        {
            if (oldValue != newValue)
            {
                UpdateTotalAmountInstallments();
            }
        }

        private void UpdateTotalAmountInstallments()
        {
            if(SelectedRepetition != Repetition.Monthly)
            {
                TotalAmountInstallments = "0,00";
                return;
            }

            if (NumberOfInstallments > 0 && decimal.TryParse(Amount, System.Globalization.NumberStyles.Currency, new System.Globalization.CultureInfo("pt-BR"), out decimal totalAmount))
            {
                int installmentsToCalculate = NumberOfInstallments - InitialInstallments;
                decimal _totalAmountInstallments = totalAmount * installmentsToCalculate;
                TotalAmountInstallments = _totalAmountInstallments.ToString("C", new System.Globalization.CultureInfo("pt-BR"));
            }
            else
            {
                TotalAmountInstallments = "0,00";
            }
        }

        partial void OnSelectedRepetitionChanged(Repetition value)
        {
            InstallmentPanelIsVisible = value == Repetition.Monthly;
        }
    }
}
