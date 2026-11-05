using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using BCrypt.Net;
using ServerCSharp.Models;

namespace ServerCSharp.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            context.Database.EnsureCreated();
            // Với DB tồn tại trước đây, chạy SQL/upgrade_case_study.sql trước khi khởi động.
            if (!context.SubscriptionPlans.Any())
            {
                context.SubscriptionPlans.Add(new SubscriptionPlan
                {
                    Name = "Gói đọc 30 ngày", DurationDays = 30, Price = 49000,
                    IsActive = true, CoversAllPaidStories = true
                });
                context.SaveChanges();
            }

            if (context.Users.Any())
            {
                SeedTruyenQQData(context);
                return; // Database has been seeded
            }

            // 1. Users
            var admin = new User
            {
                Name = "Quản Trị Viên Hệ Thống",
                Email = "admin@doctruyen.vn",
                Password = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = "admin",
                Status = "active",
                Avatar = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=150&auto=format&fit=crop&q=80",
                Bio = "Quản trị viên trưởng phụ trách nội dung và kiểm duyệt hệ thống."
            };

            var author1 = new User
            {
                Name = "Thiên Tằm Thổ Đậu",
                Email = "tacgia@doctruyen.vn",
                Password = BCrypt.Net.BCrypt.HashPassword("author123"),
                Role = "author",
                Status = "active",
                Avatar = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=150&auto=format&fit=crop&q=80",
                Bio = "Tác giả chuyên sáng tác các bộ truyện Tiên Hiệp, Huyền Huyễn kỳ ảo."
            };

            var author2 = new User
            {
                Name = "Mặc Hương Đồng Khứu",
                Email = "tacgia2@doctruyen.vn",
                Password = BCrypt.Net.BCrypt.HashPassword("author123"),
                Role = "author",
                Status = "active",
                Avatar = "https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=150&auto=format&fit=crop&q=80",
                Bio = "Cây bút truyện phiêu lưu huyền ảo và tình cảm sâu sắc."
            };

            var reader1 = new User
            {
                Name = "Nguyễn Văn Đọc",
                Email = "docgia@doctruyen.vn",
                Password = BCrypt.Net.BCrypt.HashPassword("reader123"),
                Role = "reader",
                Status = "active",
                Avatar = "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=150&auto=format&fit=crop&q=80",
                Bio = "Mọt sách nghiện truyện chữ và truyện tranh."
            };

            var reader2 = new User
            {
                Name = "Trần Thị Mơ",
                Email = "reader2@doctruyen.vn",
                Password = BCrypt.Net.BCrypt.HashPassword("reader123"),
                Role = "reader",
                Status = "active",
                Avatar = "https://images.unsplash.com/photo-1438761681033-6461ffad8d80?w=150&auto=format&fit=crop&q=80",
                Bio = "Độc giả yêu thích thể loại xuyên không, hài hước."
            };

            context.Users.AddRange(admin, author1, author2, reader1, reader2);
            context.SaveChanges();

            // 2. Genres
            var gTienHiep = new Genre { Name = "Tiên Hiệp", Slug = "tien-hiep", Description = "Tu tiên luyện đạo, tầm bảo đoạt mệnh phi thăng", Icon = "✨" };
            var gHuyenHuyen = new Genre { Name = "Huyền Huyễn", Slug = "huyen-huanh", Description = "Thế giới huyền bí, ma pháp võ thuật kỳ ảo", Icon = "🔮" };
            var gKiemHiep = new Genre { Name = "Kiếm Hiệp", Slug = "kiem-hiep", Description = "Ân oán giang hồ võ lâm hiệp khách", Icon = "⚔️" };
            var gTrungSinh = new Genre { Name = "Trùng Sinh", Slug = "trung-sinh", Description = "Sống lại kiếp khác để nghịch thiên cải mệnh", Icon = "🔄" };
            var gDoThi = new Genre { Name = "Đô Thị", Slug = "do-thi", Description = "Bối cảnh xã hội hiện đại nhiều biến cố", Icon = "🏙️" };
            var gHanhDong = new Genre { Name = "Hành Động", Slug = "hanh-dong", Description = "Những pha giao tranh kịch tính gay cấn", Icon = "💥" };
            var gNgonTinh = new Genre { Name = "Ngôn Tình", Slug = "ngon-tinh", Description = "Câu chuyện tình cảm lãng mạn ấm áp", Icon = "❤️" };
            var gHaiHuoc = new Genre { Name = "Hài Hước", Slug = "hai-huoc", Description = "Tình tiết gây cười giải trí nhẹ nhàng", Icon = "😂" };

            context.Genres.AddRange(gTienHiep, gHuyenHuyen, gKiemHiep, gTrungSinh, gDoThi, gHanhDong, gNgonTinh, gHaiHuoc);
            context.SaveChanges();

            // 3. Stories
            var story1 = new Story
            {
                Title = "Đấu Phá Thương Khung",
                Slug = "dau-pha-thuong-khung",
                AuthorId = author1.Id,
                AuthorName = author1.Name,
                Description = "Tại Đấu Khí Đại Lục, không có ma pháp diễm lệ, chỉ có đấu khí đã phát triển đến đỉnh phong! Tiêu Viêm - một thiên tài từng vang danh gia tộc nhưng bỗng chốc mất đi toàn bộ tu vi, trở thành phế vật bị người người khinh miệt. Trong lúc tuyệt vọng nhất, chiếc giới chỉ do người mẹ để lại đã thức tỉnh một linh hồn thần bí, mở ra con đường tu luyện đầy kỳ tích!",
                CoverUrl = "https://images.unsplash.com/photo-1518709268805-4e9042af9f23?w=500&auto=format&fit=crop&q=80",
                Type = "novel",
                Status = "completed",
                ApprovalStatus = "approved",
                Views = 154200
            };

            var story2 = new Story
            {
                Title = "Ta Là Tà Đế: Vạn Giới Du Hành",
                Slug = "ta-la-ta-de-van-gioi-du-hanh",
                AuthorId = author1.Id,
                AuthorName = author1.Name,
                Description = "Tạ Diệm - thanh niên thế kỷ 21 xuyên việt trở thành mỹ nam tử tuyệt sắc của Ma Đạo Yêu Nữ Tông Môn. Vì không muốn trở thành lô đỉnh bị hút cạn tinh hoa, hắn thức tỉnh hệ thống mô phỏng chư thiên vạn giới, bắt đầu hành trình thu hoạch điểm nguồn gốc và nghịch thiên xưng đế!",
                CoverUrl = "https://images.unsplash.com/photo-1578632767115-351597cf2477?w=500&auto=format&fit=crop&q=80",
                Type = "comic",
                Status = "ongoing",
                ApprovalStatus = "approved",
                Views = 98300
            };

            var story3 = new Story
            {
                Title = "Toàn Chức Cao Thủ",
                Slug = "toan-chuc-cao-thu",
                AuthorId = author2.Id,
                AuthorName = author2.Name,
                Description = "Diệp Tu, tuyển thủ đứng đầu tựa game Vinh Quang, bất ngờ bị câu lạc bộ ép buộc thoái lui và phải giao lại tài khoản đẳng cấp thần thánh Nhất Diệp Chi Thu. Rời khỏi thế giới tuyển thủ, hắn vào làm nhân viên ca đêm tại một quán net nhỏ. Khi khu thứ 10 của game khai mở, Diệp Tu cầm chiếc ô Thiên Cơ Tán bước vào chinh phục lại đỉnh cao hoàng kim!",
                CoverUrl = "https://images.unsplash.com/photo-1542751371-adc38448a05e?w=500&auto=format&fit=crop&q=80",
                Type = "novel",
                Status = "ongoing",
                ApprovalStatus = "approved",
                Views = 82150
            };

            var story4 = new Story
            {
                Title = "Thiên Đạo Thư Viện",
                Slug = "thien-dao-thu-vien",
                AuthorId = author2.Id,
                AuthorName = author2.Name,
                Description = "Trương Huyền xuyên việt sang dị giới làm một gã giáo viên mạt hạng sắp bị đuổi học viện. Nhưng trong đầu hắn xuất hiện một toà Thiên Đạo Thư Viện thần bí, chỉ cần nhìn bất cứ thứ gì hay ai đó vận công là lập tức thấy rõ thiếu sót và khiếm khuyết. Kể từ đây, một huyền thoại vạn thế sư biểu ra đời!",
                CoverUrl = "https://images.unsplash.com/photo-1532012164546-f432f2e37b73?w=500&auto=format&fit=crop&q=80",
                Type = "novel",
                Status = "ongoing",
                ApprovalStatus = "approved",
                Views = 64200
            };

            var story5 = new Story
            {
                Title = "Nguyên Tôn: Khởi Đầu Nghịch Cảnh",
                Slug = "nguyen-ton-khoi-dau-nghich-canh",
                AuthorId = author1.Id,
                AuthorName = author1.Name,
                Description = "Thiếu niên Chu Nguyên sinh ra mang Thánh Long khí vận, nhưng thuở ấu thơ đã bị Đại Võ cướp đoạt, trở thành kẻ có thân thể yếu nhược, kinh mạch bế tắc. Không chịu khuất phục trước số phận cay đắng, Chu Nguyên dấn thân vào con đường nghịch thiên đoạt lại vinh quang.",
                CoverUrl = "https://images.unsplash.com/photo-1514533450685-4493e01d1fdc?w=500&auto=format&fit=crop&q=80",
                Type = "novel",
                Status = "ongoing",
                ApprovalStatus = "pending", // Pending approval for Admin
                Views = 0
            };

            var story6 = new Story
            {
                Title = "Huyết Sát Ma Tôn",
                Slug = "huyet-sat-ma-ton",
                AuthorId = author1.Id,
                AuthorName = author1.Name,
                Description = "Tu ma tàn sát vạn giới, lấy máu chứng đạo.",
                CoverUrl = "https://images.unsplash.com/photo-1509198397868-475647b2a1e5?w=500&auto=format&fit=crop&q=80",
                Type = "novel",
                Status = "ongoing",
                ApprovalStatus = "rejected",
                RejectionReason = "Nội dung chứa miêu tả bạo lực quá mức không phù hợp tiêu chuẩn kiểm duyệt. Tác giả vui lòng sửa đổi văn phong chương mở đầu.",
                Views = 0
            };

            context.Stories.AddRange(story1, story2, story3, story4, story5, story6);
            context.SaveChanges();

            // Story Genres
            context.StoryGenres.AddRange(
                new StoryGenre { StoryId = story1.Id, GenreId = gTienHiep.Id },
                new StoryGenre { StoryId = story1.Id, GenreId = gHuyenHuyen.Id },
                new StoryGenre { StoryId = story1.Id, GenreId = gHanhDong.Id },
                new StoryGenre { StoryId = story2.Id, GenreId = gTrungSinh.Id },
                new StoryGenre { StoryId = story2.Id, GenreId = gHuyenHuyen.Id },
                new StoryGenre { StoryId = story2.Id, GenreId = gHaiHuoc.Id },
                new StoryGenre { StoryId = story3.Id, GenreId = gDoThi.Id },
                new StoryGenre { StoryId = story3.Id, GenreId = gHanhDong.Id },
                new StoryGenre { StoryId = story4.Id, GenreId = gHuyenHuyen.Id },
                new StoryGenre { StoryId = story4.Id, GenreId = gHaiHuoc.Id },
                new StoryGenre { StoryId = story5.Id, GenreId = gTienHiep.Id },
                new StoryGenre { StoryId = story6.Id, GenreId = gTienHiep.Id }
            );
            context.SaveChanges();

            // 4. Chapters
            var chap1_s1 = new Chapter
            {
                StoryId = story1.Id,
                ChapterNumber = 1,
                Title = "Chương 1: Phế vật Tiêu gia",
                Content = @"“Đấu chi khí, tam đoạn!”
Nhìn năm chữ to lớn sáng rực nhưng đầy chói mắt trên ma thạch bia, thiếu niên mặt không chút biểu cảm, khóe môi khẽ cong lên một nét tự giễu, hai bàn tay nắm chặt giấu trong tay áo run rẩy nhẹ.

“Tiêu Viêm, Đấu chi khí: Tam đoạn! Cấp bậc: Cấp thấp!”

Bên cạnh ma thạch bia, một gã tráng hán trung niên nhìn lướt qua bảng kết quả, lạnh lùng xướng to lên trước toàn thể tộc nhân Tiêu gia.

Lời vừa dứt, quảng trường to lớn lập tức nổi lên một trận xôn xao to nhỏ. Vô số ánh mắt châm biếm, tiếc nuối và cười nhạo đều đồng loạt phóng về phía thân ảnh gầy gò của thiếu niên.

“Ba đoạn? Chậc chậc, quả nhiên vẫn đứng yên tại chỗ! Vị thiên tài lừng danh một thời của Ô Thản Thành chúng ta xem ra thực sự đã thành đồ bỏ đi rồi.”
“Ai, nếu không phải tộc trưởng là cha hắn, loại phế vật này sớm đã bị trục xuất khỏi gia tộc rồi, làm sao còn tư cách chiếm dụng linh dược tu luyện?”
“Năm xưa lúc hắn mười một tuổi đột phá Đấu Giả, cả gia tộc đều sùng bái hắn như thần minh, nào ngờ ba năm sau lại thụt lùi đến thê thảm mức này...”

Nghe những lời bàn tán cay nghiệt xung quanh, Tiêu Viêm hít một hơi thật sâu. Hắn không nói một lời, xoay người chậm rãi bước xuống đài cao. Trên ngón tay hắn, một chiếc nhẫn màu đen cổ xưa bình lặng nằm đó, phản chiếu ánh sáng lấp lánh như đang thức tỉnh..."
            };

            var chap2_s1 = new Chapter
            {
                StoryId = story1.Id,
                ChapterNumber = 2,
                Title = "Chương 2: Dược Lão Thức Tỉnh",
                Content = @"Đêm khuya thanh vắng, trên đỉnh ngọn đồi phía sau Tiêu gia.

Tiêu Viêm ngồi xếp bằng trên một tảng đá lớn, ngước nhìn lên bầu trời đầy sao. Đêm nay, gió đêm mang theo từng cơn lạnh buốt thổi qua vạt áo hắn.

“Ba năm rồi... Rốt cuộc vì sao?” Hắn siết chặt nắm tay, đập mạnh xuống phiến đá.

Bỗng nhiên, từ chiếc nhẫn đen trên ngón tay hắn phát ra một luồng sáng nhu hòa màu lam nhạt. Một làn khói trắng nhẹ nhàng bốc lên, ngưng tụ giữa không trung thành một thân ảnh lão giả râu tóc bạc phơ, ánh mắt tinh anh nhìn Tiêu Viêm đầy vẻ thú vị.

“Ha ha, tiểu tử, ba năm qua nhờ có đấu khí thuần khiết của ngươi mà lão phu mới có thể khôi phục một tia linh hồn đấy!”

Tiêu Viêm giật nảy mình, lập tức bật dậy thủ thế cảnh giác: “Ngươi là ai? Tại sao lại ở trong giới chỉ của mẫu thân ta? Ngươi vừa nói đấu khí của ta biến mất là do ngươi hút lấy?”

Lão giả vuốt râu mỉm cười: “Tiểu tử chớ nóng vội. Lão phu tự xưng Dược Lão. Ngươi mất đi ba năm đấu khí, nhưng đổi lại lão phu sẽ truyền thụ cho ngươi thuật Luyện Dược vô thượng cùng công pháp đủ để ngươi giẫm đạp toàn bộ thiên tài Đấu Khí Đại Lục dưới chân, ngươi thấy vụ làm ăn này thế nào?”"
            };

            var chap3_s1 = new Chapter
            {
                StoryId = story1.Id,
                ChapterNumber = 3,
                Title = "Chương 3: Hôn Ước Nạp Lan",
                Content = @"Sáng hôm sau, Tiêu gia đại sảnh náo nhiệt lạ thường.

Khách quý đến từ Vân Lam Tông - tông môn thế lực số một Gia Mã Đế Quốc đã đích thân đến viếng thăm. Đi đầu là một thiếu nữ kiêu kỳ mặc trường bào nguyệt bạch, dung nhan thanh tú diễm lệ, bên hông đeo trường kiếm thanh phong. Đó chính là Nạp Lan Yên Nhiên.

“Tiêu thúc thúc, lần này Yên Nhiên mạo muội đến đây là muốn thương lượng chuyện từ hôn cùng Tiêu Viêm công tử!”

Lời nói vang lên dõng dạc giữa đại sảnh khiến toàn thể trưởng lão Tiêu gia biến sắc. Tiêu Chiến - tộc trưởng Tiêu gia, sắc mặt tái xanh, chiếc chén trà trong tay vỡ nát thành từng mảnh bột phấn vụn.

Đúng lúc này, Tiêu Viêm hiên ngang bước vào sảnh, ánh mắt rực sáng kiên định: “Nạp Lan Yên Nhiên, hôm nay không phải ngươi từ hôn ta, mà là Tiêu Viêm ta bỏ ngươi! Ba năm sau, ta sẽ đích thân lên Vân Lam Tông, cùng ngươi giải quyết mối ân oán này! Ba mươi năm Hà Đông, ba mươi năm Hà Tây, đừng khinh thiếu niên nghèo!”"
            };

            var comicImages = JsonSerializer.Serialize(new[]
            {
                "https://images.unsplash.com/photo-1534447677768-be436bb09401?w=800&auto=format&fit=crop&q=80",
                "https://images.unsplash.com/photo-1563089145-599997674d42?w=800&auto=format&fit=crop&q=80",
                "https://images.unsplash.com/photo-1607604276583-eef5d076aa5f?w=800&auto=format&fit=crop&q=80"
            });

            var chap1_s2 = new Chapter
            {
                StoryId = story2.Id,
                ChapterNumber = 1,
                Title = "Chapter 1: Xuyên Không Vào Ma Môn",
                Content = comicImages
            };

            var chap1_s5 = new Chapter
            {
                StoryId = story5.Id,
                ChapterNumber = 1,
                Title = "Chương 1: Khí vận Thánh Long bị đoạt",
                Content = "Thiếu niên Chu Nguyên quỳ gối trước tổ miếu Đại Chu vương triều, nhìn lên trời cao mây đen vần vũ. Từng luồng khí tức ngột ngạt bao phủ lấy toàn bộ hoàng thành. Hôm nay, thiếu niên quyết tâm phá bỏ xiềng xích vận mệnh!"
            };

            context.Chapters.AddRange(chap1_s1, chap2_s1, chap3_s1, chap1_s2, chap1_s5);
            context.SaveChanges();

            // 5. Favorites & History
            context.Favorites.AddRange(
                new Favorite { UserId = reader1.Id, StoryId = story1.Id },
                new Favorite { UserId = reader1.Id, StoryId = story2.Id }
            );

            context.ReadingHistories.Add(
                new ReadingHistory { UserId = reader1.Id, StoryId = story1.Id, ChapterId = chap2_s1.Id }
            );
            context.SaveChanges();

            // 6. Comments
            var c1 = new Comment
            {
                UserId = reader1.Id,
                StoryId = story1.Id,
                ChapterId = chap1_s1.Id,
                Content = "Đoạn 'Ba mươi năm Hà Đông, ba mươi năm Hà Tây' đọc lại bao nhiêu lần vẫn nổi da gà!"
            };

            var offensiveComment = new Comment
            {
                UserId = reader2.Id,
                StoryId = story1.Id,
                ChapterId = chap1_s1.Id,
                Content = "Tác giả viết như dở hơi, nhân vật chính vô dụng xem bực cả mình, vote 1 sao tẩy chay!"
            };

            context.Comments.AddRange(c1, offensiveComment);
            context.SaveChanges();

            // 7. Reports
            context.Reports.AddRange(
                new Report
                {
                    UserId = reader1.Id,
                    TargetType = "comment",
                    TargetId = offensiveComment.Id,
                    Reason = "Ngôn từ xúc phạm, kích động hiềm khích và công kích cá nhân",
                    Details = "Bình luận chứa thái độ thù hằn vô căn cứ, đề nghị quản trị viên ẩn hoặc gỡ bỏ.",
                    Status = "pending"
                },
                new Report
                {
                    UserId = reader2.Id,
                    TargetType = "chapter",
                    TargetId = chap2_s1.Id,
                    Reason = "Nội dung bị lỗi hiển thị",
                    Details = "Đoạn cuối chương 2 có vẻ bị ngắt giữa chừng, đề nghị tác giả cập nhật thêm.",
                    Status = "pending"
                }
            );
            context.SaveChanges();

            // 8. Notifications
            if (!context.Notifications.Any())
            {
                context.Notifications.AddRange(
                    new Notification
                    {
                        UserId = reader1.Id,
                        Title = "🎉 Chào mừng đến với Web Đọc Truyện",
                        Message = "Chúc bạn có những giây phút giải trí tuyệt vời với kho tàng truyện chọn lọc!",
                        Type = "system",
                        IsRead = true,
                        CreatedAt = DateTime.UtcNow.AddDays(-3)
                    },
                    new Notification
                    {
                        UserId = reader1.Id,
                        Title = "📖 Chương mới: Đấu Phá Thương Khung",
                        Message = "Tác giả Thiên Tằm Thổ Đậu vừa đăng tải chương mới cho bộ truyện bạn đang theo dõi.",
                        Type = "chapter",
                        ActionUrl = "/story/dau-pha-thuong-khung",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow.AddHours(-2)
                    },
                    new Notification
                    {
                        UserId = author1.Id,
                        Title = "✅ Truyện đã được duyệt",
                        Message = "Bộ truyện 'Đấu Phá Thương Khung' của bạn đã được kiểm duyệt và công khai toàn hệ thống.",
                        Type = "story",
                        ActionUrl = "/story/dau-pha-thuong-khung",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow.AddDays(-1)
                    },
                    new Notification
                    {
                        UserId = admin.Id,
                        Title = "⚠️ Báo cáo vi phạm mới",
                        Message = "Hệ thống ghi nhận 2 báo cáo vi phạm mới cần xử lý trong mục Quản trị.",
                        Type = "system",
                        ActionUrl = "/admin/reports",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow.AddHours(-1)
                    }
                );
                context.SaveChanges();
            }

            SeedTruyenQQData(context);
        }

        public static void SeedTruyenQQData(AppDbContext context)
        {
            if (context.Stories.Any(s => s.Slug == "solo-leveling"))
            {
                return; // Already seeded TruyenQQ stories
            }

            var author1 = context.Users.FirstOrDefault(u => u.Role == "author") ?? context.Users.First();
            var author2 = context.Users.Where(u => u.Role == "author").Skip(1).FirstOrDefault() ?? author1;
            var reader1 = context.Users.FirstOrDefault(u => u.Role == "reader") ?? context.Users.First();

            // 1. TruyenQQ Genres
            var gHanhDong = context.Genres.FirstOrDefault(g => g.Slug == "hanh-dong") ?? new Genre { Name = "Hành Động", Slug = "hanh-dong", Icon = "💥" };
            var gHuyenHuyen = context.Genres.FirstOrDefault(g => g.Slug == "huyen-huanh") ?? new Genre { Name = "Huyền Huyễn", Slug = "huyen-huanh", Icon = "🔮" };
            var gTienHiep = context.Genres.FirstOrDefault(g => g.Slug == "tien-hiep") ?? new Genre { Name = "Tiên Hiệp", Slug = "tien-hiep", Icon = "✨" };
            var gTrungSinh = context.Genres.FirstOrDefault(g => g.Slug == "trung-sinh") ?? new Genre { Name = "Trùng Sinh", Slug = "trung-sinh", Icon = "🔄" };
            var gDoThi = context.Genres.FirstOrDefault(g => g.Slug == "do-thi") ?? new Genre { Name = "Đô Thị", Slug = "do-thi", Icon = "🏙️" };
            var gHaiHuoc = context.Genres.FirstOrDefault(g => g.Slug == "hai-huoc") ?? new Genre { Name = "Hài Hước", Slug = "hai-huoc", Icon = "😂" };

            var gManhwa = context.Genres.FirstOrDefault(g => g.Slug == "manhwa");
            if (gManhwa == null)
            {
                gManhwa = new Genre { Name = "Manhwa", Slug = "manhwa", Description = "Truyện tranh phong cách Hàn Quốc siêu phẩm", Icon = "🇰🇷" };
                context.Genres.Add(gManhwa);
            }

            var gManhua = context.Genres.FirstOrDefault(g => g.Slug == "manhua");
            if (gManhua == null)
            {
                gManhua = new Genre { Name = "Manhua", Slug = "manhua", Description = "Truyện tranh màu Trung Quốc tu tiên, trọng sinh", Icon = "🇨🇳" };
                context.Genres.Add(gManhua);
            }

            var gIsekai = context.Genres.FirstOrDefault(g => g.Slug == "isekai");
            if (gIsekai == null)
            {
                gIsekai = new Genre { Name = "Chuyển Sinh", Slug = "isekai", Description = "Xuyên không sang dị giới phiêu lưu kỳ thú", Icon = "🌀" };
                context.Genres.Add(gIsekai);
            }
            context.SaveChanges();

            // 2. TruyenQQ Top Stories
            var s1 = new Story
            {
                Title = "Solo Leveling (Tôi Thăng Cấp Một Mình)",
                Slug = "solo-leveling",
                AuthorId = author1.Id,
                AuthorName = "Chugong & DUBU",
                Description = "10 năm trước, khi các 'Cổng' kết nối thế giới thực với thế giới quái vật mở ra, Sung Jin-Woo - một Thợ Săn hạng E yếu nhất thế giới, trong một lần thám hiểm Hầm Ngục Kép chết người đã nhận được nhiệm vụ bí mật và trở thành người duy nhất có khả năng Thăng Cấp không giới hạn!",
                CoverUrl = "https://images.unsplash.com/photo-1563089145-599997674d42?w=500&auto=format&fit=crop&q=80",
                Type = "comic",
                Status = "completed",
                ApprovalStatus = "approved",
                Views = 4850000
            };

            var s2 = new Story
            {
                Title = "Võ Luyện Đỉnh Phong",
                Slug = "vo-luyen-dinh-phong",
                AuthorId = author1.Id,
                AuthorName = "Mạc Mặc",
                Description = "Võ đạo đỉnh phong, là cô độc, là tịch mịch, là bước đi một mình không điểm dừng. Dương Khai - đệ tử quét rác của Lăng Tiêu Các tình cờ có được một cuốn Hắc Thư vô tự thần bí, từ đó mở ra con đường tu luyện nghịch thiên, bước lên đỉnh phong võ đạo của tinh không vạn giới!",
                CoverUrl = "https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?w=500&auto=format&fit=crop&q=80",
                Type = "comic",
                Status = "ongoing",
                ApprovalStatus = "approved",
                Views = 6200000
            };

            var s3 = new Story
            {
                Title = "Đại Quản Gia Là Ma Hoàng",
                Slug = "dai-quan-gia-la-ma-hoang",
                AuthorId = author2.Id,
                AuthorName = "Dạ Kiêu",
                Description = "Trác Nhất Phàm - Ma Hoàng đứng đầu Bát Hoàng thượng cổ vì sở hữu Cửu U Bí Lục mà bị đồ đệ phản bội vây giết. Linh hồn hắn trọng sinh vào thân xác Trác Phàm - quản gia thấp hèn của Lạc gia sắp lụi bại. Bằng trí tuệ thâm sâu và thủ đoạn tàn độc, Ma Hoàng từng bước đưa gia tộc nhỏ bé xưng bá thiên hạ!",
                CoverUrl = "https://images.unsplash.com/photo-1579783900882-c0d3dad7b119?w=500&auto=format&fit=crop&q=80",
                Type = "comic",
                Status = "ongoing",
                ApprovalStatus = "approved",
                Views = 3920000
            };

            var s4 = new Story
            {
                Title = "Toàn Chức Pháp Sư",
                Slug = "toan-chuc-phap-su",
                AuthorId = author2.Id,
                AuthorName = "Loạn (Chaos)",
                Description = "Mạc Phàm tỉnh dậy và thấy thế giới quen thuộc biến thành nơi dạy Ma Pháp thay vì khoa học. Trong ngày thức tỉnh pháp hệ, khi người khác chỉ thức tỉnh một hệ đơn lẻ, Mạc Phàm thức tỉnh song hệ Lôi và Hỏa trời ban hiếm có vô song, mở đầu huyền thoại đại pháp sư!",
                CoverUrl = "https://images.unsplash.com/photo-1518709268805-4e9042af9f23?w=500&auto=format&fit=crop&q=80",
                Type = "comic",
                Status = "ongoing",
                ApprovalStatus = "approved",
                Views = 3150000
            };

            var s5 = new Story
            {
                Title = "Đại Phụng Đả Canh Nhân",
                Slug = "dai-phung-da-canh-nhan",
                AuthorId = author1.Id,
                AuthorName = "Mại Báo Tiểu Lang Quân",
                Description = "Hứa Thất An tốt nghiệp trường cảnh sát, xuyên việt đến thế giới Tiên Hiệp huyền bí của vương triều Đại Phụng, vừa mở mắt đã bị giam vào ngục tối. Bằng kiến thức hình sự học hiện đại và suy luận phá án sắc bén, hắn gia nhập tổ chức Đả Canh Nhân khét tiếng, từng bước phá vỡ các đại kỳ án chấn động hoàng triều!",
                CoverUrl = "https://images.unsplash.com/photo-1534447677768-be436bb09401?w=500&auto=format&fit=crop&q=80",
                Type = "novel",
                Status = "completed",
                ApprovalStatus = "approved",
                Views = 2780000
            };

            var s6 = new Story
            {
                Title = "Trùng Sinh Đô Thị Tu Tiên",
                Slug = "trung-sinh-do-thi-tu-tien",
                AuthorId = author1.Id,
                AuthorName = "Thập Lí Kiếm Thần",
                Description = "Trần Bắc Huyền - độ kiếp kỳ đại tu sĩ tung hoành vũ trụ vạn năm, trong lúc vượt thiên kiếp thất bại đã bất ngờ trùng sinh về thời niên thiếu tại địa cầu. Kiếp trước ta bất lực nhìn người thân bị hãm hại, kiếp này trở về với ngàn năm tu vi và tiên pháp cái thế, ai dám cản ta?",
                CoverUrl = "https://images.unsplash.com/photo-1509198397868-475647b2a1e5?w=500&auto=format&fit=crop&q=80",
                Type = "comic",
                Status = "completed",
                ApprovalStatus = "approved",
                Views = 2950000
            };

            var s7 = new Story
            {
                Title = "Đấu La Đại Lục (Soul Land)",
                Slug = "dau-la-dai-luc",
                AuthorId = author2.Id,
                AuthorName = "Đường Gia Tam Thiếu",
                Description = "Đường Tam - ngoại môn đệ tử Đường Môn nhảy xuống Quỷ Kiến Sầu quyên sinh rồi tái sinh tại Đấu La Đại Lục, nơi ngự trị của Vũ Hồn kỳ diệu. Mang theo song sinh Vũ Hồn Lam Ngân Thảo và Hạo Thiên Chùy, Đường Tam bước vào huyền thoại Sử Lai Khắc Thất Quái!",
                CoverUrl = "https://images.unsplash.com/photo-1514533450685-4493e01d1fdc?w=500&auto=format&fit=crop&q=80",
                Type = "novel",
                Status = "completed",
                ApprovalStatus = "approved",
                Views = 5400000
            };

            var s8 = new Story
            {
                Title = "Chuyển Sinh Thành Slime",
                Slug = "chuyen-sinh-thanh-slime",
                AuthorId = author2.Id,
                AuthorName = "Fuse & Taiki Kawakami",
                Description = "Satoru Mikami bị cướp đâm chết trên phố và tái sinh tại thế giới ma pháp dưới hình dạng một chú Slime màu lam. Nhờ kỹ năng Kẻ Săn Mồi và Đại Hiền Triết, chú slime Rimuru Tempest bắt đầu xây dựng liên bang quái vật Ma Quốc Tempest hòa bình vĩ đại!",
                CoverUrl = "https://images.unsplash.com/photo-1578632767115-351597cf2477?w=500&auto=format&fit=crop&q=80",
                Type = "comic",
                Status = "ongoing",
                ApprovalStatus = "approved",
                Views = 2640000
            };

            var s9 = new Story
            {
                Title = "Ma Đạo Tổ Sư",
                Slug = "ma-dao-to-su",
                AuthorId = author2.Id,
                AuthorName = "Mặc Hương Đồng Khứu",
                Description = "Ngụy Vô Tiện - Di Lăng Lão Tổ bị vạn người vây giết tại Loạn Táng Cương. Mười ba năm sau, hắn bất ngờ được Mạc Huyền Vũ hiến xá sống lại. Cùng Lam Vong Cơ - Hàm Quang Quân thanh nhã chính trực, cả hai bước vào hành trình điều tra cánh tay quỷ bí ẩn và làm sáng tỏ ân oán năm xưa.",
                CoverUrl = "https://images.unsplash.com/photo-1532012164546-f432f2e37b73?w=500&auto=format&fit=crop&q=80",
                Type = "novel",
                Status = "completed",
                ApprovalStatus = "approved",
                Views = 3890000
            };

            var s10 = new Story
            {
                Title = "Ngự Linh Thế Giới",
                Slug = "ngu-linh-the-gioi",
                AuthorId = author1.Id,
                AuthorName = "Bát Trảo",
                Description = "Tác giả truyện tranh xuyên vào chính tác phẩm do mình vẽ, trở thành một nhân vật phản diện xui xẻo chỉ sống được vài chap. Nắm giữ toàn bộ thiết lập ngự linh thú và bí mật thế giới, hắn thu phục linh thú thượng cổ thần cấp để lật ngược tình thế!",
                CoverUrl = "https://images.unsplash.com/photo-1542751371-adc38448a05e?w=500&auto=format&fit=crop&q=80",
                Type = "comic",
                Status = "ongoing",
                ApprovalStatus = "approved",
                Views = 1850000
            };

            var s11 = new Story
            {
                Title = "Ta Có Một Sơn Trại",
                Slug = "ta-co-mot-son-trai",
                AuthorId = author1.Id,
                AuthorName = "Thần Thâu Lĩnh Vực",
                Description = "Trình Đại Lôi xuyên không trở thành trại chủ của một sơn trại sắp sụp đổ chỉ còn lại 2 thuộc hạ già yếu. May mắn kích hoạt Hệ Thống Xây Dựng Trại Chủ, hắn thu nhận anh hùng hảo hán, mở mang điền trang, luyện binh mã, biến sơn trại thành cứ điểm kiên cố nhất!",
                CoverUrl = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=500&auto=format&fit=crop&q=80",
                Type = "comic",
                Status = "ongoing",
                ApprovalStatus = "approved",
                Views = 2120000
            };

            var s12 = new Story
            {
                Title = "Vạn Cổ Đệ Nhất Thần",
                Slug = "van-co-de-nhat-than",
                AuthorId = author1.Id,
                AuthorName = "Phong Thanh Dương",
                Description = "Lý Thiên Mệnh bị kẻ thù hãm hại mất đi Cộng Sinh Thú và bị trục xuất khỏi Viêm Hoàng Học Viện. Trong nghịch cảnh, hắn thức tỉnh Thập Giới Luân Hồi Kiếp và có được quả trứng Thái Cổ Hỗn Độn Cự Thú bí ẩn, bước lên hành trình ngự thú xưng vạn cổ đệ nhất thần!",
                CoverUrl = "https://images.unsplash.com/photo-1518709268805-4e9042af9f23?w=500&auto=format&fit=crop&q=80",
                Type = "novel",
                Status = "ongoing",
                ApprovalStatus = "approved",
                Views = 3450000
            };

            context.Stories.AddRange(s1, s2, s3, s4, s5, s6, s7, s8, s9, s10, s11, s12);
            context.SaveChanges();

            // Link Story Genres
            context.StoryGenres.AddRange(
                new StoryGenre { StoryId = s1.Id, GenreId = gHanhDong.Id },
                new StoryGenre { StoryId = s1.Id, GenreId = gManhwa.Id },
                new StoryGenre { StoryId = s2.Id, GenreId = gTienHiep.Id },
                new StoryGenre { StoryId = s2.Id, GenreId = gManhua.Id },
                new StoryGenre { StoryId = s3.Id, GenreId = gTrungSinh.Id },
                new StoryGenre { StoryId = s3.Id, GenreId = gManhua.Id },
                new StoryGenre { StoryId = s4.Id, GenreId = gDoThi.Id },
                new StoryGenre { StoryId = s4.Id, GenreId = gHanhDong.Id },
                new StoryGenre { StoryId = s5.Id, GenreId = gTienHiep.Id },
                new StoryGenre { StoryId = s5.Id, GenreId = gHaiHuoc.Id },
                new StoryGenre { StoryId = s6.Id, GenreId = gDoThi.Id },
                new StoryGenre { StoryId = s6.Id, GenreId = gTrungSinh.Id },
                new StoryGenre { StoryId = s7.Id, GenreId = gHuyenHuyen.Id },
                new StoryGenre { StoryId = s8.Id, GenreId = gIsekai.Id },
                new StoryGenre { StoryId = s9.Id, GenreId = gTienHiep.Id },
                new StoryGenre { StoryId = s10.Id, GenreId = gHuyenHuyen.Id },
                new StoryGenre { StoryId = s11.Id, GenreId = gHaiHuoc.Id },
                new StoryGenre { StoryId = s12.Id, GenreId = gTienHiep.Id }
            );
            context.SaveChanges();

            // 3. Chapters
            var chapters = new List<Chapter>
            {
                new Chapter { StoryId = s1.Id, ChapterNumber = 1, Title = "Chapter 1: Thợ Săn Hạng E Sung Jin-Woo", Content = "Thế giới đối mặt với sự xuất hiện của các cánh cổng hầm ngục kỳ bí. Sung Jin-Woo bước vào hầm ngục D-Rank với tư cách thợ săn yếu nhất nhân loại.", Views = 152000 },
                new Chapter { StoryId = s1.Id, ChapterNumber = 2, Title = "Chapter 2: Khảo Nghiệm Hầm Ngục Kép", Content = "Các tượng đá khổng lồ thức tỉnh trong căn phòng bí mật. Quy tắc sinh tồn được hé lộ: Kính sợ Thần, Tôn sùng Thần, Chứng minh đức tin!", Views = 143000 },
                new Chapter { StoryId = s1.Id, ChapterNumber = 3, Title = "Chapter 3: Bảng Trạng Thái Hệ Thống Xuất Hiện", Content = "Khi Jin-Woo tưởng chừng đã hy sinh, một bảng thông báo ba chiều hiện ra trước mắt: [Chúc mừng bạn đã hoàn thành bí mật của Hầm Ngục Kép. Bắt đầu cài đặt Hệ Thống Người Chơi.]", Views = 138000 },

                new Chapter { StoryId = s2.Id, ChapterNumber = 1, Title = "Chương 1: Đệ tử quét rác Lăng Tiêu Các", Content = "Dương Khai quét dọn sân tập Lăng Tiêu Các, mỗi ngày nhận lấy sự khinh miệt của đồng môn. Nhưng ý chí bất khuất trong lòng hắn chưa từng lung lay.", Views = 210000 },
                new Chapter { StoryId = s2.Id, ChapterNumber = 2, Title = "Chương 2: Hắc Thư Vô Tự Thần Bí", Content = "Dưới gối nằm của Dương Khai, một cuốn sách đen không chữ bỗng nhiên hấp thụ giọt máu tươi và mở ra truyền thừa ma đạo ngạo cốt vô song!", Views = 195000 },

                new Chapter { StoryId = s3.Id, ChapterNumber = 1, Title = "Chapter 1: Ma Hoàng Bị Đồ Đệ Phản Bội", Content = "Trác Nhất Phàm bị đệ tử thân tín bán đứng, bảy vị đại cao thủ Bát Hoàng liên thủ vây sát. Hắn quyết định kích nổ Cửu U Bí Lục để đồng quy vu tận!", Views = 180000 },
                new Chapter { StoryId = s3.Id, ChapterNumber = 2, Title = "Chapter 2: Trọng Sinh Làm Quản Gia Lạc Gia", Content = "Tỉnh lại trong thân xác thiếu niên quản gia phế vật của Lạc gia, Trác Phàm đối mặt với sự uy hiếp của Hắc Phong Trại và ánh mắt cầu cứu của Lạc Vân Thường.", Views = 175000 },

                new Chapter { StoryId = s4.Id, ChapterNumber = 1, Title = "Chapter 1: Giấc Mộng Chuyển Dời Ma Pháp", Content = "Mạc Phàm ngỡ ngàng khi thầy giáo trên bục giảng nói về Ma Pháp Sơ Cấp thay vì định lý Newton. Thế giới khoa học đã hoàn toàn biến thành thế giới phép thuật.", Views = 160000 },
                new Chapter { StoryId = s4.Id, ChapterNumber = 2, Title = "Chapter 2: Thức Tỉnh Song Hệ Lôi Hỏa", Content = "Tại buổi lễ thức tỉnh pháp hệ trường Thiên Lan, khi quả cầu ma pháp phát sáng, Mạc Phàm đồng thời thức tỉnh ngọn lửa đỏ rực rỡ và tia sét tím cuồng bạo!", Views = 155000 },

                new Chapter { StoryId = s5.Id, ChapterNumber = 1, Title = "Chương 1: Mở Đầu Nơi Ngục Thất", Content = "Hứa Thất An tỉnh dậy trong ngục tối bẩn thỉu của huyện nha. Hắn biết nếu không tìm ra số bạc thuế bị cướp trong vòng ba ngày, cả nhà sẽ bị xử chém lưu đày.", Views = 140000 },
                new Chapter { StoryId = s5.Id, ChapterNumber = 2, Title = "Chương 2: Phá Kỳ Án Thuế Ngân", Content = "Bằng phương pháp khám nghiệm hiện trường hiện đại và phân tích tính chất hóa học của kim loại, Hứa Thất An vạch trần âm mưu của viên chủ bạ trước công đường.", Views = 135000 },

                new Chapter { StoryId = s7.Id, ChapterNumber = 1, Title = "Chương 1: Quỷ Kiến Sầu Đoạt Mệnh", Content = "Đường Tam để lại Phật Nộ Đường Liên tuyệt đỉnh rồi nhảy xuống vực sâu vạn trượng, linh hồn phiêu bạt đến thế giới Đấu La Đại Lục tại Thánh Hồn Thôn.", Views = 240000 },
                new Chapter { StoryId = s7.Id, ChapterNumber = 2, Title = "Chương 2: Tiên Thiên Mãn Hồn Lực", Content = "Tố Vân Đào kinh ngạc khi thấy quả cầu kiểm tra hồn lực sáng rực tối đa: 'Lam Ngân Thảo phế vũ hồn... nhưng lại là Tiên Thiên Mãn Hồn Lực?!'", Views = 230000 },

                new Chapter { StoryId = s8.Id, ChapterNumber = 1, Title = "Chapter 1: Tái Sinh Thành Cục Slime", Content = "Satoru Mikami mở mắt ra và thấy mình không có mắt mũi, chỉ là một khối thạch nhầy nhụa nhưng có thể hấp thụ cỏ thảo dược và khoáng thạch ma pháp.", Views = 120000 },
                new Chapter { StoryId = s8.Id, ChapterNumber = 2, Title = "Chapter 2: Kết Bái Cùng Bạo Phong Long", Content = "Gặp gỡ con rồng khổng lồ Veldora bị phong ấn 300 năm, Rimuru nuốt trọn phong ấn vào dạ dày không gian và đặt tên cho nhau thành bạn tri kỷ.", Views = 118000 }
            };

            context.Chapters.AddRange(chapters);
            context.SaveChanges();

            // 4. Seed Rich Notifications for Readers
            context.Notifications.AddRange(
                new Notification
                {
                    UserId = reader1.Id,
                    Title = "📖 Chương mới: Solo Leveling",
                    Message = "Chương 3: Bảng Trạng Thái Hệ Thống Xuất Hiện vừa được dịch chuẩn TruyenQQ. Đọc ngay!",
                    Type = "chapter",
                    ActionUrl = $"/Story/Detail/{s1.Id}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-12)
                },
                new Notification
                {
                    UserId = reader1.Id,
                    Title = "🔥 Đứng TOP 1: Võ Luyện Đỉnh Phong",
                    Message = "Bộ truyện đạt mốc 6.2 triệu lượt đọc và dẫn đầu BXH Thịnh Hành toàn hệ thống.",
                    Type = "story",
                    ActionUrl = $"/Story/Detail/{s2.Id}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow.AddHours(-1)
                },
                new Notification
                {
                    UserId = reader1.Id,
                    Title = "💬 Phản hồi bình luận mới",
                    Message = "Độc giả Trần Thị Mơ vừa thích và thảo luận về bộ truyện Đại Quản Gia Là Ma Hoàng.",
                    Type = "comment",
                    ActionUrl = $"/Story/Detail/{s3.Id}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow.AddHours(-3)
                },
                new Notification
                {
                    UserId = reader1.Id,
                    Title = "📖 Khám phá MêTruyệnPlus",
                    Message = "Đăng ký gói VIP và nạp Xu để kích hoạt thành viên VIP.",
                    Type = "system",
                    ActionUrl = "/Home/Favorites",
                    IsRead = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                },
                new Notification
                {
                    UserId = reader1.Id,
                    Title = "🎉 Chào mừng đến với MêTruyệnPlus",
                    Message = "Khám phá kho truyện tranh và tiểu thuyết chuẩn TruyenQQ với hơn 18 bộ siêu phẩm hấp dẫn.",
                    Type = "system",
                    ActionUrl = "/",
                    IsRead = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                }
            );
            context.SaveChanges();
        }
    }
}
