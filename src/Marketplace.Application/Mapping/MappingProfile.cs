using AutoMapper;
using Marketplace.Application.DTO;
using Marketplace.Core.Entities;

namespace Marketplace.Application.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Category, CategoryResponse>();
    }
}
