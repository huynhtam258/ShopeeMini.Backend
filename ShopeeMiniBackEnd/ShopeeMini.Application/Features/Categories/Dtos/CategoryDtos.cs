namespace ShopeeMini.Application.Features.Categories.Dtos;

public record CategoryDto(long Id, string Name, long? ParentCategoryId, DateTime CreatedAt);

public record UpsertCategoryDto(string Name, long? ParentCategoryId);
