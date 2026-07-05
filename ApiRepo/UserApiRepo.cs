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
        private const string UserEndpoint = "/user";
        private const string SessionEndpoint = "/user/session";
        private const string RefreshSessionEndpoint = "/user/session/refresh";
        private const string RefreshTokenProperty = "refreshToken";
        private const string TokenProperty = "token";
        private const string ErrorProperty = "error";
        private const string ErrorsProperty = "errors";

        public async Task<ApiResp> SignUpAsync(string name, string email, string password)
        {
            email = email.ToLowerInvariant();
            string json = JsonSerializer.Serialize(new { name, email, password });

            return await HttpClientFunctions.Request(RequestsTypes.Post, ApiKeys.ApiAddress + UserEndpoint, jsonContent: json);
        }

        public async Task<ApiResp> RecoverPasswordAsync(string email)
        {
            string json = JsonSerializer.Serialize(new { email });

            return await HttpClientFunctions.Request(RequestsTypes.Post, ApiKeys.ApiAddress + "/user/recoverpassword", jsonContent: json);
        }

        public async Task<ApiResp> GetTokenAsync(string email, string password)
        {
            string json = JsonSerializer.Serialize(new { email, password });

            var resp = await HttpClientFunctions.Request(RequestsTypes.Post, ApiKeys.ApiAddress + SessionEndpoint, jsonContent: json);

            if (resp is null || resp.Content is null)
                throw new InvalidOperationException("Resposta inválida ao obter token.");

            if (!TryParseJson(resp.Content, out JsonNode? jResp))
                return new ApiResp { Success = false, Content = resp.Content };

            if (resp.Success)
            {
                string? token = GetStringValue(jResp, TokenProperty);

                if (!string.IsNullOrWhiteSpace(token))
                {
                    string? refreshToken = GetStringValue(jResp, RefreshTokenProperty);
                    string content = JsonSerializer.Serialize(new { token, refreshToken });

                    return new ApiResp { Success = true, Content = content };
                }
            }
            else
            {
                string? error = GetStringValue(jResp, ErrorsProperty) ?? GetStringValue(jResp, ErrorProperty);

                return new ApiResp
                {
                    Success = false,
                    Content = error ?? resp.Content
                };
            }

            return new ApiResp { Success = false, Content = resp.Content };
        }

        public async Task<(bool success, string? newToken)> RefreshToken()
        {
            UserDTO? user = await userRepo.GetAsync();

            if (string.IsNullOrWhiteSpace(user?.RefreshToken))
                return (false, null);

            string json = JsonSerializer.Serialize(new { refreshToken = user.RefreshToken });

            var resp = await HttpClientFunctions.Request(RequestsTypes.Post, ApiKeys.ApiAddress + RefreshSessionEndpoint, jsonContent: json);

            if (!resp.Success || string.IsNullOrWhiteSpace(resp.Content))
                return (false, null);

            if (!TryParseJson(resp.Content, out JsonNode? jResp))
                return (false, null);

            string? newToken = GetStringValue(jResp, TokenProperty);

            if (string.IsNullOrWhiteSpace(newToken))
                return (false, null);

            string? newRefreshToken = GetStringValue(jResp, RefreshTokenProperty);

            user.Token = newToken;
            user.RefreshToken = newRefreshToken;
            await userRepo.UpdateAsync(user);

            return (true, newToken);
        }

        public async Task<ApiResp> AuthRequestAsync(RequestsTypes requestsType, string url, string? jsonContent = null)
        {
            UserDTO? user = await userRepo.GetAsync();
            string? userToken = user?.Token;

            if (string.IsNullOrWhiteSpace(userToken))
                throw new UnauthorizedAccessException("Usuário não autenticado.");

            ApiResp resp = await HttpClientFunctions.Request(requestsType, url, userToken, jsonContent);

            if (!resp.TryRefreshToken)
                return resp;

            (bool refreshTokenSuccess, string? newToken) = await RefreshToken();

            if (!refreshTokenSuccess || string.IsNullOrWhiteSpace(newToken))
                return resp;

            return await HttpClientFunctions.Request(requestsType, url, newToken, jsonContent);
        }

        public async Task<ApiResp> GetAsync(string userToken) =>
            await HttpClientFunctions.Request(RequestsTypes.Get, ApiKeys.ApiAddress + UserEndpoint, userToken: userToken);

        private static bool TryParseJson(string content, out JsonNode? node)
        {
            try
            {
                node = JsonNode.Parse(content);
                return node is not null;
            }
            catch (JsonException)
            {
                node = null;
                return false;
            }
        }

        private static string? GetStringValue(JsonNode? node, string propertyName)
        {
            JsonNode? value = node?[propertyName];

            return value switch
            {
                null => null,
                JsonValue jsonValue when jsonValue.TryGetValue<string>(out string? text) => text,
                _ => value.ToJsonString()
            };
        }
    }
}
