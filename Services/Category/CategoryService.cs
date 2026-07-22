using ApiRepo;
using Model.DTO;
using Model.Resp.Api;
using Repo;
using System.Text.Json;

namespace Service.Category
{
    public interface ICategoryService
    {
        Task<List<CategoryDTO>> GetAllAsync();
        Task<List<CategoryDTO>> GetAllGroupedAsync();
        Task<bool> ExistsByNameAsync(string name, Guid? excludeCategoryId = null);
        Task UpsertAsync(CategoryDTO category);
        Task AddLocalAsync(CategoryDTO category);
        Task UpdateLocalAsync(CategoryDTO category);
        Task UpdateMainCategoryTypeAsync(CategoryDTO mainCategory, CategoryType newType);
        Task<DateTime> GetLastUpdatedAtAsync();
        Task PullAsync(int uid, DateTime lastUpdatedAt);
        Task PushAsync();
    }

    public class CategoryService(ICategoryRepo categoryRepo, ICategoryApiRepo categoryApiRepo, ISyncCursorRepo syncCursorRepo) : ICategoryService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        /// <summary>
        /// Defensively maps an integer from the API response to a <see cref="CategoryType"/>.
        /// Returns <see cref="CategoryType.Both"/> for null or out-of-range values.
        /// </summary>
        public static CategoryType? SafeParseCategoryType(int? value)
        {
            return value switch
            {
                0 => CategoryType.Income,
                1 => CategoryType.Expense,
                _ => null
            };
        }

        public async Task<DateTime> GetLastUpdatedAtAsync()
            => await syncCursorRepo.GetAsync(SyncCursorKeys.Category);

        public async Task PullAsync(int uid, DateTime lastUpdatedAt)
        {
            const int pageSize = 100;
            int page = 1;
            DateTime maxServerTs = DateTime.MinValue;

            while (true)
            {
                ApiResp apiResp = await categoryApiRepo.GetByLastUpdateAsync(lastUpdatedAt, page);

                if (!apiResp.Success || apiResp.Content is null)
                    throw new Exception($"Erro ao buscar categorias da API: {apiResp.Content ?? "sem resposta"}");

                List<TransactionCategoryApiRes>? apiRes = JsonSerializer.Deserialize<List<TransactionCategoryApiRes>>(apiResp.Content, _jsonOptions);

                if (apiRes is null || apiRes.Count == 0) break;

                foreach (var category in apiRes)
                {
                    if (category == null) continue;

                    CategoryDTO? local = null;

                    // 1. Match by CategoryId if present
                    if (category.CategoryId is not null && category.CategoryId != Guid.Empty)
                        local = await categoryRepo.GetByCategoryIdAsync(category.CategoryId.Value);

                    // 2. Fallback to ExternalId
                    if (local is null && category.Id > 0)
                        local = await categoryRepo.GetByExternalIdAsync(category.Id);

                    if (local is not null)
                    {
                        // Last-writer-wins: update only if pulled UpdatedAt > local UpdatedAt
                        if (category.UpdatedAt > local.UpdatedAt)
                        {
                            local.Name = category.Name;
                            local.Inactive = category.Inactive;
                            local.SystemDefault = category.SystemDefault;
                            local.IsMainCategory = category.IsMainTransactionCategory;
                            local.ParentExternalId = category.ParentTransactionCategoryId;
                            local.ExternalId = category.Id;
                            local.Type = SafeParseCategoryType(category.Type);
                            local.UpdatedAt = category.UpdatedAt;
                            if (category.CategoryId is not null && category.CategoryId != Guid.Empty)
                                local.CategoryId = category.CategoryId.Value;
                            await categoryRepo.UpdateAsync(local);
                        }
                    }
                    else
                    {
                        // Insert new record
                        await categoryRepo.AddAsync(new CategoryDTO
                        {
                            CategoryId = category.CategoryId ?? Guid.Empty,
                            ExternalId = category.Id,
                            Name = category.Name,
                            Inactive = category.Inactive,
                            SystemDefault = category.SystemDefault,
                            IsMainCategory = category.IsMainTransactionCategory,
                            ParentExternalId = category.ParentTransactionCategoryId,
                            UserId = uid,
                            Type = SafeParseCategoryType(category.Type),
                            UpdatedAt = category.UpdatedAt
                        });
                    }
                }

                DateTime batchMax = apiRes.Max(c => c.UpdatedAt);
                if (batchMax > maxServerTs)
                    maxServerTs = batchMax;

                if (apiRes.Count < pageSize) break;
                page++;
            }

            // Advance the cursor to the highest server-side UpdatedAt seen across all pages.
            if (maxServerTs > DateTime.MinValue)
            {
                DateTime current = await syncCursorRepo.GetAsync(SyncCursorKeys.Category);
                if (maxServerTs > current)
                    await syncCursorRepo.SaveAsync(SyncCursorKeys.Category, maxServerTs);
            }
        }

        public async Task<List<CategoryDTO>> GetAllAsync()
        {
            var all = await categoryRepo.GetAllAsync();

            var mainById = all.Where(c => c.IsMainCategory && c.ExternalId != null).ToDictionary(c => c.ExternalId, c => c.Name);

            return all
                .OrderBy(c => c.IsMainCategory ? c.Name : mainById.GetValueOrDefault(c.ParentExternalId ?? 0, string.Empty))
                .ThenBy(c => c.IsMainCategory ? 0 : 1)
                .ThenBy(c => c.Name)
                .ToList();
        }

        public async Task<bool> ExistsByNameAsync(string name, Guid? excludeCategoryId = null)
            => await categoryRepo.ExistsByNameAsync(name, excludeCategoryId);

        public async Task<List<CategoryDTO>> GetAllGroupedAsync()
        {
            var all = await categoryRepo.GetAllAsync();
            return GroupCategories(all);
        }

        /// <summary>
        /// Pure sorting/grouping logic extracted for testability.
        /// Main categories alphabetically (active first, then inactive), each followed
        /// by its subcategories (active alphabetically, then inactive alphabetically),
        /// orphan subcategories at the end.
        /// </summary>
        public static List<CategoryDTO> GroupCategories(IEnumerable<CategoryDTO> all)
        {
            var allList = all.ToList();
            var mainCategories = allList.Where(c => c.IsMainCategory).ToList();
            var subCategories = allList.Where(c => !c.IsMainCategory).ToList();

            var mainExternalIds = new HashSet<int?>(mainCategories
                .Where(c => c.ExternalId != null)
                .Select(c => c.ExternalId));

            var sortedMains = mainCategories
                .OrderBy(c => c.Inactive ? 1 : 0)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var result = new List<CategoryDTO>();

            foreach (var main in sortedMains)
            {
                result.Add(main);

                var children = subCategories
                    .Where(s => s.ParentExternalId != null && s.ParentExternalId == main.ExternalId)
                    .OrderBy(s => s.Inactive ? 1 : 0)
                    .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                result.AddRange(children);
            }

            // Orphan subcategories: no matching main category exists for their ParentExternalId
            var orphans = subCategories
                .Where(s => s.ParentExternalId == null || !mainExternalIds.Contains(s.ParentExternalId))
                .OrderBy(s => s.Inactive ? 1 : 0)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            result.AddRange(orphans);

            return result;
        }

        public async Task AddLocalAsync(CategoryDTO category)
        {
            if (category.CategoryId == Guid.Empty)
                category.CategoryId = Guid.NewGuid();

            // Subcategory type inheritance: assign Type from parent MainCategory
            if (!category.IsMainCategory)
            {
                if (category.ParentExternalId is null)
                    throw new InvalidOperationException("Selecione uma categoria pai válida");

                var parent = await categoryRepo.GetByExternalIdAsync(category.ParentExternalId.Value);
                if (parent is null)
                    throw new InvalidOperationException("Selecione uma categoria pai válida");

                category.Type = parent.Type;
            }

            await categoryRepo.AddAsync(category);
        }

        public async Task UpdateLocalAsync(CategoryDTO category)
        {
            category.UpdatedAt = DateTime.UtcNow;

            // Subcategory type inheritance: update Type when parent changes
            if (!category.IsMainCategory)
            {
                if (category.ParentExternalId is null)
                    throw new InvalidOperationException("Selecione uma categoria pai válida");

                var parent = await categoryRepo.GetByExternalIdAsync(category.ParentExternalId.Value);
                if (parent is null)
                    throw new InvalidOperationException("Selecione uma categoria pai válida");

                category.Type = parent.Type;
            }

            await categoryRepo.UpdateAsync(category);
        }

        public async Task UpdateMainCategoryTypeAsync(CategoryDTO mainCategory, CategoryType newType)
        {
            mainCategory.Type = newType;
            mainCategory.UpdatedAt = DateTime.UtcNow;
            await categoryRepo.UpdateAsync(mainCategory);

            // Cascade to active subcategories
            var all = await categoryRepo.GetAllAsync();
            var subcategories = all.Where(c =>
                !c.IsMainCategory
                && !c.Inactive
                && c.ParentExternalId == mainCategory.ExternalId);

            foreach (var sub in subcategories)
            {
                sub.Type = newType;
                sub.UpdatedAt = DateTime.UtcNow;
                await categoryRepo.UpdateAsync(sub);
            }
        }

        public async Task PushAsync()
        {
            var pending = await categoryRepo.GetPendingPushAsync();
            foreach (var category in pending)
            {
                try
                {
                    var response = await categoryApiRepo.PostCategoryAsync(new Model.Req.CategoryReq
                    {
                        CategoryId = category.CategoryId,
                        Name = category.Name,
                        IsMainTransactionCategory = category.IsMainCategory,
                        ParentTransactionCategoryId = category.ParentExternalId,
                        Inactive = category.Inactive,
                        Color = null,
                        Type = (int)category.Type
                    });

                    if (response.Id > 0)
                    {
                        category.ExternalId = response.Id;
                        await categoryRepo.UpdateAsync(category);
                    }
                }
                catch
                {
                    // Continue with remaining records — failed one retries next cycle
                }
            }
        }

        public async Task UpsertAsync(CategoryDTO category)
        {
            CategoryDTO? existingCategory = null;
            if (category.ExternalId.HasValue)
            {
                existingCategory = await categoryRepo.GetByExternalIdAsync(category.ExternalId.Value);
            }

            if (existingCategory != null)
            {
                if (existingCategory.UpdatedAt < category.UpdatedAt)
                {
                    category.Id = existingCategory.Id;
                    await categoryRepo.UpdateAsync(category);
                }
            }
            else
            {
                await categoryRepo.AddAsync(category);
            }
        }
    }
}
