using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.DTO;
using Model.Req;
using Model.Res;
using Model.Resp;
using Service;
using Service.Account;
using Service.Category;
using Service.Recurring;
using Service.Transaction;
using System.Collections.ObjectModel;
using XpemFinancial.Views;

namespace XpemFinancial.VMs
{
    public partial class TransactionEditVM(
        IUserSessionService userSessionService,
        ITransactionService transactionService,
        IAccountService accountService,
        IRecurringRuleService recurringRuleService,
        ICategoryService categoryService) : VMBase, IQueryAttributable
    {
        #region Campos e Propriedades

        private int TransactionId { get; set; }

        [ObservableProperty] private string transactionTypeColor;
        [ObservableProperty] private DateTime transactionDate;
        [ObservableProperty] private string description;
        [ObservableProperty] private string amount;
        [ObservableProperty] private CategoryDTO selectedCategory;
        [ObservableProperty] private List<string> categories;
        [ObservableProperty] private bool installmentPanelIsVisible = false;
        [ObservableProperty] private int numberOfInstallments;
        [ObservableProperty] private int initialInstallments;
        [ObservableProperty] private string totalAmountInstallments = "0,00";
        [ObservableProperty] private string selectedCategoryName;
        [ObservableProperty] private bool isRequired;
        [ObservableProperty] private Repetition selectedRepetition;
        [ObservableProperty] private TransactionType selectedTransactionType;
        [ObservableProperty] private bool isEditing = false;

        public ObservableCollection<CategoryDTO> FlattenedCategories { get; set; } = new();

        public ObservableCollection<TransactionDescriptionRes> DescriptionSuggestions { get; set; } = new();

        [ObservableProperty] private bool suggestionsVisible = false;

        // Esta propriedade apenas facilita a exibição no botão/label da View
        // Ela será atualizada sempre que a SelectedCategory mudar
        public string CategoryDisplayName => selectedCategory?.Name ?? "Sem Categoria";

        public string PageTitle => IsEditing ? "Editar transação" : "Adicionar transação";

        public bool IsIncome => SelectedTransactionType == TransactionType.Income;

        public string TitleIcon => SelectedTransactionType == TransactionType.Income
            ? XpemFinancial.Resources.IconFont.ArrowTrendUp
            : XpemFinancial.Resources.IconFont.ArrowTrendDown;

        [ObservableProperty] private string note;

        private EditScope EditScope;

        #endregion

        partial void OnDescriptionChanged(string value)
        {
            _ = LoadDescriptionSuggestionsAsync(value);
        }

        private async Task LoadDescriptionSuggestionsAsync(string text)
        {
            DescriptionSuggestions.Clear();

            if (string.IsNullOrWhiteSpace(text) || text.Length < 2)
            {
                SuggestionsVisible = false;
                return;
            }

            var suggestions = await transactionService.GetDescriptionSuggestionsAsync(text);
            foreach (var s in suggestions)
                DescriptionSuggestions.Add(s);

            SuggestionsVisible = DescriptionSuggestions.Any();
        }

        public async Task ApplySuggestion(TransactionDescriptionRes suggestion)
        {
            // Atribui direto ao campo para não re-disparar o hook de busca
            description = suggestion.Description;
            OnPropertyChanged(nameof(Description));

            if (suggestion.CategoryId.HasValue)
            {
                var all = await categoryService.GetAllAsync();
                var category = all.FirstOrDefault(c => c.Id == suggestion.CategoryId.Value);
                if (category is not null)
                {
                    SelectedCategory = category;
                    SelectedCategoryName = await ResolveCategoryDisplayNameAsync(category);
                }
            }

            DescriptionSuggestions.Clear();
            SuggestionsVisible = false;
        }

        private async Task<string> ResolveCategoryDisplayNameAsync(CategoryDTO category)
        {
            if (category == null) return "Sem Categoria";
            if (category.IsMainCategory || !category.ParentExternalId.HasValue) return category.Name;

            var allCategories = await categoryService.GetAllAsync();
            var parent = allCategories.FirstOrDefault(c => c.ExternalId == category.ParentExternalId.Value);
            return parent != null ? $"{parent.Name} / {category.Name}" : category.Name;
        }

        partial void OnSelectedTransactionTypeChanged(TransactionType value)
        {
            // Atualiza a cor com base no tipo de transação
            TransactionTypeColor = value == TransactionType.Income ? "#2bbf69" : "#ef4444";
            OnPropertyChanged(nameof(IsIncome));
            OnPropertyChanged(nameof(TitleIcon));
        }

        [RelayCommand]
        async Task OpenCategoryPicker()
        {
            var navigationParameter = new Dictionary<string, object>
            {        { "SelectedCategory", SelectedCategory }    };

            await Shell.Current.GoToAsync(nameof(CategoryPicker), true, navigationParameter);
        }

        //public async Task InitializeAsync()
        //{
        //    TransactionTypeColor = "#f75c5c"; //Color.FromArgb("#2bbf69"); // Cor padrão para transações de entrada
        //    TransactionDate = DateTime.Now;
        //    SelectedTransactionType = TransactionType.Expense;
        //}

        // Este método é chamado automaticamente quando a navegação volta para cá
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("SelectedCategory", out var valSelectedCategory) && valSelectedCategory is CategoryDTO selected)
            {
                SelectedCategory = selected;

                SelectedCategoryName = query.TryGetValue("SelectedCategoryDisplayName", out var displayName) && displayName is string name
                    ? name
                    : selected.Name;
                return;
            }

            if (query.TryGetValue("TransactionId", out var valTransactionId) && valTransactionId is string)
            {
                int transactionId = Convert.ToInt32(valTransactionId);

                // Carrega a transação para edição
                var transaction = transactionService.GetByIdAsync(transactionId).Result;

                if (transaction != null)
                {
                    IsEditing = true;
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
                    _ = ResolveCategoryDisplayNameAsync(SelectedCategory).ContinueWith(t => SelectedCategoryName = t.Result, TaskScheduler.FromCurrentSynchronizationContext());

                    if (transaction.Type == TransactionType.Income)
                    {
                        TransactionTypeColor = "#2bbf69";
                    }
                    else
                    {
                        TransactionTypeColor = "#f75c5c";
                    }
                }
            }
            else
            {
                TransactionTypeColor = "#f75c5c"; //Color.FromArgb("#2bbf69"); // Cor padrão para transações de entrada
                TransactionDate = DateTime.Now;
                SelectedTransactionType = TransactionType.Expense;
            }
        }

        private async Task<bool> HandleEditFlowAsync(TransactionDTO transaction, Page page)
        {
            string? scope = await page.DisplayActionSheetAsync(
                "Como deseja editar?",
                "Cancelar",
                null,
                "Editar apenas esta ocorrência",
                "Editar esta e as futuras",
                "Editar todas");

            if (scope == null || scope == "Cancelar") return false;

            EditScope = scope switch
            {
                "Editar apenas esta ocorrência" => EditScope.ThisOnly,
                "Editar esta e as futuras" => EditScope.ThisAndFuture,
                "Editar todas" => EditScope.All,
                _ => EditScope.ThisOnly,
            };

            return true;
        }

        private async Task HandleDeleteRecurringAsync(TransactionDTO transaction, Page page)
        {
            string? scope = await page.DisplayActionSheetAsync(
                "Como deseja excluir?",
                "Cancelar",
                null,
                "Excluir apenas esta ocorrência",
                "Excluir esta e as futuras",
                "Excluir todas");

            if (scope == null || scope == "Cancelar") return;

            if (scope == "Excluir apenas esta ocorrência")
            {
                await transactionService.DeleteAsync(transaction.Id, IsOn);
                _ = VMBase.ShowMessage("Sucesso", "Ocorrência excluída com sucesso!");
                await Shell.Current.GoToAsync("..");
                return;
            }

            var cancelScope = scope switch
            {
                "Excluir esta e as futuras" => CancelScope.FromThisOnwards,
                "Excluir todas" => CancelScope.EntireRule,
                _ => CancelScope.FromThisOnwards,
            };

            var req = new CancelRuleReq
            {
                TransactionId = transaction.Id,
                RecurringRuleId = transaction.RecurringRuleId!.Value,
                Scope = cancelScope,
            };

            var result = await recurringRuleService.CancelAsync(req, IsOn);

            if (result.Success)
            {
                _ = VMBase.ShowMessage("Sucesso", "Transações excluídas com sucesso!");
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                _ = VMBase.ShowMessage("Erro", result.Content?.ToString() ?? "Não foi possível excluir as transações.");
            }
        }

        /// Maps a Repetition value (used on occurrences) back to the Frequency enum used on RecurringRuleDTO.
        //private static Frequency FrequencyFromRepetition(Repetition repetition) => repetition switch
        //{
        //    Repetition.Daily => Frequency.Daily,
        //    Repetition.Weekly => Frequency.Weekly,
        //    Repetition.Monthly => Frequency.Monthly,
        //    Repetition.Yearly => Frequency.Yearly,
        //    _ => Frequency.Monthly,
        //};

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
            if (value == Repetition.Monthly)
            {
                InstallmentPanelIsVisible = true;
                InitialInstallments = 1;
            }
        }

        private async Task<bool> VerrifyFields()
        {
            bool isValid = true;

            if (string.IsNullOrWhiteSpace(Description) && string.IsNullOrWhiteSpace(SelectedCategory?.Name))
            {
                isValid = false;
                _ = VMBase.ShowMessage("Aviso", "Defina uma descrição ou selecione uma categoria!");
            }
            else if (string.IsNullOrWhiteSpace(Amount) || !decimal.TryParse(Amount, System.Globalization.NumberStyles.Currency, new System.Globalization.CultureInfo("pt-BR"), out _))
            {
                isValid = false;
            }
            else if (!decimal.TryParse(Amount, System.Globalization.NumberStyles.Currency, new System.Globalization.CultureInfo("pt-BR"), out decimal _amount))
            {
                isValid = false;
            }

            if (!isValid)
                IsRequired = true;
            else
                IsRequired = false;

            return isValid;
        }

        [RelayCommand]
        public async Task DeleteTransaction()
        {
            if (TransactionId == 0) return;

            var page = Application.Current!.Windows[0].Page!;
            var transaction = await transactionService.GetByIdAsync(TransactionId);
            if (transaction == null) return;

            if (transaction.RecurringRuleId != null)
            {
                await HandleDeleteRecurringAsync(transaction, page);
                return;
            }

            bool confirmed = await page.DisplayAlertAsync("Excluir transação", "Deseja excluir esta transação?", "Excluir", "Cancelar");
            if (!confirmed) return;

            await transactionService.DeleteAsync(TransactionId, IsOn);
            _ = VMBase.ShowMessage("Sucesso", "Transação excluída com sucesso!");
            await Shell.Current.GoToAsync("..");
        }

        [RelayCommand]
        public async Task SaveTransaction()
        {
            if (!await VerrifyFields()) return;

            decimal amountValue = decimal.Parse(Amount, System.Globalization.NumberStyles.Currency, new System.Globalization.CultureInfo("pt-BR"));

            if (SelectedTransactionType == TransactionType.Expense)
                amountValue = -Math.Abs(amountValue);
            else
                amountValue = Math.Abs(amountValue);

            var user = await userSessionService.GetCurrentUserAsync();
            var account = await accountService.GetAsync();

            // ── Edição de transação existente ────────────────────────────────
            if (IsEditing && TransactionId != 0)
            {
                var existingTransaction = await transactionService.GetByIdAsync(TransactionId);
                if (existingTransaction == null) return;

                // Ocorrência recorrente: pergunta o escopo no momento do Confirmar
                if (existingTransaction.RecurringRuleId != null)
                {
                    var page = Application.Current!.Windows[0].Page!;
                    bool confirmed = await HandleEditFlowAsync(existingTransaction, page);
                    if (!confirmed) return;

                    var result = await EditRecurrencyAsync(existingTransaction, amountValue);
                    if (!result.Success)
                    {
                        _ = VMBase.ShowMessage("Erro", result.Content?.ToString() ?? "Não foi possível editar a recorrência.");
                        return;
                    }
                    _ = VMBase.ShowMessage("Sucesso", "Transação recorrente editada com sucesso!");
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                // Transação normal: atualiza os campos editáveis
                existingTransaction.Date = TransactionDate;
                existingTransaction.Description = (string.IsNullOrEmpty(Description) ? SelectedCategory?.Name : Description)?.Trim() ?? existingTransaction.Description;
                existingTransaction.Amount = amountValue;
                existingTransaction.Type = SelectedTransactionType;
                existingTransaction.Note = Note?.Trim();
                existingTransaction.CategoryId = SelectedCategory?.Id ?? existingTransaction.CategoryId;
                existingTransaction.CategoryExternalId = SelectedCategory?.ExternalId;
                existingTransaction.UpdatedAt = DateTime.Now;

                await transactionService.UpdateAsync(existingTransaction, IsOn);
                _ = VMBase.ShowMessage("Sucesso", "Transação atualizada com sucesso!");
                await Shell.Current.GoToAsync("..");
                return;
            }

            // ── Criação de nova transação ────────────────────────────────────

            // Caminho para regras recorrentes
            if (SelectedRepetition == Repetition.Recurring)
            {
                var rule = new RecurringRuleDTO
                {
                    Description = (string.IsNullOrEmpty(Description) ? SelectedCategory?.Name : Description)?.Trim(),
                    Amount = amountValue,
                    Type = SelectedTransactionType,
                    CategoryId = SelectedCategory?.Id ?? 0,
                    CategoryExternalId = SelectedCategory?.ExternalId,
                    AccountId = account?.Id,
                    Frequency = Frequency.Monthly,
                    StartDate = TransactionDate,
                    EndDate = null,
                    UserId = user.Id,
                };

                var result = await recurringRuleService.SaveAsync(rule, IsOn);

                if (!result.Success)
                {
                    _ = VMBase.ShowMessage("Erro", result.Content?.ToString() ?? "Não foi possível salvar a regra recorrente.");
                    return;
                }

                _ = VMBase.ShowMessage("Sucesso", "Transação recorrente salva com sucesso!");
                await Shell.Current.GoToAsync("..");
                return;
            }

            var transaction = new TransactionDTO()
            {
                Date = TransactionDate,
                Amount = amountValue,
                Description = (string.IsNullOrEmpty(Description) ? SelectedCategory?.Name : Description)?.Trim() ?? string.Empty,
                Type = SelectedTransactionType,
                Repetition = SelectedRepetition,
                Note = Note?.Trim(),
                CategoryId = SelectedCategory?.Id ?? 0,
                CategoryExternalId = SelectedCategory?.ExternalId,
                AccountExternalId = account?.ExternalId,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                AccountId = account.Id
            };

            if (transaction.Repetition == Repetition.Monthly)
            {
                transaction.Installment = InitialInstallments;
                transaction.TotalInstallments = NumberOfInstallments;
            }

            await transactionService.AddAsync(transaction, IsOn);

            _ = VMBase.ShowMessage("Sucesso", "Transação salva com sucesso!");
            await Shell.Current.GoToAsync("..");
        }

        private async Task<ServiceResp> EditRecurrencyAsync(TransactionDTO existingTransaction, decimal amountValue)
        {
            // Constrói o UpdatedRule com os valores atuais do form, não da transação original
            var updatedRule = new RecurringRuleDTO
            {
                RecurringRuleId = existingTransaction.RecurringRuleId!.Value,
                Description = (string.IsNullOrEmpty(Description) ? SelectedCategory?.Name : Description)?.Trim(),
                Amount = amountValue,
                Type = SelectedTransactionType,
                CategoryId = SelectedCategory?.Id ?? existingTransaction.CategoryId.Value,
                CategoryExternalId = SelectedCategory?.ExternalId,
                AccountId = existingTransaction.AccountId,
                Frequency = Frequency.Monthly,
                StartDate = TransactionDate,
                UserId = existingTransaction.UserId,
            };

            var req = new EditOccurrenceReq
            {
                TransactionId = existingTransaction.Id,
                RecurringRuleId = existingTransaction.RecurringRuleId!.Value,
                Scope = EditScope,
                UpdatedRule = updatedRule,
            };

            return await recurringRuleService.EditOccurrenceAsync(req, IsOn);
        }
    }
}
