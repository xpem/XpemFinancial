using Model.DTO;

namespace Model.Resp.Api
{
    public class AccountApiRes
    {
        public int Id { get; set; }

        public required string Name { get; set; }

        public AccountType Type { get; set; }

        public decimal CurrentBalance { get; set; }

        public bool IncludeInGeneralBalance { get; set; }

        public bool Inactive { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
