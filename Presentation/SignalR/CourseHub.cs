using Microsoft.AspNetCore.SignalR;

namespace Presentation.SignalR;

/// <summary>
/// Hub phát sóng sự kiện môn học (tạo/sửa/xóa) tới tất cả client đang online.
/// Không yêu cầu xác thực để Student không cần cookie trước khi negotiate.
/// </summary>
public sealed class CourseHub : Hub;
