using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.DTO;
using System;
using System.Collections.Generic;
using System.Text;

namespace XpemFinancial.VMs
{
    public partial class AccountVM : VMBase
    {
        [ObservableProperty]
        private string currentBalance;

        [ObservableProperty]
        private bool isEditingBalance = false;

        [ObservableProperty]
        private bool isNotEditingBalance = true;

        private AccountDTO Account { get; set; }

        partial void OnIsEditingBalanceChanged(bool value)
        {
            IsNotEditingBalance = !value;
        }

        [RelayCommand]
        private void SaveBalance()
        {
            // Aqui você pode adicionar a lógica para salvar o novo saldo, como chamar um serviço ou atualizar o banco de dados
            IsEditingBalance = false;
        }

        [RelayCommand]
        private void CancelEditBalance()
        {
            // Aqui você pode adicionar a lógica para cancelar a edição, como restaurar o valor original do saldo
            IsEditingBalance = false;
        }

        [RelayCommand]
        private void EditBalance()
        {
            IsEditingBalance = true;
        }

        public AccountVM()
        {
            CurrentBalance = "0";
        }
    }
}
