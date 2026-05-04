using Model.DTO;
using Repo;

namespace Service
{
    public interface ICategoryService
    {
        Task<List<CategoryDTO>> GetAllAsync();
        Task MockCategories(int userId);
    }

    public class CategoryService(ICategoryRepo categoryRepo) : ICategoryService
    {
        public async Task<List<Model.DTO.CategoryDTO>> GetAllAsync()
        {
            return await categoryRepo.GetAllAsync();
        }


        public class TransactionCategories
        {
            public int Id { get; set; } = 0;

            public string Category { get; set; }

            public List<string>? Subcategories { get; set; }
        }

        public async Task MockCategories(int userId)
        {
            var _list = await categoryRepo.GetAllAsync();

            if (_list != null && _list.Count > 0) return;

            List<TransactionCategories> list =
                [
                new()
                {
                    Category = "Alimentação",
                    Subcategories = new List<string> { "Almoço", "Lanche" }
                },
                new()
                {
                    Category = "Carro",
                    Subcategories = new List<string> { "Combustível", "Estacionamento", "Lavagem", "Multas", "Pedágio" }
                   },
                   new(){
                        Category = "Casa",
                        Subcategories = new List<string> { "Aluguel", "Condomínio", "Internet", "Energia", "Manutenção", "Limpeza", "Móveis", "Utensílios" }
                   },
                new(){
                      Category = "Educação",
                      Subcategories = new List<string> { "Cursos", "Livros", "Pós-Graduação" }
                },
                new(){
                      Category = "Doações",
                      Subcategories = null
                },
                new(){
                      Category = "Eletrônicos",
                      Subcategories = null
                },
                new(){
                      Category = "Presentes",
                      Subcategories = null
                },
                new(){
                      Category = "Pessoais",
                      Subcategories = new List<string> { "Academia", "Assessório", "Celular", "Cosmético", "Roupa", "Calçado", "Servidor" }
                },
                new(){
                      Category = "Impostos",
                      Subcategories = new List<string> { "IR", "IPTU", "IPVA", "FGTS" }
                },
                new(){
                      Category = "Lazer",
                      Subcategories = new List<string> { "Streaming", "Bar", "Cinema", "Show", "Jogo", "Viagem" }
                },
                new(){
                      Category = "Outros",
                      Subcategories = null
                },
                new(){
                      Category = "Receita",
                      Subcategories = new List<string> { "13°", "Bonificação", "Comissão", "Estorno", "Férias", "Juros", "Reembolso", "Salário", "Outra" }
                },
                new(){
                      Category = "Saúde",
                      Subcategories = new List<string> { "Plano de Saúde", "Dentista", "Enxame", "Farmácia", "Médico" }
                },
                new(){
                      Category = "Seguro",
                      Subcategories = new List<string> { "Carro", "Moto", "Vida", "Residencial" }
                },
                new(){
                      Category = "Transporte",
                      Subcategories = new List<string> { "Combustível", "Estacionamento", "Lavagem", "Metrô", "Multas", "Pedágio", "Transporte por app" }
                },
                new(){
                      Category = "Investimentos",
                      Subcategories = new List<string> { "Ações", "Fundos Imobiliários", "Renda Fixa", "Criptomoedas" }
                },
               ];

            foreach (var item in list)
            {
                CategoryDTO categoryDTO = new()
                {
                    Name = item.Category,
                    CreatedAt = DateTime.UtcNow,
                    IsMainCategory = true,
                    UserId = userId
                };

                await categoryRepo.AddAsync(categoryDTO);

                foreach (var sub in item.Subcategories ?? [])
                {
                    CategoryDTO subCategoryDTO = new()
                    {
                        Name = sub,
                        CreatedAt = DateTime.UtcNow,
                        IsMainCategory = false,
                        ParentId = categoryDTO.Id,
                        UserId = userId
                    };

                    await categoryRepo.AddAsync(subCategoryDTO);
                }
            }

        }
    }
}
