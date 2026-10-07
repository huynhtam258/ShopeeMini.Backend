using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopeeMini.Application.Features.Categories;
using ShopeeMini.Application.Features.Categories.Dtos;
using ShopeeMini.Domain.Enums;

namespace ShopeeMini.API.Controllers;

[Route("api/categories")]
public class CategoriesController : ApiControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService) => _categoryService = categoryService;

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var categories = await _categoryService.GetAllAsync(ct);
        return SuccessResponse(categories);
    }
}

[Route("api/admin/categories")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminCategoriesController : ApiControllerBase
{
    private readonly ICategoryService _categoryService;

    public AdminCategoriesController(ICategoryService categoryService) => _categoryService = categoryService;

    [HttpPost]
    public async Task<IActionResult> Create(UpsertCategoryDto request, CancellationToken ct)
    {
        var category = await _categoryService.CreateAsync(request, ct);
        return SuccessResponse(category, "Tạo danh mục thành công", StatusCodes.Status201Created);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpsertCategoryDto request, CancellationToken ct)
    {
        var category = await _categoryService.UpdateAsync(id, request, ct);
        return SuccessResponse(category, "Cập nhật danh mục thành công");
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _categoryService.DeleteAsync(id, ct);
        return SuccessResponse("Xóa danh mục thành công");
    }
}
