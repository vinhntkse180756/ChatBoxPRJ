using AutoMapper;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.DataAccess.Interfaces;
using ChatBoxPRJ.DataAccess.Models;
using BusinessAccessLevel = ChatBoxPRJ.Business.DTOs.LecturerAccessLevel;
using BusinessUserRole = ChatBoxPRJ.Business.DTOs.UserRole;
using DataAccessLevel = ChatBoxPRJ.DataAccess.Models.LecturerAccessLevel;
using DataUserRole = ChatBoxPRJ.DataAccess.Models.UserRole;

namespace ChatBoxPRJ.Business.Services;

public sealed class CourseService(ICourseRepository courses, IUserRepository users, IMapper mapper, ICourseStatusNotifier notifier) : ICourseService
{
    public async Task<(bool Success, string Message)> CreateAsync(string code, string name, int credits, string description, CancellationToken ct = default)
    {
        code = code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name) || credits is < 1 or > 20) return (false, "Thông tin môn học không hợp lệ.");
        if (await courses.CodeExistsAsync(code, ct)) return (false, "Mã môn học đã tồn tại.");
        var course = new Course { Code = code, Name = name.Trim(), Credits = credits, Description = description.Trim() };
        await courses.AddAsync(course, ct);
        
        var dto = mapper.Map<CourseDto>(course);
        await notifier.NotifyCourseCreatedAsync(dto, ct);

        return (true, "Đã tạo môn học.");
    }
    public async Task<(bool Success, string Message)> UpdateAsync(Guid id, string code, string name, int credits, string description, CancellationToken ct = default)
    {
        var course = await courses.FindAsync(id, ct); code = code.Trim().ToUpperInvariant();
        if (course is null) return (false, "Không tìm thấy môn học.");
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name) || credits is < 1 or > 20) return (false, "Thông tin môn học không hợp lệ.");
        if ((await courses.ListAsync(ct)).Any(x => x.Id != id && x.Code == code)) return (false, "Mã môn học đã tồn tại.");
        course.Code = code; course.Name = name.Trim(); course.Credits = credits; course.Description = description.Trim();
        await courses.UpdateAsync(course, ct);
        
        var dto = mapper.Map<CourseDto>(course);
        await notifier.NotifyCourseUpdatedAsync(dto, ct);

        return (true, "Đã cập nhật môn học.");
    }
    public async Task<(bool Success, string Message)> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (await courses.FindAsync(id, ct) is null) return (false, "Không tìm thấy môn học.");
        var paths = await courses.DeleteAsync(id, ct); foreach (var path in paths) if (File.Exists(path)) File.Delete(path);
        
        await notifier.NotifyCourseDeletedAsync(id, ct);

        return (true, "Đã xóa môn học và toàn bộ dữ liệu liên quan.");
    }
    public async Task<IReadOnlyList<CourseDto>> ListAsync(CancellationToken ct = default) => mapper.Map<IReadOnlyList<CourseDto>>(await courses.ListAsync(ct));
    public async Task<IReadOnlyList<CourseDto>> ListForLecturerAsync(Guid lecturerId, CancellationToken ct = default) => mapper.Map<IReadOnlyList<CourseDto>>(await courses.ListForLecturerAsync(lecturerId, ct));
    public async Task<IReadOnlyDictionary<Guid, BusinessAccessLevel>> GetAssignmentsAsync(Guid lecturerId, CancellationToken ct = default)
        => (await courses.GetLecturerAssignmentsAsync(lecturerId, ct))
            .ToDictionary(x => x.Key, x => (BusinessAccessLevel)x.Value);
    public async Task<(bool Success, string Message)> SaveAssignmentsAsync(Guid lecturerId, IReadOnlyDictionary<Guid, BusinessAccessLevel> assignments, CancellationToken ct = default)
    {
        if (assignments.Values.Any(x => !Enum.IsDefined(x))) return (false, "Cấp quyền môn học không hợp lệ.");
        var lecturer = await users.FindByIdAsync(lecturerId, ct);
        if (lecturer is null || lecturer.Role != DataUserRole.Lecturer)
            return (false, "Không tìm thấy giảng viên.");

        var assignedCourseIds = assignments.Keys.ToHashSet();
        var existingCourseIds = (await courses.ListAsync(ct)).Select(x => x.Id).ToHashSet();
        if (!assignedCourseIds.IsSubsetOf(existingCourseIds))
            return (false, "Một hoặc nhiều môn học không tồn tại.");

        var dataAssignments = assignments.ToDictionary(x => x.Key, x => (DataAccessLevel)x.Value);
        var courseHeadIds = dataAssignments
            .Where(x => x.Value == DataAccessLevel.CourseHead)
            .Select(x => x.Key)
            .ToList();
        if (courseHeadIds.Count > 0
            && (await courses.ListCourseHeadCourseIdsAsync(courseHeadIds, lecturerId, ct)).Count > 0)
            return (false, "Một hoặc nhiều môn đã có trưởng bộ môn.");

        await courses.ReplaceLecturerCoursesAsync(lecturerId, dataAssignments, ct);
        return (true, "Đã lưu phân quyền môn học cho giảng viên.");
    }

    public async Task<BusinessAccessLevel?> GetLecturerAccessLevelAsync(Guid lecturerId, Guid courseId, CancellationToken ct = default)
    {
        var access = await courses.GetLecturerAccessLevelAsync(lecturerId, courseId, ct);
        return access.HasValue ? (BusinessAccessLevel)access.Value : null;
    }

    public async Task<bool> CanAccessAsync(Guid userId, BusinessUserRole role, Guid courseId, CancellationToken ct = default)
        => role == BusinessUserRole.Student
            || role == BusinessUserRole.Lecturer && await courses.GetLecturerAccessLevelAsync(userId, courseId, ct) is not null;

    public async Task<bool> CanManageAsync(Guid userId, BusinessUserRole role, Guid courseId, CancellationToken ct = default)
        => role == BusinessUserRole.Lecturer
            && await courses.GetLecturerAccessLevelAsync(userId, courseId, ct) == DataAccessLevel.CourseHead;
}
