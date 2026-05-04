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

    public class UserService(Repo.IUserRepo userRepo) : IUserService
    {
        public async Task AddUserAsync(UserDTO user)
        {
            await userRepo.AddAsync(user);
        }

        public async Task<UserDTO?> GetAsync()
        {
            return await userRepo.GetAsync();
        }

        //mock user for testing purposes
        public async Task GetMockUserAsync()
        {
            if(await userRepo.GetAsync() != null)
                return;

            var mockUser = new Model.DTO.UserDTO
            {
                Name = "Mock User",
                Email = "emanuel.xpe@gmail.com",
                CreatedAt = DateTime.UtcNow,
            };

            await userRepo.AddAsync(mockUser);
        }
    }
}
