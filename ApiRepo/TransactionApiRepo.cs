using Model.DTO;
using Model.Req;
using Model.Resp.Api;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiRepo
{
    public interface ITransactionApiRepo
    {
        Task<List<TransactionApiRes>?> GetByUpdatedAtAsync(DateTime updatedAt);
        Task<int> PostAsync(TransactionReq req);
    }

    public class TransactionApiRepo(IUserApiRepo userApiRepo) : ITransactionApiRepo
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public async Task<List<TransactionApiRes>?> GetByUpdatedAtAsync(DateTime updatedAt)
        {
            ApiResp resp = await userApiRepo.AuthRequestAsync(
                RequestsTypes.Get,
                ApiKeys.ApiAddress + $"/financial/transaction?updatedAt={updatedAt:yyyy-MM-ddTHH:mm:ss.fff}");

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

            if (!resp.Success || resp.Content is null)
                throw new Exception($"Falha ao adicionar transação na API: {resp.Error}");

            // O backend retorna o TransactionDTO com o Id gerado
            var result = JsonSerializer.Deserialize<TransactionApiRes>(resp.Content, _jsonOptions)
                ?? throw new Exception("Resposta inválida ao adicionar transação na API.");

            return result.Id;
        }
    }
}
