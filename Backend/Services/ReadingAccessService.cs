using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ServerCSharp.Data;
using ServerCSharp.Models;

namespace ServerCSharp.Services
{
    public static class ReadingAccessService
    {
        public static bool IsVisible(Chapter chapter) =>
            chapter.IsPublished && (!chapter.PublishAt.HasValue || chapter.PublishAt.Value <= DateTime.UtcNow);

        public static bool IsFree(Story story, Chapter chapter) =>
            story.AccessType == "free" || chapter.IsFree;

        public static async Task<bool> CanReadAsync(AppDbContext db, Story story, Chapter chapter, int? userId, bool isAdmin = false)
        {
            if (isAdmin || (userId.HasValue && story.AuthorId == userId.Value)) return true;
            if (story.ApprovalStatus != "approved" || !IsVisible(chapter)) return false;
            if (IsFree(story, chapter)) return true;
            if (!userId.HasValue) return false;
            if (await db.CoinChapterUnlocks.AnyAsync(x => x.UserId == userId.Value && x.ChapterId == chapter.Id)) return true;
            if (await db.StoryPurchases.AnyAsync(x => x.UserId == userId.Value && x.StoryId == story.Id)) return true;
            if (!story.AllowSubscription) return false;
            if (!await ReaderMembershipService.IsVipAsync(db, userId.Value)) return false;
            var now = DateTime.UtcNow;
            return await db.UserSubscriptions.AnyAsync(x =>
                x.UserId == userId.Value && x.StartsAt <= now && x.ExpiresAt > now &&
                (x.Plan!.CoversAllPaidStories || x.Plan.IncludedStories.Any(ps => ps.StoryId == story.Id)));
        }
    }
}
