using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XpemFinancial.VMs
{
    public partial class MainPageVM : ObservableObject
    {
        public ObservableCollection<Transaction> Transactions { get; set; }

        [ObservableProperty]

        private Totals totals;

        public MainPageVM()
        {
            totals = new Totals
            {
                IncludePreviousBalance = true,
                PreviousBalance = 5000.00m,
                Income = 2000.00m,
                Expense = 1000.00m
            };

            totals.Total = totals.PreviousBalance + totals.Income - totals.Expense;

            // Criando a lista de Mock
            Transactions =
            [
                new Transaction
                {
                    Description = "Assinatura Netflix",
                    Date = DateTime.Now.AddDays(-2),
                    Category = "Lazer",
                    Amount = -55.90m
                },
                new Transaction
                {
                    Description = "Salário Mensal",
                    Date = DateTime.Now.AddDays(-1),
                    Category = "Trabalho",
                    Amount = 5000.00m
                },
                new Transaction
                {
                    Description = "Supermercado",
                    Date = DateTime.Now,
                    Category = "Alimentação",
                    Amount = -350.25m
                },
                new Transaction
                {
                    Description = "Venda de Monitor",
                    Date = DateTime.Now,
                    Category = "Extra",
                    Amount = 850.00m
                },
                new Transaction
                {
                    Description = "Posto de Gasolina",
                    Date = DateTime.Now.AddDays(1),
                    Category = "Transporte",
                    Amount = -200.00m
                }
            ];
        }

        [RelayCommand]
        private async Task ToggleIncludePreviousBalance()
        {
            // 1. Toggle the state
            totals.IncludePreviousBalance = !totals.IncludePreviousBalance;

            // 2. Perform the conditional calculation
            if (totals.IncludePreviousBalance)
            {
                totals.Total = totals.PreviousBalance + totals.Income - totals.Expense;
            }
            else
            {
                totals.Total = totals.Income - totals.Expense;
            }

            // 3. Force the UI to refresh the Totals object
            // Note: It's better if the Totals class inherits from ObservableObject,
            // but this manual trigger works if Totals is a POCO.
            OnPropertyChanged(nameof(Totals));
        }

        [RelayCommand]
        private async Task GoToTransactionEdit() => await Shell.Current.GoToAsync($"{nameof(Views.TransactionEdit)}");

    }
}
