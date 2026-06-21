using AutoMapper;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.DataAccess.Models;

namespace ChatBoxPRJ.Business.Mapping;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<AppUser, UserDto>();
        CreateMap<Course, CourseDto>();
        CreateMap<LearningDocument, DocumentDto>()
            .ForCtorParam(nameof(DocumentDto.CourseName), option => option.MapFrom(source => source.Course.Name))
            .ForCtorParam(nameof(DocumentDto.FileName), option => option.MapFrom(source => source.OriginalFileName));
        CreateMap<DocumentChunk, DocumentChunkDto>()
            .ForCtorParam(nameof(DocumentChunkDto.WordCount), option => option.MapFrom(source => CountWords(source.Content)));
    }

    private static int CountWords(string content) =>
        content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}
