using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Model;
using Model.DTO;
using Repo;
using Service;
using System.Collections.ObjectModel;
using XpemFinancial.Utils;
using XpemFinancial.Views;

namespace XpemFinancial.VMs
{
    public partial class TransactionEditVM(IUserSessionService userSessionService, ITransactionService transactionService, IAccountService accountService) : ObservableObject, IQueryAttributable
    {
        #region Campos e Propriedades

        private int TransactionId { get; set; }

        [ObservableProperty]
        private string transactionTypeColor;

        [ObservableProperty]
        private DateTime transactionDate;

        [ObservableProperty]
        private string description;

        [ObservableProperty]
        private string amount;

        [ObservableProperty]
        private CategoryDTO selectedCategory;

        [ObservableProperty]
        private List<string> categories;

        public ObservableCollection<CategoryDTO> FlattenedCategories { get; set; } = new();

        // Esta propriedade apenas facilita a exibição no botão/label da View
        // Ela será atualizada sempre que a SelectedCategory mudar
        public string CategoryDisplayName => selectedCategory?.Name ?? "Sem Categoria";

        [ObservableProperty]
        private string selectedCategoryName;

        [ObservableProperty]
        public bool isRequired;

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

        #endregion

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
            if (query.TryGetValue("TransactionId", out var valTransactionId) && valTransactionId is int transactionId and > 0)
            {
                // Carrega a transação para edição
                var transaction = transactionService.GetByIdAsync(transactionId).Result;

                if (transaction != null)
                {
                    TransactionId = transactionId;
                    TransactionDate = transaction.Date;
                    Description = transaction.Description;
                    Amount = transaction.Amount.ToString("C", new System.Globalization.CultureInfo("pt-BR"));
                    SelectedTransactionType = transaction.Type;
                    SelectedRepetition = transaction.Repetition;
                    Note = transaction.Note ?? string.Empty;
                    NumberOfInstallments = transaction.TotalInstallments ?? 0;
                    InitialInstallments = transaction.Installment ?? 0;
                    SelectedCategory = transaction.Category ?? new CategoryDTO { Id = 1, Name = "Sem Categoria" };
                    SelectedCategoryName = SelectedCategory.Name;
                }
            }

            if (query.TryGetValue("SelectedCategory", out var valSelectedCategory) && valSelectedCategory is CategoryDTO selected)
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
            if (SelectedRepetition != Repetition.Monthly)
            {
                TotalAmountInstallments = "0,00";
                return;
            }

            if (NumberOfInstallments > 0 && decimal.TryParse(Amount, System.Globalization.NumberStyles.Currency, new System.Globalization.CultureInfo("pt-BR"), out decimal totalAmount))
            {
                int installmentsToCalculate = (NumberOfInstallments + 1) - InitialInstallments;
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

        private async Task<bool> VerrifyFields()
        {
            bool isValid = true;

            if (string.IsNullOrWhiteSpace(Description))
            {
                isValid = false;
            }
            else if (string.IsNullOrWhiteSpace(Amount) || !decimal.TryParse(Amount, System.Globalization.NumberStyles.Currency, new System.Globalization.CultureInfo("pt-BR"), out _))
            {
                isValid = false;
            }
            else if (!decimal.TryParse(Amount, System.Globalization.NumberStyles.Currency, new System.Globalization.CultureInfo("pt-BR"), out decimal _amount))
            {
                isValid = false;
            }

            IsRequired = true;

            return isValid;
        }

        [RelayCommand]
        public async Task SaveTransaction()
        {
            if (!await VerrifyFields()) return;

            decimal amountValue = decimal.Parse(Amount, System.Globalization.NumberStyles.Currency, new System.Globalization.CultureInfo("pt-BR"));

            if (SelectedTransactionType == TransactionType.Expense)
            {
                amountValue = -Math.Abs(amountValue); // Garante que o valor seja negativo para despesas
            }
            else
            {
                amountValue = Math.Abs(amountValue); // Garante que o valor seja positivo para receitas
            }

            var user = await userSessionService.GetCurrentUserAsync();

            var account = await accountService.GetAsync();

            var transaction = new TransactionDTO()
            {
                Date = TransactionDate,
                Amount = amountValue,
                Description = Description.Trim(),
                Type = SelectedTransactionType,
                Repetition = SelectedRepetition,
                Note = Note?.Trim(),
                CategoryId = SelectedCategory?.Id ?? 1,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                AccountId = account.Id
            };

            if (transaction.Repetition == Repetition.Monthly)
            {
                transaction.Installment = InitialInstallments;
                transaction.TotalInstallments = NumberOfInstallments;
            }

            await transactionService.AddAsync(transaction);

            _ = VMBase.ShowMessage("Sucesso", "Transação salva com sucesso!");

            await Shell.Current.GoToAsync("..");
        }
    }
}
