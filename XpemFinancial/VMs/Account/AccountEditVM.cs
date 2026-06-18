using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.DTO;
using Service;
using Service.Account;
using Service.Transaction;

namespace XpemFinancial.VMs;

public partial class AccountEditVM(
    IAccountService accountService,
    IUserSessionService userSessionService,
    ITransactionService transactionService) : VMBase, IQueryAttributable
{
    private int? _accountId;
    private AccountDTO? _existingAccount;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private int selectedTypeIndex;

    [ObservableProperty]
    private bool includeInGeneralBalance = true;

    [ObservableProperty]
    private bool isEditMode;

    [ObservableProperty]
    private string pageTitle = "Nova Conta";

    // ── Ajuste de saldo ──
    [ObservableProperty]
    private string currentBalance = string.Empty;

    [ObservableProperty]
    private bool isActiveAccount;

    private decimal _originalBalance;

    public List<string> AccountTypeOptions { get; } =
    [
        "Conta Corrente",
        "Poupança/Investimentos",
        "Benefícios"
    ];

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("accountId", out var idObj) && int.TryParse(idObj?.ToString(), out int id))
        {
            _accountId = id;
            IsEditMode = true;
            PageTitle = "Editar Conta";
        }
    }

    public async Task InitializeAsync()
    {
        if (_accountId is not null)
        {
            _existingAccount = await accountService.GetByIdAsync(_accountId.Value);
            if (_existingAccount is not null)
            {
                Name = _existingAccount.Name;
                SelectedTypeIndex = (int)_existingAccount.Type;
                IncludeInGeneralBalance = _existingAccount.IncludeInGeneralBalance;
                IsActiveAccount = _existingAccount.IsActive;

                _originalBalance = await transactionService.GetBalanceAsync(_existingAccount.Id) ?? 0;
                CurrentBalance = _originalBalance.ToString("C");
            }
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        string trimmedName = Name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            await ShowMessage("Aviso", "Informe um nome para a conta.");
            return;
        }

        if (trimmedName.Length > 100)
        {
            await ShowMessage("Aviso", "O nome da conta deve ter no máximo 100 caracteres.");
            return;
        }

        IsBusy = true;
        try
        {
            var user = await userSessionService.GetCurrentUserAsync();
            if (user is null) return;

            var type = (AccountType)SelectedTypeIndex;

            if (IsEditMode && _existingAccount is not null)
            {
                _existingAccount.Name = trimmedName;
                _existingAccount.Type = type;
                _existingAccount.IncludeInGeneralBalance = IncludeInGeneralBalance;
                await accountService.UpdateAsync(_existingAccount);

                // Ajustar saldo se o valor foi alterado
                if (_existingAccount.IsActive && CurrentBalance != _originalBalance.ToString("C"))
                {
                    decimal newBalance = decimal.Parse(CurrentBalance, System.Globalization.NumberStyles.Currency);
                    await accountService.AdjustAccountBalanceAsync(_existingAccount.Id, newBalance, _originalBalance, IsOn);
                }

                await ShowMessage("Sucesso", "Conta atualizada com sucesso.");
            }
            else
            {
                decimal initialBalance = 0;
                if (!string.IsNullOrWhiteSpace(CurrentBalance)
                    && decimal.TryParse(CurrentBalance, System.Globalization.NumberStyles.Currency, null, out decimal parsed))
                {
                    initialBalance = parsed;
                }

                await accountService.CreateAsync(user.Id, trimmedName, type, IncludeInGeneralBalance, initialBalance);
                await ShowMessage("Sucesso", "Conta criada com sucesso.");
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (InvalidOperationException ex)
        {
            await ShowMessage("Erro", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Deactivate()
    {
        if (_existingAccount is null) return;

        var page = Application.Current!.Windows[0].Page!;
        bool confirm = await page.DisplayAlert(
            "Desativar Conta",
            $"Tem certeza que deseja desativar a conta \"{_existingAccount.Name}\"? Transações existentes serão mantidas para consulta histórica.",
            "Desativar",
            "Cancelar");

        if (!confirm) return;

        IsBusy = true;
        try
        {
            await accountService.DeactivateAsync(_existingAccount.Id);
            await ShowMessage("Sucesso", "Conta desativada.");
            await Shell.Current.GoToAsync("..");
        }
        catch (InvalidOperationException ex)
        {
            await ShowMessage("Erro", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
