using Model.Resp.Api;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiRepo
{
    public interface ITransactionApiRepo
    {
        Task<List<TransactionApiRes>?> GetByUpdatedAtAsync(DateTime updatedAt);
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

            if (resp.Success && !string.IsNullOrEmpty( resp.Content))
                return JsonSerializer.Deserialize<List<TransactionApiRes>>(resp.Content, _jsonOptions);

            return null;
        }
    }
}
