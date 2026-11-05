using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServerCSharp.Models
{
    // Chỉ quản trị viên được xác nhận thanh toán; không tự cấp quyền ở bước đặt hàng.
    public class SubscriptionPlan
    {
        public int Id { get; set; }
        [Required, MaxLength(120)] public string Name { get; set; } = string.Empty;
        [Column(TypeName = "decimal(18,2)")] public decimal Price { get; set; }
        public int DurationDays { get; set; } = 30;
        public bool IsActive { get; set; } = true;
        public bool CoversAllPaidStories { get; set; } = true;
        public List<PlanStoryAccess> IncludedStories { get; set; } = new();
    }
    public class PlanStoryAccess
    {
        public int PlanId { get; set; }
        public SubscriptionPlan? Plan { get; set; }
        public int StoryId { get; set; }
        public Story? Story { get; set; }
    }
    public class UserSubscription
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int PlanId { get; set; }
        public int PaymentTransactionId { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public User? User { get; set; }
        public SubscriptionPlan? Plan { get; set; }
    }
    public class StoryPurchase
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int StoryId { get; set; }
        public int PaymentTransactionId { get; set; }
        public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
        public User? User { get; set; }
        public Story? Story { get; set; }
    }
    public class PaymentTransaction
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        [Required, MaxLength(48)] public string OrderCode { get; set; } = string.Empty;
        [Required, MaxLength(12)] public string Type { get; set; } = string.Empty; // plan/story
        public int? PlanId { get; set; }
        public int? StoryId { get; set; }
        public int? CoinAmount { get; set; } // Chỉ dùng cho đơn nạp Xu đã được Admin xác nhận
        [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; } // Giá chốt tại lúc tạo đơn
        [MaxLength(12)] public string Status { get; set; } = "pending"; // pending/paid/failed
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }
        public int? ConfirmedByAdminId { get; set; }
        [MaxLength(500)] public string? AdminNote { get; set; }
        public User? User { get; set; }
    }
}
