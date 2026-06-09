using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.DTO;
using Service;
using Service.Category;

namespace XpemFinancial.VMs;

public partial class CategoryEditVM(
    ICategoryService categoryService,
    IUserSessionService userSessionService) : VMBase, IQueryAttributable
{
    private List<CategoryDTO> _mainCategories = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSubcategory))]
    private bool isMainCategory = true;

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private CategoryDTO? parentCategory;
    [ObservableProperty] private string? parentCategoryName;

    public bool IsSubcategory => !IsMainCategory;

    partial void OnIsMainCategoryChanged(bool value)
    {
        if (value)
        {
            // Limpa a categoria pai ao voltar para "Principal"
            ParentCategory = null;
            ParentCategoryName = null;
        }
    }

    public async Task InitializeAsync()
    {
        var all = await categoryService.GetAllAsync();
        _mainCategories = all.Where(c => c.IsMainCategory).ToList();
    }

    [RelayCommand]
    private async Task SelectParent()
    {
        // Abre um ActionSheet com as categorias principais disponíveis
        var page = Application.Current!.Windows[0].Page!;
        var options = _mainCategories.Select(c => c.Name).ToArray();

        string? chosen = await page.DisplayActionSheetAsync("Selecionar categoria pai", "Cancelar", null, options);

        if (chosen == null || chosen == "Cancelar") return;

        var selected = _mainCategories.FirstOrDefault(c => c.Name == chosen);
        if (selected is null) return;

        ParentCategory = selected;
        ParentCategoryName = selected.Name;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await VMBase.ShowMessage("Aviso", "Informe um nome para a categoria.");
            return;
        }

        if (!IsMainCategory && ParentCategory is null)
        {
            await VMBase.ShowMessage("Aviso", "Selecione a categoria pai.");
            return;
        }

        IsBusy = true;
        try
        {
            var user = await userSessionService.GetCurrentUserAsync();

            var category = new CategoryDTO
            {
                Name = Name.Trim(),
                IsMainCategory = IsMainCategory,
                ParentExternalId = IsMainCategory ? null : ParentCategory!.ExternalId,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            await categoryService.AddLocalAsync(category);

            await VMBase.ShowMessage("Sucesso", "Categoria criada com sucesso.");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await VMBase.ShowMessage("Erro", $"Não foi possível salvar a categoria: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) { }
}
