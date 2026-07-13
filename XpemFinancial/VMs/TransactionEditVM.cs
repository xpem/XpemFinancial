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
        [ObservableProperty] private string titleIcon;
        [ObservableProperty] private string note;
        [ObservableProperty] private List<AccountDTO> activeAccounts = [];
        [ObservableProperty] private AccountDTO? selectedAccount;
        [ObservableProperty] private List<AccountDTO> destinationAccounts = [];
        [ObservableProperty] private AccountDTO? selectedDestinationAccount;
        [ObservableProperty] private bool isTransfer;
        [ObservableProperty] private bool suggestionsVisible = false;
        [ObservableProperty] private string pageTitle;

        /// <summary>
        /// Stores the existing transaction's DestinationAccountId when editing a transfer, for pre-selection.
        /// </summary>
        private int? _editingDestinationAccountId;

        /// <summary>
        /// Stores the dashboard account ID passed via navigation parameter for pre-selection.
        /// </summary>
        private int? _preselectedAccountId;

        /// <summary>
        /// Stores the existing transaction's AccountId when editing, for pre-selection.
        /// </summary>
        private int? _editingTransactionAccountId;

        public ObservableCollection<CategoryDTO> FlattenedCategories { get; set; } = new();

        public ObservableCollection<TransactionDescriptionRes> DescriptionSuggestions { get; set; } = new();

        // Esta propriedade apenas facilita a exibição no botão/label da View
        // Ela será atualizada sempre que a SelectedCategory mudar
        public string CategoryDisplayName => selectedCategory?.Name ?? "Sem Categoria";

        /// <summary>
        /// Scope selected via the inline radio buttons when editing a recurring occurrence.
        /// Defaults to ThisOnly — the safest option.
        /// </summary>
        [ObservableProperty] private EditScope selectedEditScope = EditScope.ThisOnly;

        /// <summary>
        /// True when the transaction being edited belongs to a recurring rule.
        /// Drives the visibility of the edit-scope radio button panel.
        /// </summary>
        public bool IsRecurring { get; private set; }

        // Suppresses description suggestion search while the form is being populated
        // programmatically (ApplyQueryAttributes). Avoids showing the dropdown on page open.
        private bool _isLoading;

        #endregion

        partial void OnDescriptionChanged(string value)
        {
            if (_isLoading) return;
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

            if (suggestion.AccountId.HasValue)
            {
                var account = ActiveAccounts.FirstOrDefault(a => a.Id == suggestion.AccountId.Value);
                if (account is not null)
                    SelectedAccount = account;
            }

            DescriptionSuggestions.Clear();
            SuggestionsVisible = false;
        }

        [RelayCommand]
        private void DismissSuggestions()
        {
            DescriptionSuggestions.Clear();
            SuggestionsVisible = false;
        }

        /// <summary>
        /// Loads active accounts and pre-selects based on navigation parameters or default.
        /// Called from code-behind OnNavigatedTo.
        /// </summary>
        public async Task InitializeAccountsAsync()
        {
            var user = await userSessionService.GetCurrentUserAsync();
            if (user is null) return;

            var accounts = await accountService.GetActiveAsync(user.Id);
            ActiveAccounts = accounts;

            if (accounts.Count == 0) return;

            // 1. Editing existing transaction → select the transaction's account
            if (_editingTransactionAccountId.HasValue)
            {
                var match = accounts.FirstOrDefault(a => a.Id == _editingTransactionAccountId.Value);
                SelectedAccount = match ?? accounts.First();

                // Pre-select destination account for transfer editing (Req 3.4)
                if (_editingDestinationAccountId.HasValue && IsTransfer)
                {
                    FilterDestinationAccounts();
                    var destMatch = DestinationAccounts.FirstOrDefault(a => a.Id == _editingDestinationAccountId.Value);
                    SelectedDestinationAccount = destMatch;
                }

                return;
            }

            // 2. Navigated from dashboard with specific account filter → pre-select that account (Req 7.2)
            if (_preselectedAccountId.HasValue)
            {
                var match = accounts.FirstOrDefault(a => a.Id == _preselectedAccountId.Value);
                if (match is not null)
                {
                    SelectedAccount = match;
                    return;
                }
            }

            // 3. Fallback: use default account (Req 7.3)
            var defaultAccount = await accountService.GetDefaultAsync(user.Id);
            if (defaultAccount is not null && defaultAccount.IsActive)
            {
                var match = accounts.FirstOrDefault(a => a.Id == defaultAccount.Id);
                SelectedAccount = match ?? accounts.First();
            }
            else
            {
                // Default is inactive or null → first active account
                SelectedAccount = accounts.First();
            }
        }

        private async Task<string> ResolveCategoryDisplayNameAsync(CategoryDTO category)
        {
            if (category == null) return "Sem Categoria";
            if (category.IsMainCategory || !category.ParentExternalId.HasValue) return category.Name;

            var allCategories = await categoryService.GetAllAsync();
            var parent = allCategories.FirstOrDefault(c => c.ExternalId == category.ParentExternalId.Value);
            return parent != null ? $"{parent.Name} / {category.Name}" : category.Name;
        }

        private void SetTransactionTypeAttributes(TransactionType value)
        {
            // Atualiza a cor com base no tipo de transação
            switch (value)
            {
                case TransactionType.Income:
                    TransactionTypeColor = "#198754"; // Verde para entrada
                    break;
                case TransactionType.Expense:
                    TransactionTypeColor = "#ef4444"; // Vermelho para saída
                    break;
                case TransactionType.Transfer:
                    TransactionTypeColor = "#0d6efd"; // Azul para transferência
                    break;
                default:
                    TransactionTypeColor = "#6c757d"; // Cinza padrão
                    break;
            }

            switch (value)
            {
                case TransactionType.Income:
                    TitleIcon = XpemFinancial.Resources.IconFont.ArrowTrendUp;
                    break;
                case TransactionType.Expense:
                    TitleIcon = XpemFinancial.Resources.IconFont.ArrowTrendDown;
                    break;
                case TransactionType.Transfer:
                    TitleIcon = XpemFinancial.Resources.IconFont.ArrowRightArrowLeft;
                    break;
                default:
                    TitleIcon = XpemFinancial.Resources.IconFont.ArrowTrendDown;
                    break;
            }

            // Toggle IsTransfer e re-filtrar contas destino (Req 2.1, 2.2, 2.3)
            IsTransfer = value == TransactionType.Transfer;

            if (IsTransfer)
            {
                // Ocultar categoria/parcelamento/recorrência
                InstallmentPanelIsVisible = false;
                SelectedRepetition = Repetition.None;
                FilterDestinationAccounts();
            }
            else
            {
                // Restaurar visibilidade do parcelamento conforme repetição atual
                if (SelectedRepetition == Repetition.Monthly)
                    InstallmentPanelIsVisible = true;
            }
        }

        partial void OnSelectedTransactionTypeChanged(TransactionType value)
        {
            SetTransactionTypeAttributes(value);
        }

        partial void OnSelectedAccountChanged(AccountDTO? value)
        {
            // Re-filtrar contas destino excluindo a nova conta origem (Req 2.1)
            if (IsTransfer)
                FilterDestinationAccounts();
        }

        /// <summary>
        /// Filtra a lista de contas destino: todas as contas ativas excluindo a conta origem selecionada.
        /// </summary>
        private void FilterDestinationAccounts()
        {
            var originId = SelectedAccount?.Id;
            DestinationAccounts = ActiveAccounts
                .Where(a => a.Id != originId)
                .ToList();

            // Se a conta destino selecionada agora é a mesma da origem, limpar seleção
            if (SelectedDestinationAccount != null && SelectedDestinationAccount.Id == originId)
                SelectedDestinationAccount = null;
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

            // Parse dashboard account filter parameter (Task 13 — Req 7.2)
            if (query.TryGetValue("DashboardAccountId", out var dashAccountObj)
                && int.TryParse(dashAccountObj?.ToString(), out int dashAccountId))
            {
                _preselectedAccountId = dashAccountId;
            }

            if (query.TryGetValue("TransactionId", out var valTransactionId) && valTransactionId is string)
            {
                int transactionId = Convert.ToInt32(valTransactionId);

                // Carrega a transação para edição
                var transaction = transactionService.GetByIdAsync(transactionId).Result;

                if (transaction != null)
                {
                    _isLoading = true;
                    try
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

                        IsRecurring = transaction.RecurringRuleId.HasValue;
                        SelectedEditScope = EditScope.ThisOnly;
                        OnPropertyChanged(nameof(IsRecurring));

                        // Store account for pre-selection in InitializeAccountsAsync
                        _editingTransactionAccountId = transaction.AccountId;

                        // Store destination account for transfer pre-selection (Req 3.4)
                        _editingDestinationAccountId = transaction.DestinationAccountId;

                        SetTransactionTypeAttributes(transaction.Type);
                    }
                    finally
                    {
                        _isLoading = false;
                    }
                }
            }
            else
            {
                SelectedTransactionType = TransactionType.Expense;
                SetTransactionTypeAttributes(TransactionType.Expense);
                TransactionDate = DateTime.Now;
            }

            if (IsEditing)
                PageTitle = "Editar transação";
            else PageTitle = "Adicionar transação";
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

            if (!IsTransfer && SelectedCategory is null)
            {
                isValid = false;
                _ = VMBase.ShowMessage("Aviso", "Selecione uma categoria para a transação!");
            }
            else if (string.IsNullOrWhiteSpace(Description) && string.IsNullOrWhiteSpace(SelectedCategory?.Name))
            {
                isValid = false;
                _ = VMBase.ShowMessage("Aviso", "Defina uma descrição ou selecione uma categoria!");
            }
            else if (string.IsNullOrWhiteSpace(Amount) || !decimal.TryParse(Amount, System.Globalization.NumberStyles.Currency, new System.Globalization.CultureInfo("pt-BR"), out decimal parsedAmount))
            {
                isValid = false;
                _ = VMBase.ShowMessage("Aviso", "Informe um valor válido para a transação!");
            }
            else if (parsedAmount == 0)
            {
                isValid = false;
                _ = VMBase.ShowMessage("Aviso", "O valor da transação não pode ser zero!");
            }
            else if (SelectedAccount is null)
            {
                isValid = false;
                _ = VMBase.ShowMessage("Aviso", "Selecione uma conta para a transação!");
            }
            else if (IsTransfer && SelectedDestinationAccount is null)
            {
                isValid = false;
                _ = VMBase.ShowMessage("Aviso", "Selecione a conta de destino");
            }
            else if (IsTransfer && SelectedDestinationAccount?.Id == SelectedAccount?.Id)
            {
                isValid = false;
                _ = VMBase.ShowMessage("Aviso", "As contas devem ser diferentes");
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

            // ── Edição de transação existente ────────────────────────────────
            if (IsEditing && TransactionId != 0)
            {
                var existingTransaction = await transactionService.GetByIdAsync(TransactionId);
                if (existingTransaction == null) return;

                // Ocorrência recorrente: usa o escopo selecionado nos radio buttons
                if (existingTransaction.RecurringRuleId != null)
                {
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
                existingTransaction.CategoryExternalId = SelectedCategory?.ExternalId ?? existingTransaction.CategoryExternalId;
                existingTransaction.UpdatedAt = DateTime.Now;
                existingTransaction.AccountId = SelectedAccount!.Id;
                existingTransaction.AccountExternalId = SelectedAccount!.ExternalId;

                // Transfer: propaga conta destino; não-transfer: limpa campos de destino
                if (SelectedTransactionType == TransactionType.Transfer)
                {
                    existingTransaction.DestinationAccountId = SelectedDestinationAccount!.Id;
                    existingTransaction.DestinationAccountExternalId = SelectedDestinationAccount!.ExternalId;
                }
                else
                {
                    existingTransaction.DestinationAccountId = null;
                    existingTransaction.DestinationAccountExternalId = null;
                }

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
                    AccountId = SelectedAccount!.Id,
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
                AccountExternalId = SelectedAccount!.ExternalId,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                AccountId = SelectedAccount!.Id
            };

            if (SelectedTransactionType == TransactionType.Transfer)
            {
                transaction.DestinationAccountId = SelectedDestinationAccount?.Id;
                transaction.DestinationAccountExternalId = SelectedDestinationAccount?.ExternalId;
            }

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
                AccountId = SelectedAccount?.Id ?? existingTransaction.AccountId,
                Frequency = Frequency.Monthly,
                StartDate = TransactionDate,
                UserId = existingTransaction.UserId,
            };

            var req = new EditOccurrenceReq
            {
                TransactionId = existingTransaction.Id,
                RecurringRuleId = existingTransaction.RecurringRuleId!.Value,
                Scope = SelectedEditScope,
                UpdatedRule = updatedRule,
            };

            return await recurringRuleService.EditOccurrenceAsync(req, IsOn);
        }
    }
}
