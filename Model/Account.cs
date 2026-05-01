using Model.DTO;

namespace Model
{
    public class Account
    {
        public int Id { get; set; }

        public required string Name { get; set; }

        public decimal Balance { get; set; }
    }
}
