using ApiRepo;
using Model.DTO;
using Model.Resp;
using Model.Resp.Api;
using System.Reflection.Metadata;
using System.Text.Json.Nodes;

namespace Service
{
    public interface IUserService
    {
        Task AddUserAsync(UserDTO user);

        Task<UserDTO?> GetAsync();

        Task<ServiceResp> SignInAsync(string email, string password);

        Task<ServiceResp> SignInWithGoogleAsync(string idToken);

        /// <summary>
        /// Recebe o token da API já emitido pelo servidor após o fluxo OAuth via browser.
        /// Busca os dados do usuário e salva localmente — sem chamar o endpoint Google do servidor.
        /// </summary>
        Task<ServiceResp> SignInWithGoogleTokenAsync(string apiToken, string? refreshToken);

        Task<ServiceResp> SignUpAsync(string name, string email, string password);

        Task UpdateLastUpdate(int uid);

        Task<(bool success, string? message)> RecoverPassword(string email);

        Task UpdateIncludePreviousBalanceAsync(bool value, int uid);
    }

    public class UserService(Repo.IUserRepo userRepo, IUserApiRepo userApiRepo, IBuildDbService buildDbService) : IUserService
    {
        public async Task AddUserAsync(UserDTO user)
        {
            await userRepo.AddAsync(user);
        }

        public async Task<UserDTO?> GetAsync()
        {
            return await userRepo.GetAsync();
        }

        public async Task<ServiceResp> SignInAsync(string email, string password)
        {
            email = email.ToLower();

            var apiresp = await userApiRepo.GetTokenAsync(email, password);

            if (apiresp.Success && apiresp.Content is not null)
            {
                JsonNode? tokenResp = JsonNode.Parse(apiresp.Content);
                string? newToken = tokenResp?["token"]?.GetValue<string>();
                string? refreshToken = tokenResp?["refreshToken"]?.GetValue<string>();

                if (newToken is not null)
                {
                    ApiResp resp = await userApiRepo.GetAsync(newToken);

                    if (resp.Success && resp.Content != null)
                    {
                        JsonNode? userResponse = JsonNode.Parse(resp.Content);
                        if (userResponse is not null)
                        {
                            UserDTO user = new()
                            {
                                Id = userResponse["id"]?.GetValue<int>() ?? 0,
                                Name = userResponse["name"]?.GetValue<string>(),
                                Email = userResponse["email"]?.GetValue<string>(),
                                Token = newToken,
                                RefreshToken = refreshToken
                            };

                            UserDTO? actualUser = await userRepo.GetAsync();

                            if (actualUser != null)
                            {
                                if (actualUser.Id == user.Id)
                                    await userRepo.UpdateAsync(user);
                                else
                                {
                                    await buildDbService.CleanLocalDatabaseAsync();
                                    await userRepo.AddAsync(user);
                                }
                            }
                            else
                                await userRepo.AddAsync(user);

                            return new ServiceResp(true, user);
                        }
                    }
                }
            }
            else if (!apiresp.Success)
            {
                if (apiresp.Content?.Contains("User/Password incorrect") == true || apiresp.Content?.Contains("Invalid Email") == true)
                    return new ServiceResp(false, ErrorTypes.WrongEmailOrPassword);
                
                return new ServiceResp(false, ErrorTypes.ServerUnavaliable);
            }

            return new ServiceResp(false, ErrorTypes.Unknown);
        }

        public async Task<ServiceResp> SignInWithGoogleTokenAsync(string apiToken, string? refreshToken)
        {
            // O token já foi emitido pelo servidor — só precisa buscar os dados do usuário
            ApiResp resp = await userApiRepo.GetAsync(apiToken);

            if (!resp.Success || resp.Content is null)
                return new ServiceResp(false, ErrorTypes.ServerUnavaliable);

            JsonNode? userResponse = JsonNode.Parse(resp.Content);
            if (userResponse is null)
                return new ServiceResp(false, ErrorTypes.Unknown);

            UserDTO user = new()
            {
                Id = userResponse["id"]?.GetValue<int>() ?? 0,
                Name = userResponse["name"]?.GetValue<string>(),
                Email = userResponse["email"]?.GetValue<string>(),
                Token = apiToken,
                RefreshToken = refreshToken
            };

            UserDTO? actualUser = await userRepo.GetAsync();

            if (actualUser != null)
            {
                if (actualUser.Id == user.Id)
                    await userRepo.UpdateAsync(user);
                else
                {
                    await buildDbService.CleanLocalDatabaseAsync();
                    await userRepo.AddAsync(user);
                }
            }
            else
                await userRepo.AddAsync(user);

            return new ServiceResp(true, user);
        }

        public async Task<ServiceResp> SignInWithGoogleAsync(string idToken)
        {
            var apiresp = await userApiRepo.GoogleSignInAsync(idToken);

            if (apiresp.Success && apiresp.Content is not null)
            {
                JsonNode? tokenResp = JsonNode.Parse(apiresp.Content);
                string? newToken = tokenResp?["token"]?.GetValue<string>();
                string? refreshToken = tokenResp?["refreshToken"]?.GetValue<string>();

                if (newToken is not null)
                {
                    ApiResp resp = await userApiRepo.GetAsync(newToken);

                    if (resp.Success && resp.Content != null)
                    {
                        JsonNode? userResponse = JsonNode.Parse(resp.Content);
                        if (userResponse is not null)
                        {
                            UserDTO user = new()
                            {
                                Id = userResponse["id"]?.GetValue<int>() ?? 0,
                                Name = userResponse["name"]?.GetValue<string>(),
                                Email = userResponse["email"]?.GetValue<string>(),
                                Token = newToken,
                                RefreshToken = refreshToken
                            };

                            UserDTO? actualUser = await userRepo.GetAsync();

                            if (actualUser != null)
                            {
                                if (actualUser.Id == user.Id)
                                    await userRepo.UpdateAsync(user);
                                else
                                {
                                    await buildDbService.CleanLocalDatabaseAsync();
                                    await userRepo.AddAsync(user);
                                }
                            }
                            else
                                await userRepo.AddAsync(user);

                            return new ServiceResp(true, user);
                        }
                    }
                }
            }
            else if (!apiresp.Success && apiresp.Content is not null && apiresp.Content.Contains("vinculado"))
                return new ServiceResp(false, ErrorTypes.GoogleAuthEmailLinkedToPassword);
            else
                return new ServiceResp(false, ErrorTypes.ServerUnavaliable);

            return new ServiceResp(false, ErrorTypes.Unknown);
        }

        public async Task<ServiceResp> SignUpAsync(string name, string email, string password)
        {
            email = email.ToLower();

            var resp = await userApiRepo.SignUpAsync(name, email, password);

            if (resp.Success && resp.Content is not null)
            {
                JsonNode? jResp = JsonNode.Parse(resp.Content);
                if (jResp is not null)
                {
                    UserDTO user = new()
                    {
                        Id = jResp["id"]?.GetValue<int>() ?? 0,
                        Name = jResp["name"]?.GetValue<string>(),
                        Email = jResp["email"]?.GetValue<string>()
                    };

                    if (user.Id is not 0)
                        return new ServiceResp(true, user);
                }

                return new ServiceResp(false, ErrorTypes.Unknown);
            }

            if (!resp.Success && resp.Content is not null && resp.Content.Contains("already exists"))
                return new ServiceResp(false, ErrorTypes.EmailAlreadyExists);

            return new ServiceResp(false, ErrorTypes.ServerUnavaliable);
        }

        public async Task UpdateLastUpdate(int uid) => await userRepo.UpdateLastUpdateAsync(DateTime.Now, uid);

        public async Task UpdateIncludePreviousBalanceAsync(bool value, int uid)
            => await userRepo.UpdateIncludePreviousBalanceAsync(value, uid);

        public async Task<(bool success, string? message)> RecoverPassword(string email)
        {
            email = email.ToLower();
            ApiResp? resp = await userApiRepo.RecoverPasswordAsync(email);

            if (resp is null)
                return (false, "Resposta nula da API");

            if (!resp.Success)
                return (false, $"API retornou erro: {resp.Error?.ToString() ?? "desconhecido"}, Content: {resp.Content ?? "null"}");

            if (resp.Content is null)
                return (false, "API retornou sucesso mas Content é null");

            JsonNode? jResp = JsonNode.Parse(resp.Content);
            if (jResp is null)
                return (false, $"Não foi possível fazer parse do JSON: {resp.Content}");

            string? mensagem = jResp["Mensagem"]?.GetValue<string>();
            if (mensagem is null)
                return (false, $"Campo 'Mensagem' não encontrado no JSON: {resp.Content}");

            return (true, mensagem);
        }
    }
}
