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

        Task<ServiceResp> SignUpAsync(string name, string email, string password);

        Task UpdateLastUpdate(int uid);

        Task<string?> RecoverPassword(string email);

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
            else if (!apiresp.Success && apiresp.Content.Contains("User/Password incorrect") || apiresp.Content.Contains("Invalid Email"))
                return new ServiceResp(false, ErrorTypes.WrongEmailOrPassword);
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

        public async Task<string?> RecoverPassword(string email)
        {
            email = email.ToLower();
            ApiResp? resp = await userApiRepo.RecoverPasswordAsync(email);

            if (resp is not null && resp.Content is not null)
            {
                JsonNode? jResp = JsonNode.Parse(resp.Content);
                if (jResp is not null)
                    return jResp["Mensagem"]?.GetValue<string>();
            }

            return null;
        }
    }
}
