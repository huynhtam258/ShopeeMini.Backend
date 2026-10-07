using ShopeeMini.Application.Features.Categories.Dtos;

namespace ShopeeMini.Application.Features.Categories;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(UpsertCategoryDto request, CancellationToken ct = default);
    Task<CategoryDto> UpdateAsync(long id, UpsertCategoryDto request, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}
