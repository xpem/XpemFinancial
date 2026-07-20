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
        Task UpsertAsync(CategoryDTO category);
        Task AddLocalAsync(CategoryDTO category);
        Task UpdateLocalAsync(CategoryDTO category);
        Task<DateTime> GetLastUpdatedAtAsync();
        Task PullAsync(int uid, DateTime lastUpdatedAt);
        Task PushAsync();
    }

    public class CategoryService(ICategoryRepo categoryRepo, ICategoryApiRepo categoryApiRepo, ISyncCursorRepo syncCursorRepo) : ICategoryService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

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

            await categoryRepo.AddAsync(category);
        }

        public async Task UpdateLocalAsync(CategoryDTO category)
        {
            category.UpdatedAt = DateTime.UtcNow;
            await categoryRepo.UpdateAsync(category);
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
                        Color = null
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
