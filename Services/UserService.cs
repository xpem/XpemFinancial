using Model.DTO;
using System;
using System.Collections.Generic;
using System.Text;

namespace Service
{
    public interface IUserService
    {
        Task AddUserAsync(UserDTO user);
        Task GetMockUserAsync();
        Task<UserDTO?> GetAsync();
    }

    public class UserService(Repo.IUserRepo UserRepo) : IUserService
    {
        public async Task AddUserAsync(Model.DTO.UserDTO user)
        {
            await UserRepo.Add(user);
        }

        public async Task<Model.DTO.UserDTO?> GetAsync()
        {
            return await UserRepo.Get();
        }

        //mock user for testing purposes
        public async Task GetMockUserAsync()
        {
            if(await UserRepo.Get() != null)
                return;

            var mockUser = new Model.DTO.UserDTO
            {
                Id = 1,
                Name = "Mock User",
                Email = "emanuel.xpe@gmail.com",
                CreatedAt = DateTime.UtcNow,
            };

            await UserRepo.Add(mockUser);
        }
    }
}
