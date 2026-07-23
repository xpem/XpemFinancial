namespace XpemFinancial.Utils;

/// <summary>
/// Selects between an Expander template (for groups with children)
/// and a simple tappable row (for standalone categories without children).
/// </summary>
public class CategoryGroupTemplateSelector : DataTemplateSelector
{
    public DataTemplate ExpandableTemplate { get; set; } = null!;
    public DataTemplate SimpleTemplate { get; set; } = null!;

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        if (item is CategoryGroup group && group.Children.Count > 0)
            return ExpandableTemplate;

        return SimpleTemplate;
    }
}
