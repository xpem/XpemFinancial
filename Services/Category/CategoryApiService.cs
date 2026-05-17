using ApiRepo;
using Model.Resp.Api;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Service.Category
{
    public interface ICategoryApiService
    {
        Task<List<TransactionCategoryApiRes>?> GetByLastUpdateAsync(DateTime lastUpdate, int page);
    }

    public class CategoryApiService(ICategoryApiRepo categoryApiRepo) : ICategoryApiService
    {
        public async Task<List<TransactionCategoryApiRes>?> GetByLastUpdateAsync(DateTime lastUpdate, int page)
        {
            ApiResp apiResp = await categoryApiRepo.GetByLastUpdateAsync(lastUpdate, page);

            if (apiResp is not null && apiResp.Success && apiResp.Content is not null)
            {
                return JsonSerializer.Deserialize<List<TransactionCategoryApiRes>?>(apiResp.Content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            else
            {
                if (apiResp?.Content is not null and string)
                    throw new Exception($"Error getting data from API: {apiResp.Content}");
                else
                    throw new Exception("Error getting data from API");
            }
        }
    }
}
