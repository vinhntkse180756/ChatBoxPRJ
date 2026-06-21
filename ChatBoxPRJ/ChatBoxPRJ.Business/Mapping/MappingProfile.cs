using AutoMapper;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.DataAccess.Models;

namespace ChatBoxPRJ.Business.Mapping;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<AppUser, UserDto>();
        CreateMap<DataAccess.Models.UserRole, DTOs.UserRole>();
        CreateMap<DataAccess.Models.DocumentStatus, DTOs.DocumentStatus>();
        CreateMap<DataAccess.Models.MessageRole, DTOs.MessageRole>();
        CreateMap<DataAccess.Models.LecturerAccessLevel, DTOs.LecturerAccessLevel>();
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
