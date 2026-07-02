using Model.DTO;

namespace Model.Req
{
    public class AccountReq
    {
        public int? Id { get; set; }

        public DateTime UpdatedAt { get; set; }

        public required string Name { get; set; }

        public AccountType Type { get; set; }

        public bool IncludeInGeneralBalance { get; set; } = true;

        public bool Inactive { get; set; }

        public Guid? AccountId { get; set; }
    }
}
