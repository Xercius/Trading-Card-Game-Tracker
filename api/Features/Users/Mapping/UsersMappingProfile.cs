using AutoMapper;
using api.Features.Users.Dtos;
using api.Models;

namespace api.Features.Users.Mapping;

public sealed class UsersMappingProfile : Profile
{
    public UsersMappingProfile()
    {
        CreateMap<User, UserResponse>();

        CreateMap<CreateUserRequest, User>()
            .ForMember(u => u.Username, opt => opt.MapFrom(src => src.Username.Trim()))
            .ForMember(u => u.DisplayName, opt => opt.MapFrom(src => src.DisplayName.Trim()))
            .ForMember(u => u.IsAdmin, opt => opt.MapFrom(src => src.IsAdmin));

        CreateMap<UpdateUserRequest, User>()
            .ForMember(u => u.Username, opt => opt.MapFrom(src => src.Username.Trim()))
            .ForMember(u => u.DisplayName, opt => opt.MapFrom(src => src.DisplayName.Trim()))
            .ForMember(u => u.IsAdmin, opt => opt.MapFrom(src => src.IsAdmin));
    }
}
