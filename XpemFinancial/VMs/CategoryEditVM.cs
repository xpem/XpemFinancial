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
    private CategoryDTO? _editingCategory;
    private CategoryType? _originalCategoryType;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSubcategory))]
    private bool isMainCategory = true;

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private CategoryDTO? parentCategory;
    [ObservableProperty] private string? parentCategoryName;
    [ObservableProperty] private bool isEditMode;
    [ObservableProperty] private bool canChangeType = true;
    [ObservableProperty] private bool isInactive;
    [ObservableProperty] private string inheritedTypeDisplayText = string.Empty;
    [ObservableProperty] private int selectedCategoryTypeIndex = -1;

    /// <summary>
    /// Options for the CategoryType picker: Receita (Income), Despesa (Expense), Ambos (Both).
    /// Index maps to CategoryType enum values: 0=Income, 1=Expense, null=Both.
    /// </summary>
    public List<string> CategoryTypeOptions { get; } = ["Receita", "Despesa", "Ambos"];

    /// <summary>
    /// Returns the currently selected CategoryType, or null if nothing is selected.
    /// </summary>
    public CategoryType? SelectedCategoryType =>
        SelectedCategoryTypeIndex >= 0 ? (CategoryType)SelectedCategoryTypeIndex : null;

    /// <summary>
    /// True when the category is user-created (not system) and in edit mode.
    /// Controls visibility of the Inactivate/Reactivate button.
    /// </summary>
    public bool CanToggleActive => IsEditMode && _editingCategory is not null && !_editingCategory.SystemDefault;

    public bool IsSubcategory => !IsMainCategory;

    partial void OnIsMainCategoryChanged(bool value)
    {
        if (!CanChangeType && !value)
        {
            // Reverte: não permite mudar de Principal para Subcategoria
            IsMainCategory = true;
            _ = VMBase.ShowMessage("Aviso", "Não é possível alterar o tipo: existem subcategorias ativas.");
            return;
        }

        if (value)
        {
            // Limpa a categoria pai ao voltar para "Principal"
            ParentCategory = null;
            ParentCategoryName = null;
        }
    }

    partial void OnParentCategoryChanged(CategoryDTO? value)
    {
        InheritedTypeDisplayText = value is not null
            ? GetCategoryTypeDisplayText(value.Type)
            : string.Empty;
    }

    private static string GetCategoryTypeDisplayText(CategoryType? type) => type switch
    {
        CategoryType.Income => "Receita",
        CategoryType.Expense => "Despesa",
        null => "Ambos",
        _ => string.Empty
    };

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

        if (IsMainCategory && SelectedCategoryType is null)
        {
            await VMBase.ShowMessage("Aviso", "Selecione um tipo para a categoria");
            return;
        }

        if (!IsMainCategory && ParentCategory is null)
        {
            await VMBase.ShowMessage("Aviso", "Selecione a categoria pai.");
            return;
        }

        // Validação de nome duplicado entre categorias e subcategorias ativas
        var trimmedName = Name.Trim();
        var excludeId = _editingCategory?.CategoryId;
        bool duplicateExists = await categoryService.ExistsByNameAsync(trimmedName, excludeId);

        if (duplicateExists)
        {
            await VMBase.ShowMessage("Aviso", "Já existe uma categoria ou subcategoria com este nome.");
            return;
        }

        IsBusy = true;
        try
        {
            if (IsEditMode)
            {
                _editingCategory!.Name = Name.Trim();
                _editingCategory.IsMainCategory = IsMainCategory;
                _editingCategory.ParentExternalId = IsMainCategory ? null : ParentCategory!.ExternalId;
                _editingCategory.UpdatedAt = DateTime.UtcNow;

                // If MainCategory type changed, cascade to subcategories
                if (IsMainCategory && SelectedCategoryType is not null
                    && _originalCategoryType != SelectedCategoryType.Value)
                {
                    await categoryService.UpdateMainCategoryTypeAsync(_editingCategory, SelectedCategoryType.Value);
                }
                else if (IsMainCategory && SelectedCategoryType is not null)
                {
                    _editingCategory.Type = SelectedCategoryType.Value;
                }

                await categoryService.UpdateLocalAsync(_editingCategory);
                await categoryService.PushAsync();

                await VMBase.ShowMessage("Sucesso", "Categoria atualizada com sucesso.");
            }
            else
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
                    Type = IsMainCategory ? SelectedCategoryType!.Value : ParentCategory!.Type,
                };

                await categoryService.AddLocalAsync(category);

                await VMBase.ShowMessage("Sucesso", "Categoria criada com sucesso.");
            }

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

    [RelayCommand]
    private async Task ToggleActive()
    {
        if (_editingCategory is null) return;

        if (!_editingCategory.Inactive)
        {
            // Inactivating — confirm first
            var page = Application.Current!.Windows[0].Page!;
            bool confirmed = await page.DisplayAlertAsync(
                "Confirmação",
                "Deseja inativar esta categoria?",
                "Sim",
                "Cancelar");

            if (!confirmed) return;
        }

        IsBusy = true;
        try
        {
            bool newInactiveState = !_editingCategory.Inactive;
            _editingCategory.Inactive = newInactiveState;
            _editingCategory.UpdatedAt = DateTime.UtcNow;
            await categoryService.UpdateLocalAsync(_editingCategory);

            // Cascade logic
            if (newInactiveState && _editingCategory.IsMainCategory && _editingCategory.ExternalId is not null)
            {
                // Inactivating main → cascade to active subcategories
                var all = await categoryService.GetAllAsync();
                var activeSubcategories = all
                    .Where(c => !c.IsMainCategory
                        && c.ParentExternalId == _editingCategory.ExternalId
                        && !c.Inactive)
                    .ToList();

                foreach (var sub in activeSubcategories)
                {
                    sub.Inactive = true;
                    sub.UpdatedAt = DateTime.UtcNow;
                    await categoryService.UpdateLocalAsync(sub);
                }
            }
            else if (!newInactiveState && !_editingCategory.IsMainCategory && _editingCategory.ParentExternalId != null)
            {
                // Reactivating subcategory → cascade up to inactive parent
                var all = await categoryService.GetAllAsync();
                var parent = all.FirstOrDefault(c =>
                    c.IsMainCategory && c.ExternalId == _editingCategory.ParentExternalId);

                if (parent != null && parent.Inactive)
                {
                    parent.Inactive = false;
                    parent.UpdatedAt = DateTime.UtcNow;
                    await categoryService.UpdateLocalAsync(parent);
                }
            }

            await categoryService.PushAsync();

            IsInactive = _editingCategory.Inactive;

            var message = newInactiveState ? "Categoria inativada com sucesso." : "Categoria reativada com sucesso.";
            await VMBase.ShowMessage("Sucesso", message);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await VMBase.ShowMessage("Erro", $"Não foi possível alterar o status: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("categoryId", out var categoryIdValue))
        {
            Guid categoryId;
            if (categoryIdValue is Guid guid)
                categoryId = guid;
            else if (categoryIdValue is string str && Guid.TryParse(str, out var parsed))
                categoryId = parsed;
            else
                return;

            IsEditMode = true;
            _ = LoadCategoryForEditAsync(categoryId);
        }
    }

    private async Task LoadCategoryForEditAsync(Guid categoryId)
    {
        IsBusy = true;
        try
        {
            var all = await categoryService.GetAllAsync();
            _mainCategories = all.Where(c => c.IsMainCategory).ToList();

            _editingCategory = all.FirstOrDefault(c => c.CategoryId == categoryId);
            if (_editingCategory is null) return;

            Name = _editingCategory.Name;
            IsInactive = _editingCategory.Inactive;
            OnPropertyChanged(nameof(CanToggleActive));

            // Pre-fill the CategoryType selector for MainCategory edit
            if (_editingCategory.IsMainCategory)
            {
                SelectedCategoryTypeIndex = (int)_editingCategory.Type;
                _originalCategoryType = _editingCategory.Type;
            }

            // Block type change if main category has active subcategories (Requirement 5.6)
            if (_editingCategory.IsMainCategory)
            {
                var hasActiveSubcategories = all.Any(c =>
                    !c.IsMainCategory &&
                    !c.Inactive &&
                    c.ParentExternalId == _editingCategory.ExternalId);
                CanChangeType = !hasActiveSubcategories;
            }

            IsMainCategory = _editingCategory.IsMainCategory;

            if (!_editingCategory.IsMainCategory && _editingCategory.ParentExternalId.HasValue)
            {
                var parent = _mainCategories.FirstOrDefault(c => c.ExternalId == _editingCategory.ParentExternalId);
                if (parent is not null)
                {
                    ParentCategory = parent;
                    ParentCategoryName = parent.Name;
                }
            }
        }
        catch (Exception ex)
        {
            await VMBase.ShowMessage("Erro", $"Não foi possível carregar a categoria: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
