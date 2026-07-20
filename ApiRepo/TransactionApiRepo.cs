using Model.DTO;
using Model.Req;
using Model.Resp.Api;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiRepo
{
    public interface ITransactionApiRepo
    {
        Task<List<TransactionApiRes>?> GetByUpdatedAtAsync(DateTime updatedAt, int page);
        Task<int> PostAsync(TransactionReq req);
        Task PutAsync(int externalId, TransactionReq req);
    }

    public class TransactionApiRepo(IUserApiRepo userApiRepo) : ITransactionApiRepo
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public async Task<List<TransactionApiRes>?> GetByUpdatedAtAsync(DateTime updatedAt, int page)
        {
            ApiResp resp = await userApiRepo.AuthRequestAsync(
                RequestsTypes.Get,
                ApiKeys.ApiAddress + $"/financial/transaction?updatedAt={updatedAt:yyyy-MM-ddTHH:mm:ss.fff}&page={page}");

            if (resp.Success && !string.IsNullOrEmpty(resp.Content))
                return JsonSerializer.Deserialize<List<TransactionApiRes>>(resp.Content, _jsonOptions);

            return null;
        }

        public async Task<int> PostAsync(TransactionReq req)
        {
            string json = JsonSerializer.Serialize(req);
            ApiResp resp = await userApiRepo.AuthRequestAsync(
                RequestsTypes.Post,
                ApiKeys.ApiAddress + "/financial/transaction",
                json);

            // Server unreachable — the local record is already saved.
            // Return 0 so the caller knows there is no ExternalId yet;
            // the background sync will push it later.
            if (resp.Error == ErrorTypes.ServerUnavaliable)
                return 0;

            if (!resp.Success || resp.Content is null)
                throw new Exception($"Falha ao adicionar transação na API: {resp.Error}");

            // O backend retorna o TransactionDTO com o Id gerado
            var result = JsonSerializer.Deserialize<TransactionApiRes>(resp.Content, _jsonOptions)
                ?? throw new Exception("Resposta inválida ao adicionar transação na API.");

            return result.Id;
        }

        public async Task PutAsync(int externalId, TransactionReq req)
        {
            string json = JsonSerializer.Serialize(req);
            ApiResp resp = await userApiRepo.AuthRequestAsync(
                RequestsTypes.Put,
                ApiKeys.ApiAddress + $"/financial/transaction/{externalId}",
                json);

            // Server unreachable — local update already applied, sync will retry later.
            if (resp.Error == ErrorTypes.ServerUnavaliable)
                return;

            if (!resp.Success)
                throw new Exception($"Falha ao atualizar transação na API: {resp.Error}");
        }
    }
}
