using Model.Req;
using Model.Resp.Api;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiRepo
{
    public interface IAccountApiRepo
    {
        Task<AdjustAccountBalanceReq> PostAdjustAccountBalance(AdjustAccountBalanceReq req);
        Task<AccountApiRes?> GetAccountAsync(DateTime updatedAt);
    }

    public class AccountApiRepo(IUserApiRepo userApiRepo) : IAccountApiRepo
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public async Task<AdjustAccountBalanceReq> PostAdjustAccountBalance(AdjustAccountBalanceReq req)
        {
            string json = JsonSerializer.Serialize(req);
            ApiResp resp = await userApiRepo.AuthRequestAsync(RequestsTypes.Post, ApiKeys.ApiAddress + "/financial/adjustAccountBalance", json);

            if (!resp.Success || resp.Content is null)
                throw new Exception($"Falha ao ajustar balanço na API: {resp.Error}");

            return JsonSerializer.Deserialize<AdjustAccountBalanceReq>(resp.Content, _jsonOptions)
                ?? throw new Exception("Resposta inválida ao ajustar balanço na API.");
        }

        public async Task<AccountApiRes?> GetAccountAsync(DateTime updatedAt)
        {
            ApiResp resp = await userApiRepo.AuthRequestAsync(
                RequestsTypes.Get,
                ApiKeys.ApiAddress + $"/financial/account?updatedAt={updatedAt:yyyy-MM-ddTHH:mm:ss.fff}");

            if (resp.Success && resp.Content is string content)
            {
                if (!string.IsNullOrEmpty(content))
                    return JsonSerializer.Deserialize<AccountApiRes>(resp.Content, _jsonOptions);
            }

            return null;
        }
    }
}
