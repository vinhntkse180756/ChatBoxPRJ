using ChatBoxPRJ.Business.Domain;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;

namespace ChatBoxPRJ.Business.Services;

public sealed class CourseService(ICourseRepository courses) : ICourseService
{
    private static CourseDto Map(Course x) => new(x.Id, x.Code, x.Name, x.Credits, x.Description);

    public async Task<(bool Success, string Message)> CreateAsync(string code, string name, int credits, string description, CancellationToken ct = default)
    {
        code = code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name) || credits is < 1 or > 20)
            return (false, "Thông tin môn học không hợp lệ.");
        if (await courses.CodeExistsAsync(code, ct)) return (false, "Mã môn học đã tồn tại.");
        await courses.AddAsync(new Course { Code = code, Name = name.Trim(), Credits = credits, Description = description.Trim() }, ct);
        return (true, "Đã tạo môn học.");
    }

    public async Task<IReadOnlyList<CourseDto>> ListAsync(CancellationToken ct = default)
        => (await courses.ListAsync(ct)).Select(Map).ToList();

    public async Task<IReadOnlyList<CourseDto>> ListForLecturerAsync(Guid lecturerId, CancellationToken ct = default)
        => (await courses.ListForLecturerAsync(lecturerId, ct)).Select(Map).ToList();

    public Task<IReadOnlySet<Guid>> GetAssignmentsAsync(Guid lecturerId, CancellationToken ct = default)
        => courses.GetLecturerCourseIdsAsync(lecturerId, ct);

    public async Task<(bool Success, string Message)> SaveAssignmentsAsync(Guid lecturerId, IReadOnlySet<Guid> courseIds, CancellationToken ct = default)
    {
        await courses.ReplaceLecturerCoursesAsync(lecturerId, courseIds, ct);
        return (true, "Đã lưu ma trận phân quyền.");
    }

    public async Task<bool> CanManageAsync(Guid userId, UserRole role, Guid courseId, CancellationToken ct = default)
        => role == UserRole.Admin || (role == UserRole.Lecturer && (await courses.GetLecturerCourseIdsAsync(userId, ct)).Contains(courseId));
}
