using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.DTO;
using Service;
using Service.Account;
using Service.Category;
using Service.Transaction;
using System.Collections.ObjectModel;
using XpemFinancial.Views;

namespace XpemFinancial.VMs.Transaction
{
    public partial class TransactionListVM(
        ITransactionService transactionService,
        IUserSessionService userSessionService,
        IAccountService accountService,
        ICategoryService categoryService) : VMBase
    {
        [ObservableProperty] private string monthYearDisplay = string.Empty;
        [ObservableProperty] private ObservableCollection<TransactionDTO> transactions = [];
        [ObservableProperty] private ObservableCollection<TransactionDTO> filteredTransactions = [];
        [ObservableProperty] private TransactionDTO? selectedTransaction;
        [ObservableProperty] private bool hasMultipleAccounts;
        
        // Search fields
        [ObservableProperty] private string searchDescription = string.Empty;
        [ObservableProperty] private DateTime startDate = DateTime.Now.AddMonths(-1);
        [ObservableProperty] private DateTime endDate = DateTime.Now;
        [ObservableProperty] private ObservableCollection<CategoryDTO> availableCategories = [];
        [ObservableProperty] private CategoryDTO? selectedCategory;
        [ObservableProperty] private bool filterIncomeOnly;
        [ObservableProperty] private bool filterExpenseOnly;
        [ObservableProperty] private bool showFilters = false;

        private DateTime _selectedDate;
        private int? _currentUserId;

        partial void OnSelectedTransactionChanged(TransactionDTO? oldValue, TransactionDTO? newValue)
        {
            if (newValue == null) return;
            GoToTransactionEditCommand.Execute(newValue.Id);
        }

        partial void OnSearchDescriptionChanged(string value) => ApplyFilters();
        partial void OnSelectedCategoryChanged(CategoryDTO? value) => ApplyFilters();
        partial void OnFilterIncomeOnlyChanged(bool value)
        {
            if (value) FilterExpenseOnly = false;
            ApplyFilters();
        }
        partial void OnFilterExpenseOnlyChanged(bool value)
        {
            if (value) FilterIncomeOnly = false;
            ApplyFilters();
        }

        public async Task InitializeAsync()
        {
            var user = await userSessionService.GetCurrentUserAsync();
            if (user != null)
            {
                _currentUserId = user.Id;

                var activeAccounts = await accountService.GetActiveAsync(user.Id);
                HasMultipleAccounts = activeAccounts.Count > 1;

                // Load categories
                var categories = await categoryService.GetAllAsync();
                AvailableCategories = new ObservableCollection<CategoryDTO>(categories.OrderBy(c => c.Name));
            }

            _selectedDate = DateTime.Now;
            await LoadTransactionsAsync(_selectedDate);
        }

        [RelayCommand]
        private async Task LoadPreviousPeriod()
        {
            _selectedDate = _selectedDate.AddMonths(-1);
            await LoadTransactionsAsync(_selectedDate);
        }

        [RelayCommand]
        private async Task LoadNextPeriod()
        {
            _selectedDate = _selectedDate.AddMonths(1);
            await LoadTransactionsAsync(_selectedDate);
        }

        [RelayCommand]
        private async Task SearchByDateRange()
        {
            if (EndDate < StartDate)
            {
                await ShowMessage("Erro", "Data final deve ser maior que data inicial.");
                return;
            }

            await LoadTransactionsByDateRangeAsync(StartDate, EndDate);
        }

        [RelayCommand]
        private void ClearFilters()
        {
            SearchDescription = string.Empty;
            SelectedCategory = null;
            FilterIncomeOnly = false;
            FilterExpenseOnly = false;
            StartDate = DateTime.Now.AddMonths(-1);
            EndDate = DateTime.Now;
        }

        [RelayCommand]
        private void ToggleFilters()
        {
            ShowFilters = !ShowFilters;
        }

        private async Task LoadTransactionsAsync(DateTime date)
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                MonthYearDisplay = date.ToString("MMMM yyyy").ToUpper();

                var loadedTransactions = await transactionService.GetByMonthYear(date);
                Transactions = new ObservableCollection<TransactionDTO>(loadedTransactions.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id));
                
                ApplyFilters();
            }
            catch (Exception ex)
            {
                await ShowMessage("Erro", $"Erro ao carregar transações: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadTransactionsByDateRangeAsync(DateTime start, DateTime end)
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                MonthYearDisplay = $"{start:dd/MM/yyyy} - {end:dd/MM/yyyy}";

                // Load all months in range
                var allTransactions = new List<TransactionDTO>();
                var current = new DateTime(start.Year, start.Month, 1);
                var endMonth = new DateTime(end.Year, end.Month, 1);

                while (current <= endMonth)
                {
                    var monthTransactions = await transactionService.GetByMonthYear(current);
                    allTransactions.AddRange(monthTransactions.Where(t => t.Date >= start && t.Date <= end));
                    current = current.AddMonths(1);
                }

                Transactions = new ObservableCollection<TransactionDTO>(allTransactions.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id));
                
                ApplyFilters();
            }
            catch (Exception ex)
            {
                await ShowMessage("Erro", $"Erro ao carregar transações: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyFilters()
        {
            var filtered = Transactions.AsEnumerable();

            // Filter by description
            if (!string.IsNullOrWhiteSpace(SearchDescription))
            {
                filtered = filtered.Where(t => t.Description.Contains(SearchDescription, StringComparison.OrdinalIgnoreCase));
            }

            // Filter by category
            if (SelectedCategory != null)
            {
                filtered = filtered.Where(t => t.Category?.Id == SelectedCategory.Id);
            }

            // Filter by type
            if (FilterIncomeOnly)
            {
                filtered = filtered.Where(t => t.Amount > 0);
            }
            else if (FilterExpenseOnly)
            {
                filtered = filtered.Where(t => t.Amount < 0);
            }

            FilteredTransactions = new ObservableCollection<TransactionDTO>(filtered);
        }

        [RelayCommand]
        private async Task GoToTransactionEdit(int transactionId)
        {
            SelectedTransaction = null;
            await Shell.Current.GoToAsync($"{nameof(TransactionEditPage)}?id={transactionId}");
        }

        [RelayCommand]
        private async Task GoToNewTransaction()
        {
            await Shell.Current.GoToAsync(nameof(TransactionEditPage));
        }

        [RelayCommand]
        private async Task ExportToCsv()
        {
            if (FilteredTransactions.Count == 0)
            {
                await ShowMessage("Aviso", "Não há transações para exportar.");
                return;
            }

            try
            {
                IsBusy = true;

                // Generate CSV content with UTF-8 BOM for Excel compatibility
                var csv = new System.Text.StringBuilder();
                
                // Add header
                csv.AppendLine("Data,Descrição,Categoria,Conta,Valor,Tipo,Recorrente");

                foreach (var transaction in FilteredTransactions)
                {
                    var tipo = transaction.Amount >= 0 ? "Entrada" : "Saída";
                    var recorrente = transaction.RecurringRuleId.HasValue ? "Sim" : "Não";
                    
                    var line = $"{transaction.Date:dd/MM/yyyy}," +
                               $"\"{EscapeCsvField(transaction.Description)}\"," +
                               $"\"{EscapeCsvField(transaction.Category?.Name ?? "")}\"," +
                               $"\"{EscapeCsvField(transaction.Account?.Name ?? "")}\"," +
                               $"\"{transaction.Amount:F2}\"," +
                               $"{tipo}," +
                               $"{recorrente}";
                    csv.AppendLine(line);
                }

                // Add summary
                var totalEntradas = FilteredTransactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
                var totalSaidas = FilteredTransactions.Where(t => t.Amount < 0).Sum(t => t.Amount);
                var saldo = totalEntradas + totalSaidas;
                
                csv.AppendLine();
                csv.AppendLine("Resumo");
                csv.AppendLine($"Total de Entradas,\"{totalEntradas:F2}\"");
                csv.AppendLine($"Total de Saídas,\"{totalSaidas:F2}\"");
                csv.AppendLine($"Saldo,\"{saldo:F2}\"");
                csv.AppendLine($"Período,{MonthYearDisplay}");

                // Generate filename with timestamp
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"transacoes_{timestamp}.csv";

                // Save file with UTF-8 encoding (with BOM for Excel)
                var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
                var encoding = new System.Text.UTF8Encoding(true); // true = include BOM
                await File.WriteAllTextAsync(filePath, csv.ToString(), encoding);

                // Share the file
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Exportar Transações",
                    File = new ShareFile(filePath)
                });
            }
            catch (Exception ex)
            {
                await ShowMessage("Erro", $"Erro ao exportar: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return field;
            
            // Escape double quotes by doubling them
            return field.Replace("\"", "\"\"");
        }
    }
}
