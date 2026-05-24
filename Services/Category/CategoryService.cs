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
        Task<DateTime> GetLastUpdatedAtAsync();
        Task UpsertAsync(CategoryDTO category);
        Task PullAsync(int uid);
    }

    public class CategoryService(ICategoryRepo categoryRepo, ICategoryApiRepo categoryApiRepo) : ICategoryService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public async Task<DateTime> GetLastUpdatedAtAsync()
        {
            var all = await categoryRepo.GetAllAsync();
            return all.Count > 0 ? all.Max(c => c.UpdatedAt) : DateTime.MinValue;
        }

        public async Task PullAsync(int uid)
        {
            DateTime lastUpdatedAt = await GetLastUpdatedAtAsync();

            ApiResp apiResp = await categoryApiRepo.GetByLastUpdateAsync(lastUpdatedAt, page: 1);

            if (!apiResp.Success || apiResp.Content is null)
                throw new Exception($"Erro ao buscar categorias da API: {apiResp.Content ?? "sem resposta"}");

            List<TransactionCategoryApiRes>? apiRes = JsonSerializer.Deserialize<List<TransactionCategoryApiRes>>(apiResp.Content, _jsonOptions);

            if (apiRes is null) return;

            foreach (var category in apiRes)
            {
                if (category == null) continue;

                await UpsertAsync(new CategoryDTO
                {
                    ExternalId = category.Id,
                    Name = category.Name,
                    Inactive = category.Inactive,
                    SystemDefault = category.SystemDefault,
                    IsMainCategory = category.IsMainTransactionCategory,
                    ParentExternalId = category.ParentTransactionCategoryId,
                    UserId = uid
                });
            }
        }

        public async Task<List<CategoryDTO>> GetAllAsync()
        {
            var all = await categoryRepo.GetAllAsync();

            var mainById = all
                .Where(c => c.IsMainCategory)
                .ToDictionary(c => c.ExternalId, c => c.Name);

            return all
                .OrderBy(c => c.IsMainCategory ? c.Name : mainById.GetValueOrDefault(c.ParentExternalId ?? 0, string.Empty))
                .ThenBy(c => c.IsMainCategory ? 0 : 1)
                .ThenBy(c => c.Name)
                .ToList();
        }

        public async Task UpsertAsync(CategoryDTO category)
        {
            var existingCategory = await categoryRepo.GetByExternalIdAsync(category.ExternalId!.Value);

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
