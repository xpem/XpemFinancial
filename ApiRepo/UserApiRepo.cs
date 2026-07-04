using Model.DTO;
using Model.Resp.Api;
using Repo;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ApiRepo
{
    public interface IUserApiRepo
    {
        Task<ApiResp> SignUpAsync(string name, string email, string password);
        Task<ApiResp> RecoverPasswordAsync(string email);
        Task<ApiResp> GetTokenAsync(string email, string password);
        Task<(bool success, string? newToken)> RefreshToken();
        Task<ApiResp> GetAsync(string userToken);
        Task<ApiResp> AuthRequestAsync(RequestsTypes requestsType, string url, string? jsonContent = null);
    }

    public class UserApiRepo(IUserRepo userRepo) : IUserApiRepo
    {
        public async Task<ApiResp> SignUpAsync(string name, string email, string password)
        {
            email = email.ToLower();
            string json = JsonSerializer.Serialize(new { name, email, password });

            return await HttpClientFunctions.Request(RequestsTypes.Post, ApiKeys.ApiAddress + "/user", jsonContent: json);
        }

        public async Task<ApiResp> RecoverPasswordAsync(string email)
        {
            try
            {
                string json = JsonSerializer.Serialize(new { email });

                return await HttpClientFunctions.Request(RequestsTypes.Post, ApiKeys.ApiAddress + "/user/recoverpassword", jsonContent: json);
            }
            catch (Exception) { throw; }
        }

        public async Task<ApiResp> GetTokenAsync(string email, string password)
        {
            string json = JsonSerializer.Serialize(new { email, password });

            var resp = await HttpClientFunctions.Request(RequestsTypes.Post, ApiKeys.ApiAddress + "/user/session", jsonContent: json);

            if (resp is null || resp.Content is null)
                throw new ArgumentNullException(nameof(resp));

            JsonNode? jResp;

            try
            {
                jResp = JsonNode.Parse(resp.Content);
            }
            catch (JsonException)
            {
                return new ApiResp() { Success = false, Content = resp.Content };
            }

            if (resp.Success)
            {
                if (jResp?["token"]?.GetValue<string>() is string token)
                {
                    string? refreshToken = jResp?["refreshToken"]?.GetValue<string>();
                    string content = JsonSerializer.Serialize(new { token, refreshToken });
                    return new ApiResp() { Success = true, Content = content };
                }
            }
            else
            {
                if (jResp?["errors"]?.GetValue<string>() is string errors)
                    return new ApiResp() { Success = false, Content = errors };
                else if (jResp?["error"]?.GetValue<string>() is string error)
                    return new ApiResp() { Success = false, Content = error };

                return new ApiResp() { Success = false, Content = resp.Content };
            }

            return new ApiResp() { Success = false, Content = resp.Content };
        }

        public async Task<(bool success, string? newToken)> RefreshToken()
        {
            UserDTO? user = await userRepo.GetAsync();

            if (user is not null && user.RefreshToken is not null)
            {
                string json = JsonSerializer.Serialize(new { refreshToken = user.RefreshToken });

                var resp = await HttpClientFunctions.Request(RequestsTypes.Post, ApiKeys.ApiAddress + "/user/session/refresh", jsonContent: json);

                if (resp.Success && resp.Content is not null)
                {
                    JsonNode? jResp = JsonNode.Parse(resp.Content);
                    string? newToken = jResp?["token"]?.GetValue<string>();
                    string? newRefreshToken = jResp?["refreshToken"]?.GetValue<string>();

                    if (newToken is not null)
                    {
                        user.Token = newToken;
                        user.RefreshToken = newRefreshToken;
                        await userRepo.UpdateAsync(user);

                        return (true, newToken);
                    }
                }

                throw new UnauthorizedAccessException("Falha ao tentar recuperar token do usuario");
            }

            return (false, null);
        }

        public async Task<ApiResp> AuthRequestAsync(RequestsTypes requestsType, string url, string? jsonContent = null)
        {
            bool retry = true;
            ApiResp? resp = null;

            while (retry)
            {
                string? userToken;

                if (resp is not null && resp.TryRefreshToken)
                {
                    retry = false;

                    (bool refreshTokenSuccess, userToken) = await RefreshToken();

                    if (!refreshTokenSuccess || userToken is null)
                        return resp;
                }
                else
                {
                    userToken = (await userRepo.GetAsync())?.Token;

                    if (userToken is null) throw new ArgumentNullException(nameof(userToken));
                }

                resp = await HttpClientFunctions.Request(requestsType, url, userToken, jsonContent);

                if (!resp.TryRefreshToken || !retry) return resp;
            }

            throw new Exception($"Erro ao tentar AuthRequest de tipo {requestsType} na url: {url}");
        }

        public async Task<ApiResp> GetAsync(string userToken) => await HttpClientFunctions.Request(RequestsTypes.Get, ApiKeys.ApiAddress + "/user", userToken: userToken);

    }
}
