using Model.DTO;

namespace Service
{
    public interface IUserSessionService
    {
        Task<UserDTO?> GetCurrentUserAsync();
        void Invalidate();
    }

    public class UserSessionService(IUserService userService) : IUserSessionService
    {
        private UserDTO? _cachedUser;

        public async Task<UserDTO?> GetCurrentUserAsync()
        {
            _cachedUser ??= await userService.GetAsync();
            return _cachedUser;
        }

        public void Invalidate() => _cachedUser = null;
    }
}