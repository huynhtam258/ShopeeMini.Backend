using Microsoft.EntityFrameworkCore;
using ShopeeMini.Application.Common.Exceptions;
using ShopeeMini.Application.Features.Categories.Dtos;
using ShopeeMini.Application.Interfaces;
using ShopeeMini.Domain.Entities;

namespace ShopeeMini.Application.Features.Categories;

public class CategoryService : ICategoryService
{
    private readonly IAppDbContext _db;

    public CategoryService(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.ParentCategoryId, c.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<CategoryDto> CreateAsync(UpsertCategoryDto request, CancellationToken ct = default)
    {
        if (request.ParentCategoryId is not null)
        {
            var parentExists = await _db.Categories.AnyAsync(c => c.Id == request.ParentCategoryId, ct);
            if (!parentExists)
            {
                throw new NotFoundException("Danh mục cha", request.ParentCategoryId);
            }
        }

        var category = new Category { Name = request.Name, ParentCategoryId = request.ParentCategoryId };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);

        return new CategoryDto(category.Id, category.Name, category.ParentCategoryId, category.CreatedAt);
    }

    public async Task<CategoryDto> UpdateAsync(long id, UpsertCategoryDto request, CancellationToken ct = default)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Danh mục", id);

        category.Name = request.Name;
        category.ParentCategoryId = request.ParentCategoryId;
        category.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new CategoryDto(category.Id, category.Name, category.ParentCategoryId, category.CreatedAt);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Danh mục", id);

        var hasProducts = await _db.Products.AnyAsync(p => p.CategoryId == id, ct);
        if (hasProducts)
        {
            throw new ConflictException("Không thể xóa danh mục đang có sản phẩm.");
        }

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync(ct);
    }
}
