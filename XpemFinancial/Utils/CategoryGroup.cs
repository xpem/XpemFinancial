using Model.DTO;

namespace XpemFinancial.Utils;

/// <summary>
/// Groups a parent category with its subcategories for accordion display.
/// </summary>
public class CategoryGroup
{
    public CategoryDTO Parent { get; set; } = null!;
    public List<CategoryDTO> Children { get; set; } = [];
}
