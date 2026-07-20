using Model.Req;
using Model.Resp.Api;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiRepo
{
    public interface IRecurringRuleApiRepo
    {
        Task<int> PostAsync(RecurringRuleReq req);
        Task<List<RecurringRuleApiRes>?> GetByUpdatedAtAsync(DateTime updatedAt, int page);
    }

    public class RecurringRuleApiRepo(IUserApiRepo userApiRepo) : IRecurringRuleApiRepo
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public async Task<List<RecurringRuleApiRes>?> GetByUpdatedAtAsync(DateTime updatedAt, int page)
        {
            ApiResp resp = await userApiRepo.AuthRequestAsync(
                RequestsTypes.Get,
                ApiKeys.ApiAddress + $"/financial/recurringrule?updatedAt={updatedAt:yyyy-MM-ddTHH:mm:ss.fff}&page={page}");

            if (resp.Success && !string.IsNullOrEmpty(resp.Content))
                return JsonSerializer.Deserialize<List<RecurringRuleApiRes>>(resp.Content, _jsonOptions);

            return null;
        }

        public async Task<int> PostAsync(RecurringRuleReq req)
        {
            string json = JsonSerializer.Serialize(req);
            ApiResp resp = await userApiRepo.AuthRequestAsync(
                RequestsTypes.Post,
                ApiKeys.ApiAddress + "/financial/recurringrule",
                json);

            // Server unreachable — local rule already saved, sync will push it later.
            if (resp.Error == ErrorTypes.ServerUnavaliable)
                return 0;

            if (!resp.Success || resp.Content is null)
                throw new Exception($"Falha ao adicionar regra recorrente na API: {resp.Error}");

            var result = JsonSerializer.Deserialize<RecurringRuleApiRes>(resp.Content, _jsonOptions)
                ?? throw new Exception("Resposta inválida ao adicionar regra recorrente na API.");

            return result.Id;
        }
    }
}
