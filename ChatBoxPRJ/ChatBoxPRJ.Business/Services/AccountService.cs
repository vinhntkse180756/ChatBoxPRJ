using AutoMapper;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.DataAccess.Interfaces;
using ChatBoxPRJ.DataAccess.Models;
using DataUserRole = ChatBoxPRJ.DataAccess.Models.UserRole;

namespace ChatBoxPRJ.Business.Services;

public sealed class AccountService(IUserRepository users, IPasswordHasher hasher, IMapper mapper) : IAccountService
{
    public Task<(bool Success, string Message)> RegisterStudentAsync(string code, string fullName, string email, string password, CancellationToken ct = default)
        => CreateAsync(code, fullName, email, password, DataUserRole.Student, ct);

    public Task<(bool Success, string Message)> CreateLecturerAsync(string code, string fullName, string email, string password, CancellationToken ct = default)
        => CreateAsync(code, fullName, email, password, DataUserRole.Lecturer, ct);

    private async Task<(bool, string)> CreateAsync(string code, string fullName, string email, string password, DataUserRole role, CancellationToken ct)
    {
        code = code.Trim().ToUpperInvariant();
        email = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || password.Length < 6)
            return (false, "Vui lòng nhập đủ thông tin; mật khẩu tối thiểu 6 ký tự.");
        if (await users.CodeOrEmailExistsAsync(code, email, ct))
            return (false, "Mã định danh hoặc email đã tồn tại.");
        await users.AddAsync(new AppUser { Code = code, FullName = fullName.Trim(), Email = email, PasswordHash = hasher.Hash(password), Role = role }, ct);
        return (true, role == DataUserRole.Student ? "Đăng ký thành công." : "Đã cấp tài khoản giảng viên.");
    }

    public async Task<UserDto?> AuthenticateAsync(string login, string password, CancellationToken ct = default)
    {
        var user = await users.FindByLoginAsync(login.Trim(), ct);
        return user is not null && hasher.Verify(password, user.PasswordHash)
            ? mapper.Map<UserDto>(user) : null;
    }

    public async Task<IReadOnlyList<UserDto>> ListLecturersAsync(CancellationToken ct = default)
        => mapper.Map<IReadOnlyList<UserDto>>(await users.ListLecturersAsync(ct));

    public async Task<(bool Success, string Message)> UpdateLecturerAsync(Guid id, string code, string fullName, string email, string? password, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(id, ct); code = code.Trim().ToUpperInvariant(); email = email.Trim().ToLowerInvariant();
        if (user is null || user.Role != DataUserRole.Lecturer) return (false, "Không tìm thấy giảng viên.");
        if ((await users.ListLecturersAsync(ct)).Any(x => x.Id != id && (x.Code == code || x.Email == email))) return (false, "Mã giảng viên hoặc email đã tồn tại.");
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email)) return (false, "Thông tin không hợp lệ.");
        user.Code = code; user.FullName = fullName.Trim(); user.Email = email;
        if (!string.IsNullOrWhiteSpace(password)) { if (password.Length < 6) return (false, "Mật khẩu tối thiểu 6 ký tự."); user.PasswordHash = hasher.Hash(password); }
        await users.UpdateAsync(user, ct); return (true, "Đã cập nhật giảng viên.");
    }

    public async Task<(bool Success, string Message)> DeleteLecturerAsync(Guid id, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(id, ct);
        if (user is null || user.Role != DataUserRole.Lecturer) return (false, "Không tìm thấy giảng viên.");
        var paths = (await users.DeleteUserAsync(id, ct)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var failedPaths = new List<string>();
        foreach (var path in paths)
        {
            if (!await TryDeleteFileAsync(path, ct)) failedPaths.Add(path);
        }

        if (failedPaths.Count > 0)
            return (false, $"Đã xóa tài khoản và dữ liệu, nhưng không thể xóa {failedPaths.Count}/{paths.Count} file vật lý. Vui lòng kiểm tra quyền truy cập thư mục upload.");
        return (true, $"Đã xóa giảng viên, phân công và toàn bộ {paths.Count} file do người này tải lên.");
    }

    private static async Task<bool> TryDeleteFileAsync(string path, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                return !File.Exists(path);
            }
            catch (IOException) when (attempt < 2)
            {
                await Task.Delay(150, ct);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
        return false;
    }
}
