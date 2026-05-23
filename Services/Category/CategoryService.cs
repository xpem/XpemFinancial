using Model.DTO;
using Repo;

namespace Service.Category
{
    public interface ICategoryService
    {
        Task<List<CategoryDTO>> GetAllAsync();
        Task UpsertAsync(CategoryDTO category);
    }

    public class CategoryService(ICategoryRepo categoryRepo) : ICategoryService
    {
        public async Task<List<Model.DTO.CategoryDTO>> GetAllAsync()
        {
            var all = await categoryRepo.GetAllAsync();

            // Monta um dicionário para lookup rápido de categoria principal pelo Id
            var mainById = all
                .Where(c => c.IsMainCategory)
                .ToDictionary(c => c.ExternalId, c => c.Name);

            // Ordena: agrupa subcategorias logo após sua categoria pai,
            // usando o nome da principal como chave primária de ordenação.
            return all
                .OrderBy(c => c.IsMainCategory ? c.Name : mainById.GetValueOrDefault(c.ParentExternalId ?? 0, string.Empty))
                .ThenBy(c => c.IsMainCategory ? 0 : 1)
                .ThenBy(c => c.Name)
                .ToList();
        }

        public async Task UpsertAsync(CategoryDTO category)
        {
            var existingCategory = await categoryRepo.GetByExternalIdAsync(category.ExternalId.Value);

            if (existingCategory != null)
            {
                //atualiza local somente se externo for mais recente
                if (existingCategory.UpdatedAt < category.UpdatedAt)
                {
                    category.Id = existingCategory.Id;
                    await categoryRepo.UpdateAsync(category);
                }
            }
            else
            {
                await categoryRepo.AddAsync(category);
            }
        }
    }
}
