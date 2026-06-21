using AutoMapper;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.DataAccess.Interfaces;
using ChatBoxPRJ.DataAccess.Models;

namespace ChatBoxPRJ.Business.Services;

public sealed class CourseService(ICourseRepository courses, IMapper mapper) : ICourseService
{
    public async Task<(bool Success, string Message)> CreateAsync(string code, string name, int credits, string description, CancellationToken ct = default)
    {
        code = code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name) || credits is < 1 or > 20) return (false, "Thông tin môn học không hợp lệ.");
        if (await courses.CodeExistsAsync(code, ct)) return (false, "Mã môn học đã tồn tại.");
        await courses.AddAsync(new Course { Code = code, Name = name.Trim(), Credits = credits, Description = description.Trim() }, ct);
        return (true, "Đã tạo môn học.");
    }
    public async Task<(bool Success, string Message)> UpdateAsync(Guid id, string code, string name, int credits, string description, CancellationToken ct = default)
    {
        var course = await courses.FindAsync(id, ct); code = code.Trim().ToUpperInvariant();
        if (course is null) return (false, "Không tìm thấy môn học.");
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name) || credits is < 1 or > 20) return (false, "Thông tin môn học không hợp lệ.");
        if ((await courses.ListAsync(ct)).Any(x => x.Id != id && x.Code == code)) return (false, "Mã môn học đã tồn tại.");
        course.Code = code; course.Name = name.Trim(); course.Credits = credits; course.Description = description.Trim();
        await courses.UpdateAsync(course, ct); return (true, "Đã cập nhật môn học.");
    }
    public async Task<(bool Success, string Message)> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (await courses.FindAsync(id, ct) is null) return (false, "Không tìm thấy môn học.");
        var paths = await courses.DeleteAsync(id, ct); foreach (var path in paths) if (File.Exists(path)) File.Delete(path);
        return (true, "Đã xóa môn học và toàn bộ dữ liệu liên quan.");
    }
    public async Task<IReadOnlyList<CourseDto>> ListAsync(CancellationToken ct = default) => mapper.Map<IReadOnlyList<CourseDto>>(await courses.ListAsync(ct));
    public async Task<IReadOnlyList<CourseDto>> ListForLecturerAsync(Guid lecturerId, CancellationToken ct = default) => mapper.Map<IReadOnlyList<CourseDto>>(await courses.ListForLecturerAsync(lecturerId, ct));
    public Task<IReadOnlySet<Guid>> GetAssignmentsAsync(Guid lecturerId, CancellationToken ct = default) => courses.GetLecturerCourseIdsAsync(lecturerId, ct);
    public async Task<(bool Success, string Message)> SaveAssignmentsAsync(Guid lecturerId, IReadOnlySet<Guid> courseIds, CancellationToken ct = default)
    {
        try { await courses.ReplaceLecturerCoursesAsync(lecturerId, courseIds, ct); }
        catch (InvalidOperationException ex) { return (false, ex.Message); }
        return (true, "Đã lưu phân công trưởng bộ môn.");
    }
    public async Task<bool> CanManageAsync(Guid userId, UserRole role, Guid courseId, CancellationToken ct = default)
        => role == UserRole.Lecturer && await courses.GetCourseHeadAsync(courseId, ct) == userId;
}
