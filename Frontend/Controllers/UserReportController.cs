using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using ServerCSharp.Data;
using ServerCSharp.Models;
using ServerCSharp.Hubs;
namespace ServerCSharp.Controllers {
[Authorize] public class UserReportController : Controller {
 private readonly AppDbContext db; private readonly IHubContext<RealtimeHub> hub;
 public UserReportController(AppDbContext db,IHubContext<RealtimeHub> hub){this.db=db;this.hub=hub;}
 [HttpGet] public IActionResult Create()=>View();
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Create(string reason,string details,string? pageUrl){
 if(!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value,out var uid)) return Challenge();
 if(string.IsNullOrWhiteSpace(reason)||reason.Length>250||string.IsNullOrWhiteSpace(details)||details.Length>2000){ModelState.AddModelError("","Vui lòng nhập loại lỗi và mô tả (tối đa 2000 ký tự).");return View();}
 var cutoff=DateTime.UtcNow.AddMinutes(-3);if(await db.Reports.AnyAsync(x=>x.UserId==uid&&x.TargetType=="bug"&&x.CreatedAt>=cutoff)){TempData["InfoMessage"]="Bạn vừa gửi báo lỗi. Vui lòng chờ vài phút.";return RedirectToAction("Create");}
 var r=new Report{UserId=uid,TargetType="bug",TargetId=0,Reason=reason.Trim(),Details=details.Trim()+(string.IsNullOrWhiteSpace(pageUrl)?"":"\nTrang gặp lỗi: "+pageUrl.Trim().Substring(0,Math.Min(pageUrl.Trim().Length,300))),Status="pending"};
 db.Reports.Add(r);await db.SaveChangesAsync();
 var admins=await db.Users.Where(x=>x.Role=="admin").Select(x=>x.Id).ToListAsync();
 foreach(var id in admins)db.Notifications.Add(new Notification{UserId=id,Type="report",Title="Báo lỗi mới #"+r.Id,Message="Người dùng đã gửi báo lỗi: "+r.Reason,ActionUrl="/Admin?tab=reportsTab"});
 db.Notifications.Add(new Notification{UserId=uid,Type="report",Title="Đã tiếp nhận báo lỗi #"+r.Id,Message="Báo lỗi đang chờ Admin kiểm tra.",ActionUrl="/UserReport/Create"});
 await db.SaveChangesAsync();
 TempData["SuccessMessage"]="Đã gửi báo lỗi #"+r.Id+" đến Admin.";return RedirectToAction("Create");
 }
}
}
