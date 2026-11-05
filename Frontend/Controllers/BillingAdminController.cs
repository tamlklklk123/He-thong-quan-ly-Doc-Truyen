using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerCSharp.Data;
using ServerCSharp.Models;
using ServerCSharp.Services;

namespace ServerCSharp.Controllers
{
    [Authorize(Roles = "admin")]
    public class BillingAdminController : Controller
    {
        private readonly AppDbContext _db;
        public BillingAdminController(AppDbContext db) => _db = db;
        public async Task<IActionResult> Index()
        {
            ViewBag.Orders = await _db.PaymentTransactions.Include(x => x.User)
                .OrderByDescending(x => x.CreatedAt).Take(150).ToListAsync();
            ViewBag.Plans = await _db.SubscriptionPlans.OrderBy(x => x.Id).ToListAsync();
            ViewBag.Stories = await _db.Stories.Include(x => x.Chapters)
                .OrderByDescending(x => x.CreatedAt).Take(150).ToListAsync();
            var paid = await _db.PaymentTransactions.Where(x => x.Status == "paid").ToListAsync();
            ViewBag.Revenue = paid.Sum(x => x.Amount);
            ViewBag.PlanRevenue = paid.Where(x => x.Type == "plan").Sum(x => x.Amount);
            ViewBag.StoryRevenue = paid.Where(x => x.Type == "story").Sum(x => x.Amount);
            ViewBag.CoinRevenue = paid.Where(x => x.Type == "coins").Sum(x => x.Amount);
            return View();
        }
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Decide(int id, bool success, string? note)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(claim, out var adminId)) return Unauthorized();
            try
            {
                var order = await BillingService.SettleAsync(_db, id, adminId, success, note);
                TempData["SuccessMessage"] = $"Đơn {order.OrderCode}: {order.Status}.";
            }
            catch (InvalidOperationException ex) { TempData["ErrorMessage"] = ex.Message; }
            return RedirectToAction("Index");
        }
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePlan(int? id, string name, decimal price, int durationDays, bool isActive = false, bool coversAllPaidStories = false)
        {
            if (string.IsNullOrWhiteSpace(name) || price < 0 || durationDays < 1)
                TempData["ErrorMessage"] = "Dữ liệu gói không hợp lệ.";
            else
            {
                var plan = id.HasValue ? await _db.SubscriptionPlans.FindAsync(id.Value) : null;
                if (id.HasValue && plan == null) return NotFound();
                if (plan == null) { plan = new SubscriptionPlan(); _db.SubscriptionPlans.Add(plan); }
                plan.Name = name.Trim(); plan.Price = price; plan.DurationDays = durationDays;
                plan.IsActive = isActive; plan.CoversAllPaidStories = coversAllPaidStories;
                await _db.SaveChangesAsync(); TempData["SuccessMessage"] = "Đã lưu gói đọc.";
            }
            return RedirectToAction("Index");
        }
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveStoryPolicy(int id, string accessType, decimal price, bool allowSubscription = false)
        {
            var story = await _db.Stories.FindAsync(id);
            if (story == null) return NotFound();
            if (!new[] { "free", "paid", "mixed" }.Contains(accessType) || price < 0 || (accessType != "free" && price <= 0))
                TempData["ErrorMessage"] = "Vui lòng chọn chính sách và nhập giá hợp lệ.";
            else
            {
                story.AccessType = accessType; story.Price = accessType == "free" ? 0 : price;
                story.AllowSubscription = allowSubscription; story.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(); TempData["SuccessMessage"] = "Đã cập nhật chính sách truyện.";
            }
            return RedirectToAction("Index");
        }
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveChapterPolicy(int id, bool isFree = false, bool isPublished = false)
        {
            var ch = await _db.Chapters.FindAsync(id);
            if (ch == null) return NotFound();
            ch.IsFree = isFree; ch.IsPublished = isPublished;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã cập nhật trạng thái chương.";
            return RedirectToAction("Index");
        }
    }
}
