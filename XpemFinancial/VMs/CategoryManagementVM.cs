using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.DTO;
using Service;
using Service.Category;
using XpemFinancial.Views;

namespace XpemFinancial.VMs;

public partial class CategoryManagementVM(
    ICategoryService categoryService,
    IUserSessionService userSessionService) : VMBase
{
    [ObservableProperty]
    private List<CategoryDisplayItem> categories = [];

    [ObservableProperty]
    private bool hasNoCategories;

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var grouped = await categoryService.GetAllGroupedAsync();

            Categories = grouped
                .Select(c => new CategoryDisplayItem { Category = c })
                .ToList();

            HasNoCategories = Categories.Count == 0;
        }
        catch
        {
            await ShowMessage("Erro", "Erro ao carregar categorias");
            Categories = [];
            HasNoCategories = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task EditCategory(CategoryDTO category)
    {
        // System categories are read-only — don't navigate to edit
        if (category.SystemDefault) return;

        await Shell.Current.GoToAsync($"{nameof(CategoryEditPage)}?categoryId={category.CategoryId}");
    }

    [RelayCommand]
    private async Task InactivateCategory(CategoryDTO category)
    {
        var page = Application.Current!.Windows[0].Page!;
        bool confirmed = await page.DisplayAlertAsync(
            "Confirmação",
            "Deseja inativar esta categoria?",
            "Sim",
            "Cancelar");

        if (!confirmed) return;

        IsBusy = true;
        try
        {
            category.Inactive = true;
            category.UpdatedAt = DateTime.UtcNow;
            await categoryService.UpdateLocalAsync(category);

            // Cascade: if main category, inactivate all active subcategories
            if (category.IsMainCategory && category.ExternalId is not null)
            {
                var all = await categoryService.GetAllAsync();
                var activeSubcategories = all
                    .Where(c => !c.IsMainCategory
                        && c.ParentExternalId == category.ExternalId
                        && !c.Inactive)
                    .ToList();

                foreach (var sub in activeSubcategories)
                {
                    sub.Inactive = true;
                    sub.UpdatedAt = DateTime.UtcNow;
                    await categoryService.UpdateLocalAsync(sub);
                }
            }

            await categoryService.PushAsync();
            await InitializeAsync();
        }
        catch
        {
            await ShowMessage("Erro", "Erro ao inativar categoria");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ReactivateCategory(CategoryDTO category)
    {
        IsBusy = true;
        try
        {
            category.Inactive = false;
            category.UpdatedAt = DateTime.UtcNow;
            await categoryService.UpdateLocalAsync(category);

            // Cascade up: if subcategory's parent is inactive, also reactivate the parent
            if (!category.IsMainCategory && category.ParentExternalId != null)
            {
                var all = await categoryService.GetAllAsync();
                var parent = all.FirstOrDefault(c =>
                    c.IsMainCategory && c.ExternalId == category.ParentExternalId);

                if (parent != null && parent.Inactive)
                {
                    parent.Inactive = false;
                    parent.UpdatedAt = DateTime.UtcNow;
                    await categoryService.UpdateLocalAsync(parent);
                }
            }

            await categoryService.PushAsync();
            await InitializeAsync();
        }
        catch
        {
            await ShowMessage("Erro", "Erro ao reativar categoria");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
