using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Model;

namespace XpemFinancial.VMs
{
    public class MainPageVM
    {
        public ObservableCollection<Transaction> Transactions { get; set; }

        public MainPageVM()
        {
            // Criando a lista de Mock
            Transactions = new ObservableCollection<Transaction>
        {
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
        };
        }
    }
}
