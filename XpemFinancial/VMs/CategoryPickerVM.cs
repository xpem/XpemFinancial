using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using XpemFinancial.Views;
using static XpemFinancial.VMs.TransactionEditVM;

namespace XpemFinancial.VMs
{
    public partial class CategoryPickerVM : ObservableObject
    {
        // Lista completa (backup para o filtro)
        private List<SelectableCategory> allCategories = new();

        // Lista que o CollectionView observa
        [ObservableProperty]
        private ObservableCollection<SelectableCategory> categories;


        [ObservableProperty]
        private SelectableCategory selectedItem;

        [ObservableProperty]
        private string searchText;

        public CategoryPickerVM()
        {
            LoadAndFlattenCategories(); // Ou receba de um serviço
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("SelectedCategory") &&
                query["SelectedCategory"] is SelectableCategory selected)
            {
                selectedItem = selected;

                // Limpa o dicionário para evitar re-processamento indesejado
                query.Clear();
            }
        }

        private void LoadAndFlattenCategories()
        {
            var tempItems = new List<SelectableCategory>();

            foreach (var cat in TransactionCategories.LoadTransactionCategories())
            {
                tempItems.Add(new SelectableCategory { Name = cat.Category, IsCategory = true });

                if (cat.Subcategories != null)
                {
                    foreach (var sub in cat.Subcategories)
                    {
                        tempItems.Add(new SelectableCategory
                        {
                            Name = sub,
                            ParentCategory = cat.Category,
                            IsCategory = false
                        });
                    }
                }
            }

            allCategories = tempItems;
            Categories = new ObservableCollection<SelectableCategory>(allCategories);
        }

        // Executado toda vez que o SearchText mudar (via partial method do Toolkit)
        partial void OnSearchTextChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                // Importante usar a Propriedade (Maiúscula) para disparar o OnPropertyChanged
                Categories = new ObservableCollection<SelectableCategory>(allCategories);
                return;
            }

            var search = value.ToLower();
            var filtered = allCategories.Where(x => x.Name.ToLower().Contains(search));
            Categories = new ObservableCollection<SelectableCategory>(filtered);
        }

        [RelayCommand]
        private async Task SelectItem(SelectableCategory item)
        {
            if (item == null) return;

            // Criamos o dicionário de parâmetros
            var navigationParameter = new Dictionary<string, object>
            {        { "SelectedCategory", item }    };

            // Navegamos de volta passando o objeto
            await Shell.Current.GoToAsync(nameof(TransactionEdit), true, navigationParameter);
        }
    }
}
