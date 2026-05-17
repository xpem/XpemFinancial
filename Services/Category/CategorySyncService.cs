using Model.DTO;
using Model.Resp.Api;

namespace Service.Category
{
    public interface ICategorySyncService
    {
        Task PullAsync(int uid);
    }

    public class CategorySyncService(ICategoryApiService categoryApiService, ICategoryService categoryService) : ICategorySyncService
    {

        public async Task PullAsync(int uid)
        {
            DateTime lastUpdate = DateTime.MinValue;
            int page = 1;
            List<TransactionCategoryApiRes>? apiRes;

            apiRes = await categoryApiService.GetByLastUpdateAsync(lastUpdate, page);

            if (apiRes is null) return;

            foreach (var category in apiRes)
            {

                if (category == null) continue;

                CategoryDTO categoryDTO = new()
                {
                    ExternalId = category.Id,
                    Name = category.Name,
                    Inactive = category.Inactive,
                    SystemDefault = category.SystemDefault,
                    IsMainCategory = category.IsMainTransactionCategory,
                    ParentExternalId = category.ParentTransactionCategoryId,
                    UserId = uid
                };

                await categoryService.UpsertAsync(categoryDTO);
            }
        }
    }
}


