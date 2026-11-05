using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerCSharp.Data;
using ServerCSharp.Services;

namespace ServerCSharp.Controllers
{
    [Authorize]
    public class CoinChaptersController : Controller
    {
        private readonly AppDbContext _db;
        public CoinChaptersController(AppDbContext db) => _db = db;

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(int chapterId, bool useTicket = false)
        {
            if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId)) return Unauthorized();
            try
            {
                TempData["SuccessMessage"] = await ReaderPointsService.UnlockAsync(_db, userId, chapterId, useTicket);
                return RedirectToAction("Read", "Reader", new { id = chapterId });
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index", "Billing", new { chapterId });
            }
        }
    }
}
