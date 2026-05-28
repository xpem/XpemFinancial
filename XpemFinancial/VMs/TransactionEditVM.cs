using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.DTO;
using Model.Req;
using Model.Resp;
using Service;
using Service.Account;
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
        IRecurringRuleService recurringRuleService) : VMBase, IQueryAttributable
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
        private bool isRequired;

        [ObservableProperty]
        private Repetition selectedRepetition;

        [ObservableProperty]
        private TransactionType selectedTransactionType;

        [ObservableProperty]
        private bool isEditing = false;

        public string PageTitle => IsEditing ? "Editar transação" : "Adicionar transação";

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

        private EditScope EditScope;

        // Bloqueia o botão Confirmar enquanto o ActionSheet de recorrência está aberto
        private bool _awaitingRecurringAction = false;

        #endregion

        partial void OnIsEditingChanged(bool value)
        {
            OnPropertyChanged(nameof(PageTitle));
        }

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

                    // Se for uma ocorrência recorrente, apresenta as opções de edição/cancelamento
                    if (transaction.RecurringRuleId != null)
                    {
                        _awaitingRecurringAction = true;
                        _ = HandleRecurringOccurrenceAsync(transaction);
                    }
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

        private async Task HandleRecurringOccurrenceAsync(TransactionDTO transaction)
        {
            var page = Application.Current!.Windows[0].Page!;

            // Primeiro: perguntar se o usuário quer Editar ou Cancelar
            //string? action = await page.DisplayActionSheet(
            //    "Transação recorrente",
            //    null,
            //    "Editar",
            //    "Cancelar recorrência");

            //if (action == "Editar")
            //{
                await HandleEditFlowAsync(transaction, page);
            //}
            //else if (action == "Cancelar recorrência")
            //{
            //    await HandleCancelFlowAsync(transaction, page);
            //}
            _awaitingRecurringAction = false;
        }

        private async Task HandleEditFlowAsync(TransactionDTO transaction, Page page)
        {
            string? scope = await page.DisplayActionSheet(
                "Como deseja editar?",
                "Cancelar",
                null,
                "Editar apenas esta ocorrência",
                "Editar esta e as futuras",
                "Editar todas");

            //retorna para a main
            if (scope == null || scope == "Cancelar") await Shell.Current.GoToAsync("..");

            EditScope = scope switch
            {
                "Editar apenas esta ocorrência" => EditScope.ThisOnly,
                "Editar esta e as futuras" => EditScope.ThisAndFuture,
                "Editar todas" => EditScope.All,
                _ => EditScope.ThisOnly,
            };
        }

        //private async Task HandleCancelFlowAsync(TransactionDTO transaction, Page page)
        //{
        //    string? scope = await page.DisplayActionSheet(
        //        "Como deseja cancelar?",
        //        "Voltar",
        //        null,
        //        "Cancelar a partir desta",
        //        "Cancelar a regra inteira");

        //    if (scope == null || scope == "Voltar") return;

        //    CancelScope cancelScope = scope switch
        //    {
        //        "Cancelar a partir desta" => CancelScope.FromThisOnwards,
        //        "Cancelar a regra inteira" => CancelScope.EntireRule,
        //        _ => CancelScope.FromThisOnwards,
        //    };

        //    var req = new CancelRuleReq
        //    {
        //        TransactionId = transaction.Id,
        //        RecurringRuleId = transaction.RecurringRuleId!.Value,
        //        Scope = cancelScope,
        //    };

        //    var result = await recurringRuleService.CancelAsync(req, IsOn);

        //    if (result.Success)
        //    {
        //        _ = VMBase.ShowMessage("Sucesso", "Recorrência cancelada com sucesso!");
        //        await Shell.Current.GoToAsync("..");
        //    }
        //    else
        //    {
        //        _ = VMBase.ShowMessage("Erro", result.Content?.ToString() ?? "Não foi possível cancelar a recorrência.");
        //    }
        //}

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
        public async Task SaveTransaction()
        {
            // Aguarda o ActionSheet de recorrência terminar antes de prosseguir
            if (_awaitingRecurringAction)
            {
                _ = VMBase.ShowMessage("Aguarde", "Selecione uma opção para a transação recorrente antes de confirmar.");
                return;
            }

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

                // Ocorrência recorrente: delega ao fluxo de edição de recorrência
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
                CategoryId = SelectedCategory?.Id ?? existingTransaction.CategoryId,
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
