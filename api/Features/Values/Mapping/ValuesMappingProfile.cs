using api.Features.Values.Dtos;
using api.Models;
using AutoMapper;

namespace api.Features.Values.Mapping;

public sealed class ValuesMappingProfile : Profile
{
    public ValuesMappingProfile()
    {
        CreateMap<ValueHistory, SeriesPointResponse>()
            .ForCtorParam(nameof(SeriesPointResponse.AsOfUtc), opt => opt.MapFrom(v => v.AsOfUtc))
            .ForCtorParam(nameof(SeriesPointResponse.PriceCents), opt => opt.MapFrom(v => v.PriceCents))
            .ForCtorParam(nameof(SeriesPointResponse.Source), opt => opt.MapFrom(v => v.Source));
    }
}
