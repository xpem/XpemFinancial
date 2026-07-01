using Model.Req;
using Model.Resp.Api;
using System.Text.Json;

namespace ApiRepo
{
    public interface ICategoryApiRepo
    {
        Task<ApiResp> GetByLastUpdateAsync(DateTime lastUpdate, int page);
        Task<CategoryPushRes> PostCategoryAsync(CategoryReq req);
    }

    public class CategoryApiRepo(IUserApiRepo userApiRepo) : ICategoryApiRepo
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<ApiResp> GetByLastUpdateAsync(DateTime lastUpdate, int page)
        {
            return await userApiRepo.AuthRequestAsync(RequestsTypes.Get, ApiKeys.ApiAddress + $"/financial/categories?updatedAt={lastUpdate:yyyy-MM-ddThh:mm:ss.fff}");
        }

        public async Task<CategoryPushRes> PostCategoryAsync(CategoryReq req)
        {
            string json = JsonSerializer.Serialize(req);
            ApiResp resp = await userApiRepo.AuthRequestAsync(
                RequestsTypes.Post,
                ApiKeys.ApiAddress + "/financial/category",
                json);

            if (!resp.Success || resp.Content is null)
                throw new Exception($"Falha ao adicionar categoria na API: {resp.Error}");

            return JsonSerializer.Deserialize<CategoryPushRes>(resp.Content, _jsonOptions)
                ?? throw new Exception("Resposta inválida ao adicionar categoria na API.");
        }
    }
}
