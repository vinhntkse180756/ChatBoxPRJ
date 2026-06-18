using ChatBoxPRJ.Business.Domain;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;

namespace ChatBoxPRJ.Business.Services;

public sealed class AccountService(IUserRepository users, IPasswordHasher hasher) : IAccountService
{
    public Task<(bool Success, string Message)> RegisterStudentAsync(string code, string fullName, string email, string password, CancellationToken ct = default)
        => CreateAsync(code, fullName, email, password, UserRole.Student, ct);

    public Task<(bool Success, string Message)> CreateLecturerAsync(string code, string fullName, string email, string password, CancellationToken ct = default)
        => CreateAsync(code, fullName, email, password, UserRole.Lecturer, ct);

    private async Task<(bool, string)> CreateAsync(string code, string fullName, string email, string password, UserRole role, CancellationToken ct)
    {
        code = code.Trim().ToUpperInvariant();
        email = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || password.Length < 6)
            return (false, "Vui lòng nhập đủ thông tin; mật khẩu tối thiểu 6 ký tự.");
        if (await users.CodeOrEmailExistsAsync(code, email, ct))
            return (false, "Mã định danh hoặc email đã tồn tại.");
        await users.AddAsync(new AppUser { Code = code, FullName = fullName.Trim(), Email = email, PasswordHash = hasher.Hash(password), Role = role }, ct);
        return (true, role == UserRole.Student ? "Đăng ký thành công." : "Đã cấp tài khoản giảng viên.");
    }

    public async Task<UserDto?> AuthenticateAsync(string login, string password, CancellationToken ct = default)
    {
        var user = await users.FindByLoginAsync(login.Trim(), ct);
        return user is not null && hasher.Verify(password, user.PasswordHash)
            ? new UserDto(user.Id, user.Code, user.FullName, user.Email, user.Role) : null;
    }

    public async Task<IReadOnlyList<UserDto>> ListLecturersAsync(CancellationToken ct = default)
        => (await users.ListLecturersAsync(ct)).Select(x => new UserDto(x.Id, x.Code, x.FullName, x.Email, x.Role)).ToList();
}
