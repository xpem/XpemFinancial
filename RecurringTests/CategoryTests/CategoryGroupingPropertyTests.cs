// Feature: category-management, Property 1: Hierarchical sorting preserves grouping and ordering invariants
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Model.DTO;
using Service.Category;
using Xunit;

namespace RecurringTests.CategoryTests;

/// <summary>
/// Property 1: Hierarchical sorting preserves grouping and ordering invariants
/// **Validates: Requirements 2.1, 2.5**
/// 
/// For any list of categories with varying names, hierarchy relationships, and inactive flags,
/// the grouped output SHALL place each main category immediately before its subcategories,
/// with active items sorted alphabetically before inactive items within each group,
/// and main category groups sorted alphabetically by the main category's name.
/// </summary>
[Trait("Feature", "category-management")]
[Trait("Property", "1")]
public class CategoryGroupingPropertyTests
{
    /// <summary>
    /// Generates a list of CategoryDTO with a mix of main categories and subcategories,
    /// with varying inactive flags.
    /// </summary>
    private static Gen<List<CategoryDTO>> CategoryListGen()
    {
        return from mainCount in Gen.Choose(1, 5)
               from mainNames in Gen.ListOf(
                   Gen.Elements("Alimentação", "Transporte", "Saúde", "Lazer", "Educação", "Moradia", "Vestuário", "Outros"),
                   mainCount)
               from mainInactiveFlags in Gen.ListOf(Gen.Elements(true, false), mainCount)
               let mains = mainNames.Select((name, i) => new CategoryDTO
               {
                   Name = name + "_" + i,
                   IsMainCategory = true,
                   Inactive = mainInactiveFlags[i],
                   ExternalId = i + 1,
                   ParentExternalId = null,
                   CategoryId = Guid.NewGuid(),
                   UserId = 1,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow,
               }).ToList()
               from subCount in Gen.Choose(0, 8)
               from subNames in Gen.ListOf(
                   Gen.Elements("Sub_A", "Sub_B", "Sub_C", "Sub_D", "Sub_E", "Sub_F"),
                   subCount)
               from subInactiveFlags in Gen.ListOf(Gen.Elements(true, false), subCount)
               from parentIndices in Gen.ListOf(Gen.Choose(0, mains.Count - 1), subCount)
               from subSuffixes in Gen.ListOf(Gen.Choose(1, 100), subCount)
               let subs = subNames.Select((name, i) => new CategoryDTO
               {
                   Name = name + "_" + subSuffixes[i],
                   IsMainCategory = false,
                   Inactive = subInactiveFlags[i],
                   ExternalId = 100 + i,
                   ParentExternalId = mains[parentIndices[i]].ExternalId,
                   CategoryId = Guid.NewGuid(),
                   UserId = 1,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow,
               }).ToList()
               from orphanCount in Gen.Choose(0, 3)
               from orphanNames in Gen.ListOf(
                   Gen.Elements("Orphan_X", "Orphan_Y", "Orphan_Z"),
                   orphanCount)
               from orphanInactiveFlags in Gen.ListOf(Gen.Elements(true, false), orphanCount)
               from orphanSuffixes in Gen.ListOf(Gen.Choose(1, 50), orphanCount)
               let orphans = orphanNames.Select((name, i) => new CategoryDTO
               {
                   Name = name + "_" + orphanSuffixes[i],
                   IsMainCategory = false,
                   Inactive = orphanInactiveFlags[i],
                   ExternalId = 200 + i,
                   ParentExternalId = 999, // non-existent parent
                   CategoryId = Guid.NewGuid(),
                   UserId = 1,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow,
               }).ToList()
               select mains.Concat(subs).Concat(orphans).ToList();
    }

    /// <summary>
    /// Each main category in the result immediately precedes its subcategories
    /// (no interleaving from other groups).
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MainCategory_ImmediatelyPrecedesItsSubcategories()
    {
        return Prop.ForAll(
            CategoryListGen().ToArbitrary(),
            categories =>
            {
                var result = CategoryService.GroupCategories(categories);

                for (int i = 0; i < result.Count; i++)
                {
                    var item = result[i];
                    if (!item.IsMainCategory) continue;

                    // Find the block after this main (up to the next main or end)
                    int nextMainIdx = result.Count;
                    for (int j = i + 1; j < result.Count; j++)
                    {
                        if (result[j].IsMainCategory)
                        {
                            nextMainIdx = j;
                            break;
                        }
                    }

                    var blockAfterMain = result.GetRange(i + 1, nextMainIdx - i - 1);

                    // All expected children of this main should be in this block
                    var expectedChildren = result
                        .Where(c => !c.IsMainCategory && c.ParentExternalId == item.ExternalId)
                        .ToList();

                    foreach (var child in expectedChildren)
                    {
                        if (!blockAfterMain.Contains(child))
                            return false;
                    }
                }

                return true;
            });
    }

    /// <summary>
    /// Within each group, active items appear before inactive items.
    /// Active mains before inactive mains, and active subs before inactive subs within each parent.
    /// Orphan subcategories (at the end) also follow active-before-inactive ordering.
    /// **Validates: Requirements 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ActiveItems_AppearBeforeInactiveItems_WithinEachGroup()
    {
        return Prop.ForAll(
            CategoryListGen().ToArbitrary(),
            categories =>
            {
                var result = CategoryService.GroupCategories(categories);

                // Check main categories: active mains appear before inactive mains
                var mains = result.Where(c => c.IsMainCategory).ToList();
                bool seenInactiveMain = false;
                foreach (var main in mains)
                {
                    if (main.Inactive)
                        seenInactiveMain = true;
                    else if (seenInactiveMain)
                        return false; // active main after inactive main
                }

                // Check subcategories within each main's children (matched by ParentExternalId)
                foreach (var main in mains)
                {
                    var children = result
                        .Where(c => !c.IsMainCategory && c.ParentExternalId == main.ExternalId)
                        .ToList();

                    bool seenInactiveSub = false;
                    foreach (var child in children)
                    {
                        if (child.Inactive)
                            seenInactiveSub = true;
                        else if (seenInactiveSub)
                            return false; // active sub after inactive sub within same group
                    }
                }

                // Check orphans (subcategories with no matching main parent)
                var mainExternalIds = new HashSet<int?>(mains
                    .Where(m => m.ExternalId != null)
                    .Select(m => m.ExternalId));

                var orphans = result
                    .Where(c => !c.IsMainCategory
                        && (c.ParentExternalId == null || !mainExternalIds.Contains(c.ParentExternalId)))
                    .ToList();

                bool seenInactiveOrphan = false;
                foreach (var orphan in orphans)
                {
                    if (orphan.Inactive)
                        seenInactiveOrphan = true;
                    else if (seenInactiveOrphan)
                        return false; // active orphan after inactive orphan
                }

                return true;
            });
    }

    /// <summary>
    /// Main category groups are sorted alphabetically by the main category's name
    /// (within active group and within inactive group respectively).
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MainCategoryGroups_SortedAlphabeticallyByName()
    {
        return Prop.ForAll(
            CategoryListGen().ToArbitrary(),
            categories =>
            {
                var result = CategoryService.GroupCategories(categories);

                var mains = result.Where(c => c.IsMainCategory).ToList();

                // Active mains should be alphabetical among themselves
                var activeMains = mains.Where(m => !m.Inactive).ToList();
                for (int i = 1; i < activeMains.Count; i++)
                {
                    if (string.Compare(activeMains[i - 1].Name, activeMains[i].Name, StringComparison.OrdinalIgnoreCase) > 0)
                        return false;
                }

                // Inactive mains should be alphabetical among themselves
                var inactiveMains = mains.Where(m => m.Inactive).ToList();
                for (int i = 1; i < inactiveMains.Count; i++)
                {
                    if (string.Compare(inactiveMains[i - 1].Name, inactiveMains[i].Name, StringComparison.OrdinalIgnoreCase) > 0)
                        return false;
                }

                return true;
            });
    }

    /// <summary>
    /// Subcategories within each main's group are sorted alphabetically by name
    /// (within active subs and within inactive subs respectively).
    /// Orphan subcategories also sorted alphabetically within their active/inactive groups.
    /// **Validates: Requirements 2.1, 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Subcategories_SortedAlphabeticallyWithinGroup()
    {
        return Prop.ForAll(
            CategoryListGen().ToArbitrary(),
            categories =>
            {
                var result = CategoryService.GroupCategories(categories);

                var mains = result.Where(c => c.IsMainCategory).ToList();

                // Check children of each main (by ParentExternalId) appear in alphabetical order
                // within active and inactive subgroups. We check the relative order in result.
                foreach (var main in mains)
                {
                    var children = result
                        .Where(c => !c.IsMainCategory && c.ParentExternalId == main.ExternalId)
                        .ToList();

                    // Active children should be alphabetical
                    var activeChildren = children.Where(c => !c.Inactive).ToList();
                    for (int j = 1; j < activeChildren.Count; j++)
                    {
                        if (string.Compare(activeChildren[j - 1].Name, activeChildren[j].Name, StringComparison.OrdinalIgnoreCase) > 0)
                            return false;
                    }

                    // Inactive children should be alphabetical
                    var inactiveChildren = children.Where(c => c.Inactive).ToList();
                    for (int j = 1; j < inactiveChildren.Count; j++)
                    {
                        if (string.Compare(inactiveChildren[j - 1].Name, inactiveChildren[j].Name, StringComparison.OrdinalIgnoreCase) > 0)
                            return false;
                    }
                }

                // Also check orphans alphabetical ordering
                var mainExternalIds = new HashSet<int?>(mains
                    .Where(m => m.ExternalId != null)
                    .Select(m => m.ExternalId));

                var orphans = result
                    .Where(c => !c.IsMainCategory
                        && (c.ParentExternalId == null || !mainExternalIds.Contains(c.ParentExternalId)))
                    .ToList();

                var activeOrphans = orphans.Where(o => !o.Inactive).ToList();
                for (int j = 1; j < activeOrphans.Count; j++)
                {
                    if (string.Compare(activeOrphans[j - 1].Name, activeOrphans[j].Name, StringComparison.OrdinalIgnoreCase) > 0)
                        return false;
                }

                var inactiveOrphans = orphans.Where(o => o.Inactive).ToList();
                for (int j = 1; j < inactiveOrphans.Count; j++)
                {
                    if (string.Compare(inactiveOrphans[j - 1].Name, inactiveOrphans[j].Name, StringComparison.OrdinalIgnoreCase) > 0)
                        return false;
                }

                return true;
            });
    }

    /// <summary>
    /// The output contains exactly the same elements as the input (no categories lost or duplicated).
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GroupedOutput_PreservesAllElements()
    {
        return Prop.ForAll(
            CategoryListGen().ToArbitrary(),
            categories =>
            {
                var result = CategoryService.GroupCategories(categories);

                // Same count
                if (result.Count != categories.Count) return false;

                // Same elements (by reference)
                var inputSet = new HashSet<CategoryDTO>(categories);
                var outputSet = new HashSet<CategoryDTO>(result);

                return inputSet.SetEquals(outputSet);
            });
    }
}
