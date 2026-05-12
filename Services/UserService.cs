using ApiRepo;
using ApiRepo.Handlers;
using Model.DTO;
using Model.Resp;
using Model.Resp.Api;
using System.Text.Json.Nodes;

namespace Service
{
    public interface IUserService
    {
        Task AddUserAsync(UserDTO user);
        Task<UserDTO?> GetAsync();
        Task<ServiceResp> SignInAsync(string email, string password);
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
            try
            {
                email = email.ToLower();

                var apiresp = await userApiRepo.GetTokenAsync(email, password);

                if (apiresp.Success && apiresp.Content is not null and string newToken)
                {
                    ApiResp resp = await userApiRepo.GetAsync(newToken);

                    if (resp.Success && resp.Content != null)
                    {
                        JsonNode? userResponse = JsonNode.Parse(resp.Content);
                        if (userResponse is not null)
                        {
                            UserDTO? user = new()
                            {
                                Id = userResponse["id"]?.GetValue<int>() ?? 0,
                                Name = userResponse["name"]?.GetValue<string>(),
                                Email = userResponse["email"]?.GetValue<string>(),
                                Token = newToken,
                                Password = EncryptionHandler.Encrypt(password)
                            };

                            UserDTO? actualUser = await userRepo.GetAsync();

                            //resign 
                            if (actualUser != null)
                            {
                                //with the same user
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
                else if (!apiresp.Success && apiresp.Content is not null && apiresp.Content is "User/Password incorrect" or "Invalid Email")
                    return new ServiceResp(false, ErrorTypes.WrongEmailOrPassword);
                else return new ServiceResp(false, ErrorTypes.ServerUnavaliable);

                return new ServiceResp(false, ErrorTypes.Unknown);
            }
            catch { throw; }
        }
    }
}
