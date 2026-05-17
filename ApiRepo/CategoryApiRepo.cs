using Model.Resp.Api;

namespace ApiRepo
{
    public interface ICategoryApiRepo
    {
        Task<ApiResp> GetByLastUpdateAsync(DateTime lastUpdate, int page);
    }

    public class CategoryApiRepo(IUserApiRepo userApiRepo) : ICategoryApiRepo
    {
        public async Task<ApiResp> GetByLastUpdateAsync(DateTime lastUpdate, int page)
        {
            return await userApiRepo.AuthRequestAsync(RequestsTypes.Get, ApiKeys.ApiAddress + $"/financial/categories?updatedAt={lastUpdate:yyyy-MM-ddThh:mm:ss.fff}");
        }
    }
}
