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

        // New multi-account methods
        Task<List<AccountApiRes>> GetAccountsAsync(DateTime updatedAt);
        Task<AccountApiRes> PostAccountAsync(AccountReq req);
        Task<AccountApiRes> PutAccountAsync(int externalId, AccountReq req);
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

            // Server unreachable — local update already applied, sync will retry later.
            if (resp.Error == ErrorTypes.ServerUnavaliable)
                return req;

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

        public async Task<List<AccountApiRes>> GetAccountsAsync(DateTime updatedAt)
        {
            ApiResp resp = await userApiRepo.AuthRequestAsync(
                RequestsTypes.Get,
                ApiKeys.ApiAddress + $"/financial/accounts?updatedAt={updatedAt:yyyy-MM-ddTHH:mm:ss.fff}");

            if (resp.Error == ErrorTypes.ServerUnavaliable)
                return [];

            if (!resp.Success)
                throw new Exception($"Falha ao buscar contas na API: {resp.Error}");

            // 204 NoContent → empty string body → means no updated accounts
            if (string.IsNullOrEmpty(resp.Content))
                return [];

            return JsonSerializer.Deserialize<List<AccountApiRes>>(resp.Content, _jsonOptions) ?? [];
        }

        public async Task<AccountApiRes> PostAccountAsync(AccountReq req)
        {
            string json = JsonSerializer.Serialize(req, _jsonOptions);
            ApiResp resp = await userApiRepo.AuthRequestAsync(
                RequestsTypes.Post,
                ApiKeys.ApiAddress + "/financial/account",
                json);

            if (resp.Error == ErrorTypes.ServerUnavaliable)
                throw new Exception("Servidor indisponível ao criar conta.");

            if (!resp.Success || resp.Content is null)
                throw new Exception($"Falha ao criar conta na API: {resp.Error}");

            return JsonSerializer.Deserialize<AccountApiRes>(resp.Content, _jsonOptions)
                ?? throw new Exception("Resposta inválida ao criar conta na API.");
        }

        public async Task<AccountApiRes> PutAccountAsync(int externalId, AccountReq req)
        {
            string json = JsonSerializer.Serialize(req, _jsonOptions);
            ApiResp resp = await userApiRepo.AuthRequestAsync(
                RequestsTypes.Put,
                ApiKeys.ApiAddress + $"/financial/account/{externalId}",
                json);

            if (resp.Error == ErrorTypes.ServerUnavaliable)
                throw new Exception("Servidor indisponível ao atualizar conta.");

            if (!resp.Success || resp.Content is null)
                throw new Exception($"Falha ao atualizar conta na API: {resp.Error}");

            return JsonSerializer.Deserialize<AccountApiRes>(resp.Content, _jsonOptions)
                ?? throw new Exception("Resposta inválida ao atualizar conta na API.");
        }
    }
}
