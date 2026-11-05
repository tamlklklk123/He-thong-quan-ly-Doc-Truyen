using System;
using ServerCSharp.Models;
namespace ServerCSharp.Services
{
    // Common moderation rules across MVC and API. Time comparisons always UTC.
    public static class UserModeration
    {
        public static string EffectiveStatus(User? user)
        {
            if (user == null) return "banned";
            if ((user.Status == "restricted" || user.Status == "suspended")
                && user.StatusUntil.HasValue && user.StatusUntil.Value <= DateTime.UtcNow)
                return "active";
            return user.Status;
        }
        public static bool IsLoginBlocked(User? user)
        {
            var status = EffectiveStatus(user);
            return status == "banned" || status == "suspended";
        }
        public static bool CanParticipate(User? user) => EffectiveStatus(user) == "active";
        public static string StatusMessage(User? user)
        {
            if (user == null) return "Không tìm thấy tài khoản.";
            var status = EffectiveStatus(user);
            if (status == "restricted") return "Tài khoản đang bị hạn chế tương tác" + Until(user) + ". " + user.ModerationReason;
            if (status == "suspended") return "Tài khoản đang bị tạm khóa" + Until(user) + ". " + user.ModerationReason;
            if (status == "banned") return "Tài khoản đã bị khóa. " + user.ModerationReason;
            return "";
        }
        private static string Until(User user) => user.StatusUntil.HasValue
            ? " đến " + user.StatusUntil.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "";
    }
}
