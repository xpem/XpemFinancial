using Model.Req;
using Model.Resp.Api;
using System.Text.Json;

namespace ApiRepo
{
    public interface IAccountApiRepo
    {
        Task PostAdjustAccountBalance(AdjustAccountBalanceReq req);
    }

    public class AccountApiRepo(IUserApiRepo userApiRepo) : IAccountApiRepo
    {
        public async Task PostAdjustAccountBalance(AdjustAccountBalanceReq req)
        {
            string json = JsonSerializer.Serialize(req);
            await userApiRepo.AuthRequestAsync(RequestsTypes.Post, ApiKeys.ApiAddress + "/financial/adjustAccountBalance", json);
        }
    }
}
