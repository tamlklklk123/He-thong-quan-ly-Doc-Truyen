using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using ServerCSharp.Data;
using ServerCSharp.Services;
using ServerCSharp.Models;

namespace ServerCSharp.Controllers
{
    [ApiController]
    [Route("api/billing")]
    [Tags("Billing - Gói đọc, mua truyện, lịch sử giao dịch")]
    public class BillingApiController : ControllerBase
    {
        private readonly AppDbContext _db;
        public BillingApiController(AppDbContext db) => _db = db;
        private int CurrentUserId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        [HttpGet("plans")]
        public async Task<IActionResult> Plans() => Ok(await _db.SubscriptionPlans
            .Where(p => p.IsActive).OrderBy(p => p.Price)
            .Select(p => new { p.Id, p.Name, p.Price, p.DurationDays, p.CoversAllPaidStories })
            .ToListAsync());

        public class PlaceOrderRequest { public string Type { get; set; } = ""; public int TargetId { get; set; } }
        [Authorize]
        [HttpPost("orders")]
        public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request)
        {
            try
            {
                var order = await BillingService.CreateOrderAsync(_db, CurrentUserId, request.Type, request.TargetId);
                return Ok(new { order.Id, order.OrderCode, order.Type, order.Amount, order.CoinAmount, order.Status,
                    message = "Đã tạo đơn chờ xác nhận. KHÔNG tự động thanh toán/cấp quyền." });
            }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqlException sql && sql.Number == 547 &&
                sql.Message.Contains("CK_PaymentTransactions_Type", StringComparison.Ordinal))
            {
                return StatusCode(503, new { message = "CSDL chưa hỗ trợ đơn nạp Xu. Chạy SQL/fix_payment_transaction_type.sql rồi thử lại." });
            }
        }
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> MyBilling()
        {
            var uid = CurrentUserId;
            var orders = await _db.PaymentTransactions.AsNoTracking().Where(x => x.UserId == uid)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new { x.Id, x.OrderCode, x.Type, x.Amount, x.CoinAmount, x.Status, x.PlanId, x.StoryId, x.CreatedAt, x.PaidAt })
                .ToListAsync();
            var now = DateTime.UtcNow;
            var subscriptions = await _db.UserSubscriptions.AsNoTracking()
                .Where(x => x.UserId == uid && x.ExpiresAt > now).OrderByDescending(x => x.ExpiresAt)
                .Select(x => new { x.PlanId, PlanName = x.Plan!.Name, x.StartsAt, x.ExpiresAt }).ToListAsync();
            var purchasedStories = await _db.StoryPurchases.AsNoTracking().Where(x => x.UserId == uid)
                .Select(x => new { x.StoryId, StoryName = x.Story!.Title, x.PurchasedAt }).ToListAsync();
            var coinBalance = await _db.Users.Where(x => x.Id == uid).Select(x => x.CoinBalance).FirstOrDefaultAsync();
            var isVip = await ServerCSharp.Services.ReaderMembershipService.IsVipAsync(_db, uid);
            return Ok(new { orders, subscriptions, purchasedStories, coinBalance, isVip });
        }
        [Authorize]
        [HttpGet("access/{chapterId}")]
        public async Task<IActionResult> Access(int chapterId)
        {
            var ch = await _db.Chapters.Include(x => x.Story).FirstOrDefaultAsync(x => x.Id == chapterId);
            if (ch?.Story == null) return NotFound();
            var permitted = await ReadingAccessService.CanReadAsync(_db, ch.Story, ch, CurrentUserId, User.IsInRole("admin"));
            return Ok(new { permitted, requiresPayment = !ReadingAccessService.IsFree(ch.Story, ch) });
        }

        [Authorize(Roles = "admin")]
        [HttpGet("admin/orders")]
        public async Task<IActionResult> AdminOrders([FromQuery] string? status) => Ok(await _db.PaymentTransactions.AsNoTracking()
            .Where(x => status == null || x.Status == status).OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, x.OrderCode, x.UserId, x.Type, x.PlanId, x.StoryId, x.Amount,
                x.Status, x.CreatedAt, x.PaidAt, x.ConfirmedByAdminId }).Take(500).ToListAsync());

        public class DecisionRequest { public bool Success { get; set; } public string? Note { get; set; } }
        [Authorize(Roles = "admin")]
        [HttpPost("admin/orders/{id}/decision")]
        public async Task<IActionResult> Decide(int id, [FromBody] DecisionRequest request)
        {
            try
            {
                var order = await BillingService.SettleAsync(_db, id, CurrentUserId, request.Success, request.Note);
                return Ok(new { order.Id, order.Status, order.PaidAt });
            }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }
        [Authorize(Roles = "admin")]
        [HttpGet("admin/plans")]
        public async Task<IActionResult> AdminPlans() => Ok(await _db.SubscriptionPlans
            .Select(x => new { x.Id, x.Name, x.Price, x.DurationDays, x.IsActive, x.CoversAllPaidStories }).ToListAsync());

        public class PlanRequest
        {
            public string Name { get; set; } = "";
            public decimal Price { get; set; }
            public int DurationDays { get; set; } = 30;
            public bool IsActive { get; set; } = true;
            public bool CoversAllPaidStories { get; set; } = true;
        }
        [Authorize(Roles = "admin")]
        [HttpPost("admin/plans")]
        public async Task<IActionResult> AddPlan([FromBody] PlanRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Price < 0 || request.DurationDays < 1)
                return BadRequest(new { message = "Tên, thời hạn, giá gói không hợp lệ" });
            var plan = new SubscriptionPlan { Name = request.Name.Trim(), Price = request.Price,
                DurationDays = request.DurationDays, IsActive = request.IsActive,
                CoversAllPaidStories = request.CoversAllPaidStories };
            _db.SubscriptionPlans.Add(plan); await _db.SaveChangesAsync(); return Ok(new { plan.Id });
        }
        [Authorize(Roles = "admin")]
        [HttpPut("admin/plans/{id}")]
        public async Task<IActionResult> UpdatePlan(int id, [FromBody] PlanRequest request)
        {
            var plan = await _db.SubscriptionPlans.FindAsync(id);
            if (plan == null) return NotFound();
            if (string.IsNullOrWhiteSpace(request.Name) || request.Price < 0 || request.DurationDays < 1)
                return BadRequest();
            plan.Name = request.Name.Trim(); plan.Price = request.Price; plan.DurationDays = request.DurationDays;
            plan.IsActive = request.IsActive; plan.CoversAllPaidStories = request.CoversAllPaidStories;
            await _db.SaveChangesAsync(); return Ok(new { plan.Id });
        }
        [Authorize(Roles = "admin")]
        [HttpPut("admin/stories/{id}/policy")]
        public async Task<IActionResult> UpdateStoryPolicy(int id, [FromBody] StoryPolicyRequest request)
        {
            var story = await _db.Stories.FindAsync(id);
            if (story == null) return NotFound();
            if (!new[] { "free", "paid", "mixed" }.Contains(request.AccessType) || request.Price < 0 ||
                (request.AccessType != "free" && request.Price <= 0))
                return BadRequest(new { message = "Chính sách hoặc giá truyện không hợp lệ" });
            story.AccessType = request.AccessType; story.Price = request.AccessType == "free" ? 0 : request.Price;
            story.AllowSubscription = request.AllowSubscription; story.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(); return Ok(new { story.Id, story.AccessType, story.Price });
        }
        [Authorize(Roles = "admin")]
        [HttpPut("admin/chapters/{id}/policy")]
        public async Task<IActionResult> UpdateChapterPolicy(int id, [FromBody] ChapterPolicyRequest request)
        {
            var chapter = await _db.Chapters.FindAsync(id);
            if (chapter == null) return NotFound();
            chapter.IsFree = request.IsFree; chapter.IsPublished = request.IsPublished;
            chapter.PublishAt = request.PublishAt?.ToUniversalTime();
            await _db.SaveChangesAsync(); return Ok(new { chapter.Id, chapter.IsFree });
        }
        public class StoryPolicyRequest { public string AccessType { get; set; } = "free"; public decimal Price { get; set; } public bool AllowSubscription { get; set; } = true; }
        public class ChapterPolicyRequest { public bool IsFree { get; set; } = true; public bool IsPublished { get; set; } = true; public DateTime? PublishAt { get; set; } }
        [Authorize(Roles = "admin")]
        [HttpPost("admin/plans/{planId}/stories/{storyId}")]
        public async Task<IActionResult> AddPlanStory(int planId, int storyId)
        {
            var plan = await _db.SubscriptionPlans.FindAsync(planId);
            if (plan == null || !await _db.Stories.AnyAsync(s => s.Id == storyId)) return NotFound();
            if (!await _db.PlanStoryAccesses.AnyAsync(x => x.PlanId == planId && x.StoryId == storyId))
            { _db.PlanStoryAccesses.Add(new PlanStoryAccess { PlanId = planId, StoryId = storyId }); await _db.SaveChangesAsync(); }
            return Ok(new { planId, storyId });
        }
        [Authorize(Roles = "admin")]
        [HttpDelete("admin/plans/{planId}/stories/{storyId}")]
        public async Task<IActionResult> RemovePlanStory(int planId, int storyId)
        {
            var link = await _db.PlanStoryAccesses.FindAsync(planId, storyId);
            if (link == null) return NotFound();
            _db.PlanStoryAccesses.Remove(link); await _db.SaveChangesAsync(); return Ok();
        }
        [Authorize(Roles = "admin")]
        [HttpGet("admin/revenue")]
        public async Task<IActionResult> Revenue([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var start = from ?? DateTime.UtcNow.Date.AddDays(-30);
            var end = to ?? DateTime.UtcNow.AddDays(1);
            if (end <= start) return BadRequest();
            var rows = await _db.PaymentTransactions.Where(x => x.Status == "paid" && x.PaidAt >= start && x.PaidAt < end)
                .Select(x => new { x.Type, x.Amount, x.PlanId, x.StoryId, x.PaidAt }).ToListAsync();
            return Ok(new
            {
                totalRevenue = rows.Sum(x => x.Amount),
                subscriptionRevenue = rows.Where(x => x.Type == "plan").Sum(x => x.Amount),
                storyRevenue = rows.Where(x => x.Type == "story").Sum(x => x.Amount),
                newSubscriptions = rows.Count(x => x.Type == "plan"),
                storyPurchases = rows.Count(x => x.Type == "story"),
                byDate = rows.GroupBy(x => x.PaidAt!.Value.Date).OrderBy(x => x.Key)
                    .Select(g => new { date = g.Key, amount = g.Sum(x => x.Amount) }).ToList(),
                byPlan = rows.Where(x => x.PlanId.HasValue).GroupBy(x => x.PlanId)
                    .Select(g => new { planId = g.Key, count = g.Count(), revenue = g.Sum(x => x.Amount) }).ToList(),
                byStory = rows.Where(x => x.StoryId.HasValue).GroupBy(x => x.StoryId)
                    .Select(g => new { storyId = g.Key, count = g.Count(), revenue = g.Sum(x => x.Amount) }).ToList()
            });
        }
    }
}
