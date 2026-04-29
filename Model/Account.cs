namespace Model
{
    public class Account
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public decimal Balance { get; set; }
    }

    public class MockAccount
    {
        public static List<Account> GetMockAccounts()
        {
            return new List<Account>
            {
                new Account { Id = 1, Name = "Banco", Balance = 1500.00m },
                new Account { Id = 2, Name = "Carteira", Balance = 0m },
                new Account { Id = 3, Name = "Cartão de Crédito", Balance = 0.00m }
            };
        }
    }
}
