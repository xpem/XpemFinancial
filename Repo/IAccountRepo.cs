using Model.DTO;

namespace Repo
{
    public interface IAccountRepo
    {
        Task Add(AccountDTO account);
        Task<AccountDTO?> GetAsync();
    }
}