using Model.DTO;

namespace XpemFinancial.VMs.Category
{
    public class CategoryDisplayItem
    {
        public CategoryDTO Category { get; set; }

        public bool IsMainCategory => Category.IsMainCategory;

        public bool IsInactive => Category.Inactive;

        public bool IsSystemDefault => Category.SystemDefault;

        public bool CanEdit => !IsSystemDefault;

        public bool CanInactivate => !IsSystemDefault && !IsInactive;

        public bool CanReactivate => !IsSystemDefault && IsInactive;
    }
}
