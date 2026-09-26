namespace Marketplace.Application.DTO;

public record CategoryCreateRequest(string Name, Guid? ParentId);
public record CategoryUpdateRequest(string Name, Guid? ParentId);
public record CategoryResponse(Guid Id, string Name, string Slug, Guid? ParentId);
