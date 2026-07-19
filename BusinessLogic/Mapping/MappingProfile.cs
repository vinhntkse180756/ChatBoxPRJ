using AutoMapper;
using BusinessLogic.DTOs;
using BusinessObjects.Entities;

namespace BusinessLogic.Mapping;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<AppUser, UserDto>();
        CreateMap<BusinessObjects.Entities.UserRole, DTOs.UserRole>();
        CreateMap<BusinessObjects.Entities.DocumentStatus, DTOs.DocumentStatus>();
        CreateMap<BusinessObjects.Entities.MessageRole, DTOs.MessageRole>();
        CreateMap<BusinessObjects.Entities.LecturerAccessLevel, DTOs.LecturerAccessLevel>();
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
