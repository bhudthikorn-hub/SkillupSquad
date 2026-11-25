using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Skill_up.Data;
using Skill_up.Models;
using System.Diagnostics;

namespace Skill_up.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Login()
        {
            return View();
        }

        public IActionResult Explore()
        {
            return View();
        }

        public IActionResult Quiz()
        {
            return View();
        }

        [HttpGet]
        public IActionResult PreLearningQuiz(string? skill)
        {
            return View(CreatePreLearningQuiz(skill));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult PreLearningQuiz(string skill, int[]? answers)
        {
            var quiz = CreatePreLearningQuiz(skill);
            var submittedAnswers = answers ?? Array.Empty<int>();
            var correctAnswers = quiz.Questions
                .Select((question, index) => index < submittedAnswers.Length && submittedAnswers[index] == question.CorrectOptionIndex)
                .Count(isCorrect => isCorrect);

            quiz.IsSubmitted = true;
            quiz.Score = quiz.Questions.Count == 0
                ? 0
                : (int)Math.Round(correctAnswers * 100d / quiz.Questions.Count);

            return View(quiz);
        }

        public async Task<IActionResult> Profile()
        {
            var profile = new ProfileViewModel();
            if (User.Identity?.IsAuthenticated != true)
            {
                return View(profile);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user is not null)
            {
                profile.DisplayName = string.IsNullOrWhiteSpace(user.DisplayName)
                    ? user.UserName ?? "ผู้เรียน"
                    : user.DisplayName.Trim();
                profile.Email = user.Email ?? "ยังไม่ได้เพิ่มอีเมล";
                profile.AvatarLetter = profile.DisplayName[0].ToString().ToUpperInvariant();
            }

            var userId = _userManager.GetUserId(User);
            var learningCourses = await _db.LearningCourses
                .AsNoTracking()
                .Include(item => item.Course)
                .Where(item => item.UserId == userId && item.Course.IsActive)
                .OrderByDescending(item => item.AddedAtUtc)
                .ToListAsync();

            var skills = learningCourses
                .Select(item => new ProfileSkillViewModel
                {
                    Name = item.Course.Name,
                    Category = item.Course.Category,
                    ProgressPercent = Math.Clamp(item.ProgressPercent, 0, 100)
                })
                .ToList();

            profile.Skills = skills;
            profile.CompletedCourseCount = skills.Count(skill => skill.ProgressPercent >= 100);
            profile.AverageProgress = skills.Count == 0
                ? 0
                : (int)Math.Round(skills.Average(skill => skill.ProgressPercent));
            profile.CurrentCourse = skills.FirstOrDefault(skill => skill.ProgressPercent < 100);

            return View(profile);
        }

        public async Task<IActionResult> CourseLearning(string? skill)
        {
            var courseLearning = CreateCourseLearning(skill);
            courseLearning.PostCourseQuiz = CreatePostCourseQuiz(courseLearning.CategoryName);
            LearningCourse? learningCourse = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = _userManager.GetUserId(User);
                learningCourse = await _db.LearningCourses
                    .AsNoTracking()
                    .Include(item => item.Course)
                    .FirstOrDefaultAsync(item => item.UserId == userId && item.Course.Name == courseLearning.CategoryName);

                if (learningCourse is not null)
                {
                    courseLearning.IsEnrolled = true;
                    courseLearning.LearningCourseId = learningCourse.Id;
                    courseLearning.ProgressPercent = Math.Clamp(learningCourse.ProgressPercent, 0, 100);
                }
            }

            var completedLessonNumbers = ParseCompletedLessonNumbers(learningCourse?.CompletedLessonNumbers);
            courseLearning.Lessons = courseLearning.Lessons
                .Select(lesson =>
                {
                    lesson.IsCompleted = completedLessonNumbers.Contains(lesson.Number);
                    return lesson;
                })
                .ToList();

            return View(courseLearning);
        }

        [HttpGet]
        public IActionResult PostCourseQuiz(string category)
        {
            var normalizedCategory = string.IsNullOrWhiteSpace(category) ? "3D & Animation" : category.Trim();
            return View(CreatePostCourseQuiz(normalizedCategory));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckPostCourseQuiz(string category, string[]? answers)
        {
            var normalizedCategory = string.IsNullOrWhiteSpace(category) ? "3D & Animation" : category.Trim();
            var quiz = CreatePostCourseQuiz(normalizedCategory);
            if (answers is null || answers.Length != quiz.Questions.Count || answers.Any(answer => !int.TryParse(answer, out _)))
            {
                quiz.ErrorMessage = "กรุณาตอบคำถามให้ครบทั้ง 10 ข้อก่อนตรวจคะแนน";
                return View(nameof(PostCourseQuiz), quiz);
            }

            quiz.IsChecked = true;
            quiz.Score = answers.Select((answer, index) =>
                int.TryParse(answer, out var selected) && selected == quiz.Questions[index].CorrectOptionIndex ? 1 : 0).Sum();

            var userId = _userManager.GetUserId(User);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                _db.QuizAttempts.Add(new QuizAttempt
                {
                    UserId = userId,
                    Category = normalizedCategory,
                    Score = quiz.Score,
                    MaxScore = quiz.Questions.Count,
                    IsPassed = quiz.Questions.Count > 0 && quiz.Score * 100 >= quiz.Questions.Count * 70
                });
                await _db.SaveChangesAsync();
            }

            return View(nameof(PostCourseQuiz), quiz);
        }

        [HttpGet]
        public IActionResult LessonContent(string skill, int lessonNumber, string mode = "read")
        {
            var course = CreateCourseLearning(skill);
            var lesson = course.Lessons.FirstOrDefault(item => item.Number == lessonNumber);
            if (lesson is null)
            {
                return RedirectToAction(nameof(CourseLearning), new { skill });
            }

            var normalizedMode = mode.Trim().ToLowerInvariant() switch
            {
                "listen" => "listen",
                "watch" => "watch",
                "practice" => "practice",
                _ => "read"
            };

            return View(CreateLessonContent(course.CategoryName, lesson, normalizedMode));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CheckPractice(string skill, int lessonNumber, string[]? answers)
        {
            var course = CreateCourseLearning(skill);
            var lesson = course.Lessons.FirstOrDefault(item => item.Number == lessonNumber);
            if (lesson is null)
            {
                return RedirectToAction(nameof(CourseLearning), new { skill });
            }

            var content = CreateLessonContent(course.CategoryName, lesson, "practice");
            if (answers is null || answers.Length != content.PracticeQuestions.Count || answers.Any(answer => !int.TryParse(answer, out _)))
            {
                content.PracticeChecked = false;
                return View(nameof(LessonContent), content);
            }

            content.PracticeChecked = true;
            content.PracticeScore = (answers ?? Array.Empty<string>())
                .Select((answer, index) => int.TryParse(answer, out var selected) && index < content.PracticeQuestions.Count && selected == content.PracticeQuestions[index].CorrectOptionIndex ? 1 : 0)
                .Sum();

            return View(nameof(LessonContent), content);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteLesson(string skill, int lessonNumber, string? learningMode = null, bool practiceChecked = false)
        {
            var courseLearning = CreateCourseLearning(skill);
            if (lessonNumber < 1 || lessonNumber > courseLearning.Lessons.Count)
            {
                return RedirectToAction(nameof(CourseLearning), new { skill });
            }

            if (string.Equals(learningMode, "practice", StringComparison.OrdinalIgnoreCase) && !practiceChecked)
            {
                return RedirectToAction(nameof(LessonContent), new { skill, lessonNumber, mode = "practice" });
            }

            var userId = _userManager.GetUserId(User);
            var learningCourse = await _db.LearningCourses
                .Include(item => item.Course)
                .FirstOrDefaultAsync(item => item.UserId == userId && item.Course.Name == courseLearning.CategoryName);
            if (learningCourse is null)
            {
                return RedirectToAction(nameof(Category), new { skill = courseLearning.CategoryName });
            }

            var completedLessonNumbers = ParseCompletedLessonNumbers(learningCourse.CompletedLessonNumbers);
            completedLessonNumbers.Add(lessonNumber);
            learningCourse.CompletedLessonNumbers = SerializeCompletedLessonNumbers(completedLessonNumbers);
            learningCourse.ProgressPercent = (int)Math.Round(completedLessonNumbers.Count * 100d / courseLearning.Lessons.Count);
            await _db.SaveChangesAsync();

            if (learningCourse.ProgressPercent >= 100)
            {
                return RedirectToAction(nameof(Category), new { skill = courseLearning.CategoryName });
            }

            return RedirectToAction(nameof(CourseLearning), new { skill = courseLearning.CategoryName });
        }

        public async Task<IActionResult> Learning()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return View(Array.Empty<LearningCourse>());
            }

            var userId = _userManager.GetUserId(User);
            var learningCourses = await _db.LearningCourses
                .AsNoTracking()
                .Include(item => item.Course)
                .Where(item => item.UserId == userId && item.Course.IsActive)
                .OrderByDescending(item => item.AddedAtUtc)
                .ToListAsync();

            return View(learningCourses);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToLearning(string courseName, string? category, string? returnUrl)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                var loginReturnUrl = string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl)
                    ? Url.Action(nameof(Category), "Home", new { skill = courseName })
                    : returnUrl;

                return RedirectToAction("Login", "Account", new { returnUrl = loginReturnUrl });
            }

            if (string.IsNullOrWhiteSpace(courseName))
            {
                return RedirectToLocal(returnUrl);
            }

            var userId = _userManager.GetUserId(User);
            var course = await _db.Courses.FirstOrDefaultAsync(item => item.Name == courseName);
            if (course is null)
            {
                course = new Course
                {
                    Name = courseName.Trim(),
                    Category = string.IsNullOrWhiteSpace(category) ? courseName.Trim() : category.Trim()
                };
                _db.Courses.Add(course);
                await _db.SaveChangesAsync();
            }

            var alreadyAdded = await _db.LearningCourses
                .AnyAsync(item => item.UserId == userId && item.CourseId == course.Id);
            if (!alreadyAdded)
            {
                _db.LearningCourses.Add(new LearningCourse
                {
                    UserId = userId!,
                    CourseId = course.Id
                });
                await _db.SaveChangesAsync();
            }

            return RedirectToLocal(returnUrl, nameof(Learning));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromLearning(int id)
        {
            var userId = _userManager.GetUserId(User);
            var learningCourse = await _db.LearningCourses
                .FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);
            if (learningCourse is not null)
            {
                _db.LearningCourses.Remove(learningCourse);
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Learning));
        }

        public async Task<IActionResult> Category(string? skill)
        {
            var skillName = string.IsNullOrWhiteSpace(skill) ? "3D & Animation" : skill.Trim();
            ViewData["SkillName"] = skillName;
            ViewData["Title"] = ViewData["SkillName"];

            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = _userManager.GetUserId(User);
                var learningCourse = await _db.LearningCourses
                    .AsNoTracking()
                    .Include(item => item.Course)
                    .FirstOrDefaultAsync(item => item.UserId == userId && item.Course.Name == skillName);
                ViewData["LearningCourseId"] = learningCourse?.Id;
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private static HashSet<int> ParseCompletedLessonNumbers(string? completedLessonNumbers)
        {
            if (string.IsNullOrWhiteSpace(completedLessonNumbers))
            {
                return new HashSet<int>();
            }

            return completedLessonNumbers
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => int.TryParse(value, out _))
                .Select(int.Parse)
                .ToHashSet();
        }

        private static string SerializeCompletedLessonNumbers(IEnumerable<int> completedLessonNumbers)
        {
            return string.Join(',', completedLessonNumbers.OrderBy(number => number));
        }

        private static CourseLearningViewModel CreateCourseLearning(string? skill)
        {
            var category = string.IsNullOrWhiteSpace(skill) ? "3D & Animation" : skill.Trim();
            var course = new CourseLearningViewModel
            {
                CategoryName = category,
                Description = category switch
                {
                    "Graphic Design" => "เรียนรู้การออกแบบภาพให้สวย ชัดเจน และสื่อสารได้ตรงกลุ่มเป้าหมาย",
                    "Video Editing" => "ฝึกตัดต่อวิดีโอให้เล่าเรื่องได้ลื่นไหล พร้อมเทคนิคสำหรับงานจริง",
                    "UX/UI" => "ออกแบบประสบการณ์ผู้ใช้และหน้าจอแอปให้ใช้งานง่ายและน่าประทับใจ",
                    "Web Development" => "สร้างเว็บไซต์ตั้งแต่โครงสร้างพื้นฐานจนถึงหน้าเว็บที่ใช้งานได้จริง",
                    "Digital Marketing" => "วางแผนการตลาดดิจิทัล สร้างคอนเทนต์ และวัดผลแคมเปญอย่างเป็นระบบ",
                    _ => "เรียนรู้การสร้างผลงาน 3D ตั้งแต่การขึ้นโมเดล แสง วัสดุ ไปจนถึงการเรนเดอร์"
                },
                ImageName = category switch
                {
                    "Graphic Design" => "Graphic Design2.png",
                    "Video Editing" => "Video Editing2.png",
                    "UX/UI" => "UXUI2.png",
                    "Web Development" => "Web Development2.png",
                    "Digital Marketing" => "Digital Marketing2.png",
                    _ => "3D&Animation2.png"
                },
                Lessons = new[]
                {
                    Lesson(1, category == "Graphic Design" ? "พื้นฐานการออกแบบกราฟิก (Graphic Design Fundamentals)" : category == "3D & Animation" ? "พื้นฐานโปรแกรม Blender สำหรับผู้เริ่มต้น (Absolute Beginner Basics)" : category == "Video Editing" ? "เทคนิคการตัดต่อพื้นฐาน 10 รูปแบบ (10 Essential Editing Cuts)" : category == "Digital Marketing" ? "ภาพรวมการตลาดดิจิทัล (Digital Marketing Overview)" : category == "UX/UI" ? "ความเข้าใจพื้นฐานเกี่ยวกับ UI และ UX (UI/UX Fundamentals)" : category == "Web Development" ? "กลไกการทำงานของเว็บไซต์ (How the Web Works)" : $"รู้จักพื้นฐาน {category}", "ทำความเข้าใจแนวคิดและเครื่องมือสำคัญของหมวดนี้", "5-10 นาที"),
                    Lesson(2, category == "Graphic Design" ? "หลักการออกแบบกราฟิก (Principles of Design)" : category == "3D & Animation" ? "การปั้นโมเดลและจัดการ UV เบื้องต้น (3D Modeling & UV Workflow)" : category == "Video Editing" ? "ศิลปะการเล่าเรื่องด้วยการตัดต่อ (Storytelling Through Cuts)" : category == "Digital Marketing" ? "การเชื่อมโยงช่องทางและวัดผลการตลาด (Marketing Funnel & Measurement)" : category == "UX/UI" ? "กระบวนการคิดเชิงออกแบบ (Design Thinking Process)" : category == "Web Development" ? "โครงสร้างสถาปัตยกรรมเว็บแอปพลิเคชัน (Frontend, Backend, API & Database)" : "เครื่องมือและเทคนิคเบื้องต้น", "เรียนรู้วิธีใช้เครื่องมือหลักผ่านตัวอย่าง", "5-10 นาที"),
                    Lesson(3, category == "Graphic Design" ? "การจัดวางองค์ประกอบและเลย์เอาต์ (Layout & Composition)" : category == "3D & Animation" ? "การจัดแสงใน Blender (Lighting Basics)" : category == "Video Editing" ? "เวิร์กโฟลว์การตัดต่อให้เร็วขึ้น 10 เท่า (Fast Editing Workflow)" : category == "Digital Marketing" ? "การทำ SEO ให้ติดอันดับค้นหา (Search Engine Optimization)" : category == "UX/UI" ? "ขั้นตอนการออกแบบ UI/UX และระบบการออกแบบ (Design System & Workflow)" : category == "Web Development" ? "ภาพรวมการพัฒนาเว็บครบวงจร (Web Development Stack & Infrastructure)" : "หลักการทำงานที่ถูกต้อง", "ฝึกวิเคราะห์และเลือกวิธีทำงานให้เหมาะกับโจทย์", "5-10 นาที"),
                    Lesson(4, category == "Graphic Design" ? "ทฤษฎีสีพื้นฐาน (Color Theory)" : category == "3D & Animation" ? "การใส่กระดูกให้ตัวละครเบื้องต้น (Character Rigging)" : category == "Video Editing" ? "คู่มือเริ่มต้นใช้งาน CapCut สำหรับ PC & Mac" : category == "Digital Marketing" ? "กลยุทธ์การตลาดด้วยเนื้อหา (Content Marketing Strategy)" : category == "UX/UI" ? "หลักการออกแบบ UI เชิงลึก (Core UI Concepts & Details)" : category == "Web Development" ? "พื้นฐานภาษา HTML (HTML Essentials)" : "ลงมือทำจากโจทย์จริง", "ประยุกต์ความรู้กับแบบฝึกหัดที่ใกล้เคียงงานจริง", "5-10 นาที"),
                    Lesson(5, category == "Graphic Design" ? "กฎเหล็ก 6 ข้อของการจัดวางตัวอักษร (Typography Golden Rules)" : category == "3D & Animation" ? "เวิร์กโฟลว์ครบวงจร ปั้น-ใส่กระดูก-ทำแอนิเมชัน (Complete Animation Workflow)" : category == "Video Editing" ? "คู่มือเริ่มต้นใช้งาน Adobe Premiere Pro" : category == "Digital Marketing" ? "การปรับแต่งโซเชียลมีเดียให้มีประสิทธิภาพ (Social Media Optimization - SMO)" : category == "UX/UI" ? "เทคนิคการร่างโครงสร้างหน้าเว็บลงบนกระดาษ (Sketching Wireframes)" : category == "Web Development" ? "การจัดวางเลย์เอาต์ด้วย CSS Flexbox (CSS Flexbox Layout)" : "โปรเจกต์สรุปบทเรียน", "สร้างผลงานชิ้นแรกและทบทวนสิ่งที่ได้เรียนรู้", "5-10 นาที")
                }
            };

            return course;
        }

        private static CourseLessonViewModel Lesson(int number, string title, string description, string duration)
        {
            return new CourseLessonViewModel
            {
                Number = number,
                Title = title,
                Description = description,
                Duration = duration
            };
        }

        private static PostCourseQuizViewModel CreatePostCourseQuiz(string category)
        {
            var questions = category switch
            {
                "Graphic Design" => new[]
                {
                    QuizQuestion("หลักการใดช่วยสร้างความโดดเด่นให้กับองค์ประกอบในการออกแบบ?", "Contrast", "Database", "Rigging", "Proxy"),
                    QuizQuestion("Proximity ในการออกแบบหมายถึงอะไร?", "จัดกลุ่มสิ่งที่เกี่ยวข้องให้อยู่ใกล้กัน", "เพิ่มความเร็ววิดีโอ", "สร้างฐานข้อมูล", "ลดความละเอียดภาพ"),
                    QuizQuestion("สีคู่ตรงข้ามบนวงล้อสีให้ผลลัพธ์ใด?", "ความแตกต่างที่โดดเด่น", "เสียงที่ชัดขึ้น", "หน้าเว็บที่เร็วขึ้น", "โมเดล 3D ที่เบาลง"),
                    QuizQuestion("Typography เกี่ยวข้องกับเรื่องใด?", "การเลือกและจัดวางตัวอักษร", "การตัดต่อเสียง", "การเขียน API", "การทำ SEO"),
                    QuizQuestion("Layout มีหน้าที่หลักอย่างไร?", "จัดวางองค์ประกอบให้เป็นระบบ", "บีบอัดไฟล์เสียง", "เพิ่มเฟรมเรต", "สร้างผู้ชมโฆษณา"),
                    QuizQuestion("Whitespace ช่วยเรื่องใด?", "เพิ่มพื้นที่หายใจและความอ่านง่าย", "ทำให้ภาพเคลื่อนไหว", "เชื่อมต่อฐานข้อมูล", "เพิ่มจำนวนสีเสมอ"),
                    QuizQuestion("ลำดับชั้นทางสายตาช่วยให้ผู้ชมทำอะไร?", "เห็นความสำคัญของข้อมูลตามลำดับ", "ดาวน์โหลดไฟล์เร็วขึ้น", "สร้างโมเดลได้ทันที", "เพิ่มผู้ติดตามอัตโนมัติ"),
                    QuizQuestion("Contrast ที่ดีควรช่วยอะไรในงานออกแบบ?", "แยกองค์ประกอบและเน้นจุดสำคัญ", "ลดจำนวนหน้าเว็บ", "เพิ่มความยาวเสียง", "เปลี่ยนโค้ดเป็นวิดีโอ"),
                    QuizQuestion("ก่อนเริ่มออกแบบควรทำสิ่งใด?", "ทำความเข้าใจโจทย์และกลุ่มเป้าหมาย", "เลือกฟอนต์แบบสุ่ม", "ลบโครงร่างทั้งหมด", "ส่งงานทันที"),
                    QuizQuestion("ไฟล์ภาพแบบใดเหมาะกับโลโก้ที่ต้องย่อขยาย?", "ไฟล์เวกเตอร์", "ไฟล์เสียง", "ไฟล์ฐานข้อมูล", "ไฟล์ซับไตเติล")
                },
                "Video Editing" => new[]
                {
                    QuizQuestion("Jump cut ใช้เพื่ออะไร?", "ตัดช่วงเวลาที่ไม่จำเป็นออก", "สร้างฐานข้อมูล", "วาดไอคอน", "ทำ SEO"),
                    QuizQuestion("Timeline ใช้จัดการอะไร?", "ลำดับคลิปและเสียง", "สีของโลโก้เท่านั้น", "ข้อมูลผู้ใช้อย่างเดียว", "โครงสร้าง HTML"),
                    QuizQuestion("B-roll คืออะไร?", "ภาพเสริมที่ช่วยเล่าเรื่อง", "เสียงแจ้งเตือน", "ชื่อฐานข้อมูล", "แบบอักษร"),
                    QuizQuestion("การตัดต่อที่ดีควรเริ่มจากอะไร?", "วางแผนเรื่องและลำดับภาพ", "ใส่เอฟเฟกต์ทุกคลิป", "เร่งทุกช็อต", "ลบเสียงทั้งหมด"),
                    QuizQuestion("CapCut และ Premiere Pro ใช้ทำอะไร?", "ตัดต่อวิดีโอ", "สร้างโมเดล 3D เท่านั้น", "ทำโฆษณาอัตโนมัติเท่านั้น", "ออกแบบฐานข้อมูล"),
                    QuizQuestion("J-cut คือเทคนิคใด?", "เสียงของช็อตถัดไปมาก่อนภาพ", "ภาพนิ่งตลอดทั้งเรื่อง", "การย้อนวิดีโอ", "การลดขนาดไฟล์ภาพ"),
                    QuizQuestion("Color correction มีเป้าหมายใด?", "ปรับสีให้สมดุลและสม่ำเสมอ", "เพิ่มจำนวนฉาก", "สร้างคำถาม Quiz", "จัดโฟลเดอร์"),
                    QuizQuestion("เสียงในวิดีโอควรมีลักษณะอย่างไร?", "ชัดเจนและสมดุลกับภาพ", "ดังที่สุดเสมอ", "ไม่มีเสียงรบกวนไม่ได้เลย", "ใช้เสียงเดียวทุกฉาก"),
                    QuizQuestion("Export คือขั้นตอนใด?", "สร้างไฟล์วิดีโอผลลัพธ์", "วาดภาพบนกระดาษ", "เขียนโค้ด HTML", "เลือกกลุ่มเป้าหมาย"),
                    QuizQuestion("เหตุใดจึงควรจัดไฟล์ในโปรเจกต์ให้เป็นระบบ?", "ค้นหาและแก้ไขงานได้ง่าย", "ทำให้ภาพมีสีมากขึ้น", "เพิ่มความยาววิดีโอ", "ทำให้เสียงดังขึ้น")
                },
                _ => Enumerable.Range(1, 10).Select(_ => QuizQuestion($"แนวคิดสำคัญของคอร์ส {category} คืออะไร?", "นำความรู้ไปประยุกต์ใช้กับงานจริง", "ไม่ต้องวางแผน", "เลือกวิธีแบบสุ่ม", "ไม่ต้องตรวจผลงาน")).ToArray()
            };

            return new PostCourseQuizViewModel { CategoryName = category, Questions = questions };
        }

        private static PostCourseQuizQuestionViewModel QuizQuestion(string question, params string[] options)
        {
            return new PostCourseQuizQuestionViewModel
            {
                Question = question,
                Options = options,
                CorrectOptionIndex = 0
            };
        }

        private static void ConfigurePracticeContent(LessonContentViewModel content)
        {
            var practice = content.CategoryName switch
            {
                "Graphic Design" => new[]
                {
                    ("องค์ประกอบใดช่วยนำสายตาและแบ่งสัดส่วนเนื้อหาในการออกแบบ?", new[] { "เส้น (Line)", "ฐานข้อมูล", "เฟรมเรต", "คีย์ลัด", "เซิร์ฟเวอร์" }),
                    ("หลักการใดใช้สร้างความโดดเด่นด้วยการจับคู่องค์ประกอบที่ตรงข้ามกัน?", new[] { "Contrast", "Proxy", "DNS", "Rigging", "Serialization" }),
                    ("หลักการ Proximity ในการจัดเลย์เอาต์หมายถึงอะไร?", new[] { "จัดกลุ่มข้อมูลที่เกี่ยวข้องให้อยู่ใกล้กัน", "เพิ่มความสว่างของภาพ", "ใส่กระดูกให้โมเดล", "ลดความเร็ววิดีโอ", "เปลี่ยนชนิดฐานข้อมูล" }),
                    ("สีคู่ตรงข้ามบนวงล้อสีเหมาะกับการใช้งานแบบใด?", new[] { "เน้นให้เกิดความโดดเด่นและ Contrast สูง", "สร้างเสียงบรรยาย", "สร้างฐานข้อมูล", "ทำ Animation Loop", "บีบอัดวิดีโอ" }),
                    ("งานเว็บไซต์ควรใช้ขนาดตัวอักษรเนื้อหาหลักประมาณเท่าใด?", new[] { "15–25 px", "1–2 px", "80–100 px", "200–300 pt", "5–8 px" })
                },
                "3D & Animation" => new[]
                {
                    ("ปุ่มใดใช้สลับระหว่าง Object Mode และ Edit Mode ใน Blender?", new[] { "Tab", "Ctrl + R", "Shift + E", "F12", "Alt + Z" }),
                    ("Modifier ใดช่วยสะท้อนโมเดลอีกฝั่งให้สมมาตร?", new[] { "Mirror Modifier", "Proxy Modifier", "Color Modifier", "Audio Modifier", "DNS Modifier" }),
                    ("แสงชนิดใดกระจายออกทุกทิศทางจากจุดเดียว?", new[] { "Point Light", "Sun Light", "Area Light", "Spotlight", "HDRI" }),
                    ("การผูกกระดูกกับโมเดลแบบคำนวณน้ำหนักอัตโนมัติใช้คำสั่งใด?", new[] { "With Automatic Weights", "Shade Auto Smooth", "Project from View", "Make Cyclic", "Add Keyframe" }),
                    ("คำสั่งใดใช้บันทึกท่าทางเป็น Keyframe ในการทำแอนิเมชัน?", new[] { "I", "U", "K", "M", "N" })
                },
                "Video Editing" => new[]
                {
                    ("การตัดแบบใดทำให้เกิดการกระโดดข้ามเวลาในช็อตเดียวกัน?", new[] { "Jump Cut", "L-Cut", "Fade", "Match Cut", "Cross Cutting" }),
                    ("J-Cut มีลักษณะสำคัญอย่างไร?", new[] { "เสียงช็อตถัดไปมาก่อนภาพ", "ภาพค่อย ๆ จางเป็นสีดำ", "ตัดภาพทุกเฟรม", "เพิ่มความคมชัดเสียง", "กลับภาพซ้ายขวา" }),
                    ("ไฟล์ Proxy มีประโยชน์หลักอย่างไร?", new[] { "ช่วยให้ตัดต่อไฟล์ความละเอียดสูงได้ลื่นขึ้น", "เพิ่มจำนวนกล้อง", "สร้างซับอัตโนมัติ", "เปลี่ยนเสียงเป็นภาพ", "ลบ Timeline" }),
                    ("คีย์ลัด Ctrl/Cmd + B ใน CapCut ใช้ทำอะไร?", new[] { "Split แบ่งคลิป", "Export วิดีโอ", "เพิ่มเพลง", "สร้าง Marker", "เปิด Auto Captions" }),
                    ("เครื่องมือใดใน Premiere Pro ใช้ตัดแบ่งคลิปบน Timeline?", new[] { "Razor Tool (C)", "Type Tool (T)", "Selection Tool (V)", "Rate Stretch (R)", "Track Select (A)" })
                },
                "Digital Marketing" => new[]
                {
                    ("ข้อใดคือการทำ SEO?", new[] { "ปรับเว็บไซต์ให้ติดอันดับค้นหาแบบออร์แกนิก", "ส่งอีเมลอย่างเดียว", "ตัดต่อวิดีโอ", "สร้างโมเดล 3D", "วาด Wireframe" }),
                    ("ขั้นตอนใดของ Marketing Funnel คือลูกค้าตัดสินใจสั่งซื้อ?", new[] { "Conversion", "Awareness", "Consideration", "Retention", "Research" }),
                    ("CAC ใช้บอกข้อมูลใด?", new[] { "ต้นทุนในการหาลูกค้าใหม่หนึ่งคน", "จำนวนผู้ติดตาม", "ความเร็วเว็บไซต์", "จำนวนคีย์เวิร์ด", "จำนวนโพสต์" }),
                    ("ขั้นตอนใดเป็นการทำ Content Marketing ที่เหมาะสม?", new[] { "กำหนดเป้าหมาย ผลิตสม่ำเสมอ และวัดผล", "โพสต์โดยไม่วางแผน", "คัดลอกทุกโพสต์", "ละเลย KPI", "ไม่ดูข้อมูลผู้ชม" }),
                    ("การใส่คีย์เวิร์ดใน Bio และ Hashtags เป็นส่วนหนึ่งของอะไร?", new[] { "Social Media Optimization", "Database Design", "Motion Blur", "Wireframing", "HTTP Request" })
                },
                "UX/UI" => new[]
                {
                    ("UI หมายถึงอะไรในการออกแบบผลิตภัณฑ์ดิจิทัล?", new[] { "หน้าตาและการจัดวางที่ผู้ใช้มองเห็น", "การจัดเก็บฐานข้อมูล", "การตัดต่อวิดีโอ", "การตั้งค่าเซิร์ฟเวอร์", "การทำ SEO" }),
                    ("ขั้นตอนใดของ Design Thinking ใช้ทำความเข้าใจผู้ใช้อย่างลึกซึ้ง?", new[] { "Empathize", "Define", "Ideate", "Prototype", "Test" }),
                    ("Design System มีไว้เพื่ออะไร?", new[] { "กำหนดมาตรฐานองค์ประกอบให้สอดคล้องกัน", "เพิ่มความเร็วอินเทอร์เน็ต", "สร้างเสียงประกอบ", "จัดการฐานข้อมูล", "ทำให้ภาพเบลอ" }),
                    ("ปุ่มควรมีสถานะใดเพื่อให้ผู้ใช้รับรู้การโต้ตอบ?", new[] { "Normal, Hover, Pressed และ Disabled", "เฉพาะ Normal เท่านั้น", "เฉพาะ Disabled เท่านั้น", "เฉพาะ Loading เท่านั้น", "ไม่มีสถานะ" }),
                    ("ก่อนเริ่มวาด Wireframe ควรทำสิ่งใด?", new[] { "กำหนดเป้าหมายของธุรกิจและผู้ใช้", "เลือก Animation ก่อน", "สร้างฐานข้อมูลก่อนเสมอ", "ตั้งค่า DNS", "เรนเดอร์วิดีโอ" })
                },
                "Web Development" => new[]
                {
                    ("DNS มีหน้าที่หลักอะไร?", new[] { "แปลงชื่อโดเมนเป็น IP Address", "จัดรูปแบบ CSS", "ตัดต่อวิดีโอ", "สร้างภาพ 3D", "ส่งอีเมล" }),
                    ("ส่วนใดเป็นตัวกลางรับส่งข้อมูลระหว่าง Frontend และ Backend?", new[] { "API", "CSS", "Database เท่านั้น", "Monitor", "Keyboard" }),
                    ("Authentication แตกต่างจาก Authorization อย่างไร?", new[] { "ยืนยันว่าเป็นใคร เทียบกับกำหนดว่าทำอะไรได้บ้าง", "ทั้งสองอย่างคือการจัดสี", "ทั้งสองอย่างคือการทำ SEO", "ยืนยันไฟล์ภาพเท่านั้น", "ใช้แทนกันได้เสมอ" }),
                    ("แท็ก HTML ใดใช้เก็บเนื้อหาที่แสดงบนหน้าจอ?", new[] { "body", "head", "meta", "link", "title" }),
                    ("คุณสมบัติใดของ Flexbox ใช้จัดองค์ประกอบตามแกนหลัก?", new[] { "justify-content", "align-items", "flex-wrap", "flex-basis", "order" })
                },
                _ => new[]
                {
                    ($"ข้อใดเป็นแนวทางสำคัญของบทเรียน {content.LessonTitle}?", new[] { "วิเคราะห์โจทย์และเลือกวิธีที่เหมาะสม", "ข้ามขั้นตอนทั้งหมด", "ไม่ตรวจสอบผลลัพธ์", "ใช้ข้อมูลโดยไม่วางแผน", "ละเลยเป้าหมายของงาน" })
                }
            };

            var lessonIndex = Math.Clamp(content.LessonNumber - 1, 0, practice.Length - 1);
            content.PracticeQuestions = practice
                .Select(item => new LessonContentViewModel.PracticeQuestionViewModel
                {
                    Question = item.Item1,
                    Options = item.Item2,
                    CorrectOptionIndex = 0
                })
                .ToArray();
            content.PracticeQuestion = practice[lessonIndex].Item1;
            content.PracticeOptions = practice[lessonIndex].Item2;
        }

        private static LessonContentViewModel CreateLessonContent(string category, CourseLessonViewModel lesson, string mode)
        {
            var content = new LessonContentViewModel
            {
                CategoryName = category,
                LessonNumber = lesson.Number,
                LessonTitle = lesson.Title,
                LessonDescription = lesson.Description,
                Mode = mode,
                ContentTitle = $"{lesson.Title}: พื้นฐานที่ควรรู้",
                Content = $"บทเรียนนี้จะพาคุณทำความเข้าใจแนวคิดสำคัญของ {category} ผ่านตัวอย่างที่นำไปใช้ได้จริง เริ่มจากการรู้จักองค์ประกอบหลัก แล้วค่อย ๆ ทดลองตามขั้นตอนเพื่อสร้างความเข้าใจที่ถูกต้องก่อนลงมือทำโปรเจกต์จริง",
                KeyPoints = new[]
                {
                    "ทำความเข้าใจหลักการสำคัญก่อนเลือกใช้เครื่องมือ",
                    "สังเกตตัวอย่างและวิเคราะห์เหตุผลของแต่ละขั้นตอน",
                    "ทดลองทำด้วยตัวเอง แล้วทบทวนผลลัพธ์ที่ได้"
                },
                PracticeOptions = new[]
                {
                    "เลือกวิธีที่อธิบายหลักการของบทเรียนได้ถูกต้องที่สุด",
                    "จัดลำดับขั้นตอนการทำงานให้เหมาะสม",
                    "วิเคราะห์ว่าควรนำความรู้นี้ไปใช้กับโจทย์ใด"
                }
            };

            ConfigurePracticeContent(content);

            if (category == "Graphic Design" && lesson.Number == 1 && mode == "read")
            {
                content.ContentTitle = "พื้นฐานการออกแบบกราฟิก (Graphic Design Fundamentals)";
                content.Content = """
                    <p>พื้นฐานการออกแบบคือหัวใจสำคัญของงานสื่อภาพทุกประเภท ตั้งแต่งานศิลปะ เว็บไซต์ ไปจนถึงการเลือกใช้ฟอนต์ แม้จะไม่ได้เป็นศิลปินโดยตรง แต่การเข้าใจองค์ประกอบพื้นฐานทั้ง 5 อย่างนี้ จะช่วยให้สร้างสรรค์และจัดวางงานออกแบบได้อย่างมืออาชีพมากขึ้น</p>
                    <h3>1. เส้น (Line)</h3>
                    <p><strong>คืออะไร:</strong> รูปร่างที่เชื่อมต่อจุดตั้งแต่ 2 จุดขึ้นไป สามารถปรับเปลี่ยนลักษณะได้หลากหลาย เช่น เส้นหนา เส้นบาง เส้นหยัก หรือเส้นคลื่น ซึ่งแต่ละแบบจะให้อารมณ์ความรู้สึกที่แตกต่างกัน</p>
                    <p><strong>นำไปใช้อย่างไร:</strong></p><ul><li>ใช้ในการวาดภาพประกอบ รวมถึงการสร้างพื้นผิวและลวดลาย</li><li>ใช้ในการจัดวางข้อความเพื่อเน้นย้ำ แบ่งสัดส่วน จัดระเบียบเนื้อหา และนำสายตาผู้ดู</li></ul>
                    <p><strong>ข้อควรคำนึง:</strong> น้ำหนัก สี พื้นผิว และสไตล์ของเส้น มีผลต่อความรู้สึกของงานโดยรวม</p>
                    <h3>2. รูปร่าง (Shape)</h3>
                    <p><strong>คืออะไร:</strong> พื้นที่ 2 มิติที่มีขอบเขตชัดเจน แบ่งออกเป็น 2 ประเภทหลัก คือ รูปทรงเรขาคณิต เช่น วงกลม สี่เหลี่ยม สามเหลี่ยม และรูปทรงอิสระหรือธรรมชาติ (Organic)</p>
                    <p><strong>นำไปใช้อย่างไร:</strong></p><ul><li>ช่วยสื่อสารไอเดียทางสายตา ทำให้ภาพดูมีน้ำหนักและจดจำได้ง่าย เช่น ป้ายจราจรหรือสัญลักษณ์ต่าง ๆ</li><li>ช่วยจัดระเบียบเนื้อหา แยกส่วนประกอบ หรือใช้สร้างภาพประกอบแบบง่าย ๆ</li></ul>
                    <h3>3. รูปทรง 3 มิติ (Form)</h3>
                    <p><strong>คืออะไร:</strong> การทำให้รูปร่าง 2 มิติกลายเป็น 3 มิติ ซึ่งอาจเป็นวัตถุจริงหรือการสร้างภาพลวงตาให้ดูมีมิติความลึกด้วยเทคนิคการใช้แสง เงา และมุมมอง (Perspective)</p>
                    <p><strong>นำไปใช้อย่างไร:</strong> ช่วยเพิ่มความสมจริงและมิติให้กับภาพ เช่น การใส่เงาให้วัตถุเพื่อสร้างมิติของการซ้อนทับกัน (Layers) หรือทำให้วัตถุดูมีที่ตั้งที่ชัดเจน</p>
                    <p><strong>ข้อควรระวัง:</strong> ควรใช้ในปริมาณที่พอเหมาะเพื่อไม่ให้งานดูรกจนเกินไป</p>
                    <h3>4. พื้นผิว (Texture)</h3>
                    <p><strong>คืออะไร:</strong> ลักษณะทางกายภาพของพื้นผิว ไม่ว่าจะเป็นพื้นผิวที่สัมผัสได้จริง หรือพื้นผิวที่สร้างขึ้นด้วยภาพเพื่อให้รู้สึกถึงความเรียบ ความหยาบ ความแข็ง หรือความนุ่ม</p>
                    <p><strong>นำไปใช้อย่างไร:</strong></p><ul><li>ช่วยเพิ่มมิติและความรู้สึกจับต้องได้ให้กับภาพที่แบนราบ</li><li>เหมาะสำหรับใช้เป็นภาพพื้นหลัง หรือแทรกในรายละเอียดเล็ก ๆ เช่น ฟอนต์ที่มีรอยถลอก หรือไอคอนที่ดูมันวาว</li></ul>
                    <p><strong>ข้อควรระวัง:</strong> ไม่ควรใส่พื้นผิวมากเกินไปในงานชิ้นเดียว เพราะจะทำให้ดูตาลายและรบกวนสายตา</p>
                    <h3>5. ความสมดุล (Balance)</h3>
                    <p><strong>คืออะไร:</strong> การกระจายน้ำหนักทางสายตาให้เท่ากัน โดยปัจจัยที่มีผลต่อน้ำหนักทางสายตา ได้แก่ สี ขนาด จำนวน และพื้นที่ว่าง (Negative Space)</p>
                    <p><strong>รูปแบบของความสมดุล:</strong></p><ul><li><strong>สมดุลแบบสมมาตร (Symmetrical):</strong> องค์ประกอบทั้งสองฝั่งของแกนกลางมีความเหมือนกันหรือคล้ายคลึงกัน</li><li><strong>สมดุลแบบอสมมาตร (Asymmetrical):</strong> องค์ประกอบทั้งสองฝั่งไม่เหมือนกัน แต่มีการกระจายน้ำหนักให้รู้สึกสมดุลและดึงดูดสายตาไปยังจุดที่ต้องการได้อย่างเหมาะสม</li></ul>
                    <p><strong>เทคนิคแนะนำ:</strong> กฎสามส่วน (Rule of Thirds) คือการแบ่งพื้นที่ทำงานเป็นตาราง 3x3 แล้ววางจุดเด่น (Focal Point) ไว้บนหรือใกล้กับเส้นตัด ซึ่งสอดคล้องกับธรรมชาติการกวาดสายตาของมนุษย์ ทำให้ภาพดูสมดุลและน่าดึงดูดมากยิ่งขึ้น</p>
                    <p>การเข้าใจพื้นฐานทั้ง 5 ข้อนี้ จะช่วยให้มองเห็นรายละเอียดเล็ก ๆ น้อย ๆ ที่ประกอบกันเป็นภาพรวมของงานออกแบบ และสามารถนำไปประยุกต์ใช้กับโปรเจกต์ได้ทุกรูปแบบ</p>
                    """;
            }
            else if (category == "Graphic Design" && lesson.Number == 2 && mode == "read")
            {
                content.ContentTitle = "หลักการออกแบบกราฟิก (Principles of Design)";
                content.Content = """
                    <p>เมื่อเรามีองค์ประกอบพื้นฐาน เช่น เส้น รูปร่าง สี และข้อความแล้ว เราสามารถนำมาประกอบกันอย่างมั่นใจได้ด้วย 8 หลักการออกแบบต่อไปนี้</p>
                    <h3>การเน้นจุดเด่น (Emphasis)</h3>
                    <p>การทำให้องค์ประกอบสำคัญโดดเด่นสะดุดตาเป็นสิ่งแรก เช่น การทำให้ใหญ่ขึ้น หนาขึ้น หรือสว่างกว่าส่วนอื่น ๆ โดยอาศัยเทคนิคอย่าง Contrast, Movement และ White Space</p>
                    <h3>ความแตกต่าง (Contrast)</h3>
                    <p>การจับคู่องค์ประกอบที่ตรงข้ามกันเพื่อสร้างความน่าสนใจและเน้นจุดสำคัญ เช่น สีเข้มตัดสีอ่อน พื้นผิวหยาบตัดพื้นเรียบ เส้นหนาตัดเส้นบาง หรือขนาดใหญ่ตัดขนาดเล็ก</p>
                    <h3>พื้นที่ว่าง (White Space)</h3>
                    <p>พื้นที่ที่ไม่มีองค์ประกอบใด ๆ วางอยู่ ไม่จำเป็นต้องเป็นสีขาวเสมอไป ทำหน้าที่เป็นพื้นที่หายใจ ป้องกันไม่ให้งานดูอึดอัด ช่วยจัดกลุ่มเนื้อหา ทำให้อ่านง่าย และช่วยขับเน้นจุดเด่นให้ชัดเจนขึ้น</p>
                    <h3>การเคลื่อนไหวของสายตา (Movement)</h3>
                    <p>การใช้เส้น สี หรือพื้นที่ว่าง ชี้นำสายตาผู้ชมให้กวาดมองไล่ไปตามลำดับที่ผู้ออกแบบต้องการ</p>
                    <h3>การทำซ้ำ (Repetition)</h3>
                    <p>การใช้องค์ประกอบเดิมซ้ำ ๆ เพื่อสร้างความสม่ำเสมอ สร้างลวดลาย (Pattern) หรือสร้างจังหวะทางสายตา (Visual Beat) ทำให้งานดูสะอาดและเป็นระเบียบ</p>
                    <h3>สัดส่วน (Proportion)</h3>
                    <p>ความสัมพันธ์ของขนาดและปริมาณของวัตถุแต่ละชิ้นเมื่อวางอยู่ด้วยกัน เพื่อให้ดูสมเหตุสมผลและไม่ดูเอียงหรือหนักไปด้านใดด้านหนึ่ง เช่น หนังสือควรใหญ่กว่าแก้วชา และแก้วชาควรใหญ่กว่าช้อน</p>
                    <h3>ความสมดุล (Balance)</h3>
                    <p>การจัดวางน้ำหนักทางสายตาขององค์ประกอบต่าง ๆ ให้เหมาะสม แบ่งเป็น 3 รูปแบบ:</p>
                    <ul><li><strong>สมดุลแบบสมมาตร (Symmetrical):</strong> ซ้ายขวาเหมือนกัน ให้ความรู้สึกมั่นคงและปลอดภัย แต่อาจดูเรียบเกินไป</li><li><strong>สมดุลแบบอสมมาตร (Asymmetrical):</strong> สองฝั่งไม่เหมือนกันแต่ถ่วงน้ำหนักกันได้พอดี ให้ความรู้สึกมีชีวิตชีวาและน่าสนใจกว่า</li><li><strong>จงใจให้ไม่สมดุล (Unbalanced):</strong> ใช้เมื่อต้องการสื่อความรู้สึกไม่มั่นคง น่ากังวล หรือตื่นเต้น</li></ul>
                    <h3>การจัดแนว (Alignment)</h3>
                    <p>การจัดวางองค์ประกอบให้มีแนวเส้นอ้างอิงที่เชื่อมโยงกัน เช่น ชิดขอบซ้าย ขวา บน ล่าง หรือจัดกึ่งกลางแกน X และ Y เพื่อแสดงความสัมพันธ์ของข้อมูลและทำให้งานดูเป็นมืออาชีพ ไม่กระจัดกระจาย</p>
                    """;
            }
            else if (category == "Graphic Design" && lesson.Number == 3 && mode == "read")
            {
                content.ContentTitle = "การจัดวางองค์ประกอบและเลย์เอาต์ (Layout & Composition)";
                content.Content = """
                    <p>การจัดเลย์เอาต์ที่ดีเปรียบเสมือนโครงสร้างหลักที่ทำให้คนดูเข้าใจเนื้อหาได้ง่ายและไม่หลงทาง ประกอบด้วย 5 หลักการสำคัญ ได้แก่</p>
                    <h3>ความใกล้ชิด (Proximity)</h3>
                    <p>การใช้ระยะห่างเพื่อบอกความสัมพันธ์ของข้อมูล สิ่งที่เกี่ยวข้องกันควรจัดกลุ่มให้อยู่ใกล้กัน และเว้นระยะห่างออกจากกลุ่มที่ไม่เกี่ยวข้องกัน เพื่อให้ผู้ชมกวาดสายตาแล้วเข้าใจโครงสร้างเนื้อหาได้ทันที</p>
                    <h3>พื้นที่ว่าง (White Space)</h3>
                    <p>การเว้นระยะห่างระหว่างบรรทัด ระหว่างย่อหน้า และขอบกระดาษ (Margins) เพื่อแบ่งสัดส่วนเนื้อหาให้ชัดเจนและทำให้งานดูไม่อึดอัด</p>
                    <h3>การจัดแนว (Alignment)</h3>
                    <p>การจัดเรียงกล่องข้อความและรูปภาพให้ตรงแนวกันอย่างสม่ำเสมอ ลองจินตนาการถึงเส้นตาราง Grid ที่มองไม่เห็น เพื่อความเป็นระเบียบและง่ายต่อการไล่สายตาอ่าน</p>
                    <h3>ความแตกต่างและลำดับชั้น (Contrast &amp; Hierarchy)</h3>
                    <p>การใช้ความแตกต่างของขนาด สี หรือความหนา เพื่อสร้างลำดับชั้นของการมองเห็น (Hierarchy) บอกให้ผู้อ่านรู้ว่าควรเริ่มอ่านตรงไหนก่อน และไล่สายตาไปจุดไหนต่อ โดยหัวข้อสำคัญที่สุดควรใหญ่และเด่นที่สุด</p>
                    <h3>การทำซ้ำ (Repetition)</h3>
                    <p>การคุมโทนและสไตล์ให้ไปในทิศทางเดียวกันตลอดทั้งชิ้นงาน เช่น ใช้ชุดสีเดิม หรือใช้รูปแบบหัวข้อแบบเดียวกันทุกหน้า เพื่อให้ผู้อ่านคาดเดารูปแบบได้และโฟกัสกับเนื้อหาได้ง่ายขึ้น</p>
                    """;
            }
            else if (category == "Graphic Design" && lesson.Number == 4 && mode == "read")
            {
                content.ContentTitle = "ทฤษฎีสีพื้นฐาน (Color Theory)";
                content.Content = """
                    <p>การเข้าใจเรื่องสีช่วยให้เลือกจับคู่สีได้อย่างมีประสิทธิภาพและสื่ออารมณ์ได้ตรงจุด</p>
                    <h3>โครงสร้างของสีและการปรับค่าสี</h3>
                    <p><strong>แม่สี (Primary):</strong> แดง เหลือง น้ำเงิน สามารถผสมกันได้สีขั้นที่ 2 (Secondary) เช่น ม่วง และผสมต่อได้สีขั้นที่ 3 (Tertiary)</p>
                    <p><strong>การปรับความสว่าง/มืด:</strong> เติมสีดำเรียกว่า Shade (สีเข้มขึ้น) และเติมสีขาวเรียกว่า Tint (สีอ่อนลง)</p>
                    <p><strong>อุณหภูมิสี:</strong> สีโทนร้อน เช่น แดง ส้ม เหลือง ให้ความรู้สึกมีพลังและอบอุ่น ส่วนสีโทนเย็น เช่น ฟ้า น้ำเงิน เขียว ให้ความรู้สึกสงบและผ่อนคลาย</p>
                    <h3>การจับคู่สีด้วยวงล้อสี (Color Wheel)</h3>
                    <p><strong>สีคู่ตรงข้าม (Complementary):</strong> สีที่อยู่ตรงข้ามกันบนวงล้อ เช่น แดง-เขียว เหลือง-ม่วง และส้ม-น้ำเงิน ให้ความตัดกันสูง เหมาะสำหรับใช้เน้นให้โดดเด่น</p>
                    <p><strong>สีโทนเดียว (Monochromatic):</strong> การใช้สีเดียวแต่ไล่ระดับความเข้ม-อ่อนด้วย Tint, Shade และ Tone ให้ความกลมกลืนและนุ่มนวล</p>
                    <p><strong>สีข้างเคียง (Analogous):</strong> สีที่อยู่ติดกันบนวงล้อ จับคู่กันได้ลงตัวและให้ความรู้สึกเชื่อมโยงเป็นเรื่องเดียวกัน</p>
                    <h3>การนำชุดสี (Color Palette) ไปใช้งาน</h3>
                    <p>กำหนดหน้าที่ของแต่ละสีให้ชัดเจน เช่น สีพื้นหลัง สีข้อความ และสีวัตถุ โดยสามารถดูดสีจากภาพถ่ายมาสร้างเป็นชุดสีได้</p>
                    <h3>โหมดสีและการเข้าถึง (Accessibility)</h3>
                    <p>ใช้โหมด RGB สำหรับงานหน้าจอดิจิทัล และใช้ CMYK สำหรับงานสิ่งพิมพ์ นอกจากนี้ควรคำนึงถึงค่าความตัดกันของสีที่ชัดเจน เพื่อให้ผู้ที่มีภาวะตาบอดสีสามารถอ่านและเข้าใจงานได้ง่าย</p>
                    """;
            }
            else if (category == "Graphic Design" && lesson.Number == 5 && mode == "read")
            {
                content.ContentTitle = "กฎเหล็ก 6 ข้อของการจัดวางตัวอักษร (Typography Golden Rules)";
                content.Content = """
                    <p>การจัดวางตัวอักษรที่ดีช่วยให้อ่านง่าย สบายตา และดูเป็นมืออาชีพ โดยมีกฎพื้นฐานดังนี้</p>
                    <h3>ขนาดตัวอักษรเนื้อหาหลัก (Body Text Size)</h3>
                    <ul><li>งานสิ่งพิมพ์ เช่น นิตยสาร ควรอยู่ที่ 10–12 pt และไม่ควรเล็กกว่า 9 pt ยกเว้นงานเฉพาะทางอย่างนามบัตร</li><li>งานเว็บไซต์ควรอยู่ที่ 15–25 px</li></ul>
                    <h3>ระยะห่างระหว่างบรรทัด (Leading)</h3>
                    <p>ควรตั้งค่าอยู่ที่ 120%–145% ของขนาดตัวอักษร เช่น ตัวอักษรขนาด 10 pt ควรเว้นระยะบรรทัด 12–14.5 pt เพื่อไม่ให้ตัวหนังสือเบียดกันจนอ่านยาก และควรเช็กระยะห่างระหว่างตัวอักษร (Tracking) ไม่ให้ชิดหรือห่างเกินไป</p>
                    <h3>ความยาวของบรรทัด</h3>
                    <p>ใน 1 บรรทัดควรมีจำนวนตัวอักษร รวมเว้นวรรค ประมาณ 45–90 ตัวอักษร เพื่อให้กวาดสายตาอ่านได้ต่อเนื่องไม่เมื่อยตา ยกเว้นการจัดหน้าแบบหลายคอลัมน์</p>
                    <h3>สัดส่วนขนาดหัวข้อ (Heading Hierarchy)</h3>
                    <ul><li>หัวข้อหลัก (Primary Heading) ควรมีขนาด 180%–200% ของเนื้อหาหลัก</li><li>หัวข้อรอง (Secondary Heading) ควรมีขนาด 130%–150% ของเนื้อหาหลัก</li></ul>
                    <h3>การจัดชิดซ้ายและห้ามตัดคำ (Alignment &amp; Hyphenation)</h3>
                    <p>หลีกเลี่ยงการตัดคำข้ามบรรทัดด้วยเครื่องหมายขีด (-) ในเนื้อหาหลัก เพราะทำให้งานดูไม่เรียบร้อยและอ่านสะดุด หากไม่แน่ใจว่าจะจัดวางข้อความแบบไหน ให้เลือกจัดชิดซ้าย (Left-aligned) เป็นหลัก และหลีกเลี่ยงการจัดกึ่งกลางกับเนื้อหายาว ๆ เพราะอ่านยาก</p>
                    <h3>ทดลองพิมพ์ก่อนเสมอ (Test Print)</h3>
                    <p>สำหรับงานสิ่งพิมพ์ ต้องพิมพ์ตัวอย่างออกมาดูขนาดจริงบนกระดาษเสมอ เพื่อเช็กความคมชัดและความสบายตาในการอ่านก่อนสั่งผลิตจำนวนมาก</p>
                    """;
            }
            else if (category == "3D & Animation" && lesson.Number == 1 && mode == "read")
            {
                content.ContentTitle = "พื้นฐานโปรแกรม Blender สำหรับผู้เริ่มต้น (Absolute Beginner Basics)";
                content.Content = """
                    <p>การเริ่มต้นใช้งาน Blender ให้คล่องตัวเริ่มจากการควบคุมมุมมอง การเข้าใจโหมดการทำงาน และคีย์ลัดพื้นฐาน</p>
                    <h3>1. การควบคุมมุมมอง (Navigation)</h3>
                    <ul><li><strong>ซูมเข้า-ออก:</strong> เลื่อนลูกกลิ้งเมาส์ (Middle Scroll Wheel)</li><li><strong>หมุนมุมมอง (Orbit):</strong> กดคลิกปุ่มลูกกลิ้งเมาส์ค้างไว้แล้วลาก</li><li><strong>เลื่อนฉาก (Pan):</strong> กด Shift + คลิกปุ่มลูกกลิ้งเมาส์ค้างไว้แล้วลาก</li><li><strong>สลับมุมมองมาตรฐาน (Orthographic):</strong> กดตัวเลขบน Numpad โดย 1 คือด้านหน้า 3 คือด้านขวา 7 คือด้านบน และ 9 คือด้านล่าง หรือกด Alt ค้างไว้แล้วคลิกลูกกลิ้งเมาส์ลาก</li></ul>
                    <h3>2. โหมดการทำงานหลัก (Modes)</h3>
                    <p><strong>Object Mode:</strong> ใช้จัดการวัตถุทั้งชิ้น เช่น ย้ายตำแหน่ง หมุน หรือปรับขนาดโดยรวม โดยกด Tab เพื่อสลับโหมด</p><p><strong>Edit Mode:</strong> ใช้แก้ไขโครงสร้างย่อยของวัตถุ ประกอบด้วยจุด (Vertices) เส้นขอบ (Edges) และพื้นผิว (Faces)</p>
                    <h3>3. คีย์ลัดสำคัญในการปั้นโมเดล (Essential Hotkeys)</h3>
                    <ul><li><strong>Shift + A:</strong> เพิ่มวัตถุพื้นฐานใหม่เข้ามาในฉาก เช่น Cube, Cylinder และ Sphere</li><li><strong>A:</strong> เลือกทั้งหมด (Select All) / <strong>Shift + D:</strong> คัดลอกวัตถุ (Duplicate)</li><li><strong>G:</strong> เคลื่อนย้าย (Grab) / <strong>R:</strong> หมุน (Rotate) / <strong>S:</strong> ย่อ-ขยาย (Scale) และสามารถกดตามด้วย X, Y, Z เพื่อล็อกแกนได้</li><li><strong>E:</strong> ดึงพื้นผิวหรือเส้นให้ยืดออก (Extrude)</li><li><strong>Ctrl + R:</strong> เพิ่มเส้นตัดรอบวัตถุเพื่อเพิ่มรายละเอียด (Loop Cut)</li><li><strong>Ctrl + B:</strong> ลบเหลี่ยมมุมให้โค้งมน (Bevel)</li></ul>
                    <p><strong>การปรับผิวเรียบ:</strong> คลิกขวาเลือก Shade Auto Smooth เพื่อทำให้พื้นผิวดูเนียนขึ้นโดยไม่ต้องเพิ่มจำนวนโพลีกอนให้หนักเครื่อง</p>
                    """;
            }
            else if (category == "3D & Animation" && lesson.Number == 2 && mode == "read")
            {
                content.ContentTitle = "การปั้นโมเดลและจัดการ UV เบื้องต้น (3D Modeling & UV Workflow)";
                content.Content = """
                    <p>กระบวนการปั้นโมเดลที่มีรายละเอียดซับซ้อน เช่น อาวุธในเกม สามารถทำได้ง่ายขึ้นด้วยการวิเคราะห์รูปทรงและใช้ Modifier ช่วยลดงานซ้ำซ้อน</p>
                    <h3>การเตรียมแบบร่าง (Reference &amp; Blocking)</h3><ul><li>นำภาพอ้างอิงใส่เข้ามาในมุมมองด้านหน้า (1) และจัดให้อยู่กึ่งกลางฉาก</li><li>วิเคราะห์โครงสร้างของวัตถุและลดทอนให้เป็นรูปทรงเรขาคณิตพื้นฐาน เช่น ใบดาบเริ่มจาก Plane และด้ามจับเริ่มจาก Cylinder</li><li>กด Alt + Z เพื่อเปิดโหมดโปร่งแสง (X-Ray) ทำให้มองเห็นภาพอ้างอิงทะลุผ่านโมเดล</li></ul>
                    <h3>การขึ้นรูปใบดาบและด้ามจับ</h3><ul><li>ใช้คำสั่งดึงจุด (Extend Vertices) ไล่ตามขอบของวัตถุตามภาพอ้างอิง</li><li>ใช้ Inset กด I เพื่อสร้างขอบสันคม แล้วดึงพื้นผิวตรงกลางออกมาให้เกิดความหนา</li><li>ใช้ Mirror Modifier เพื่อสะท้อนโมเดลอีกฝั่งให้สมมาตรกัน</li><li>ใช้ Knife Tool (K) สำหรับกรีดเส้นสร้างลวดลาย และใช้ Proportional Editing เพื่อปั้นส่วนโค้งเว้าของด้ามจับ</li></ul>
                    <h3>การคลี่พื้นผิว (UV Unwrapping)</h3><p>ก่อนใส่ลวดลายต้องคลี่โมเดล 3 มิติลงบนระนาบ 2 มิติ ในหน้าต่าง UV Editing โดยกด U แล้วเลือก Unwrap หากพื้นผิวแบนเกิดการยืดเบี้ยว ให้ปรับมุมมองเป็นด้านหน้าตรงแล้วกด U และเลือก Project from View</p>
                    """;
            }
            else if (category == "3D & Animation" && lesson.Number == 3 && mode == "read")
            {
                content.ContentTitle = "การจัดแสงใน Blender (Lighting Basics)";
                content.Content = """
                    <p>การจัดแสงที่ดีช่วยกำหนดอารมณ์ สร้างมิติ และดึงดูดสายตาไปยังจุดสำคัญ</p><h3>1. ประเภทของแหล่งกำเนิดแสง (Lighting Tools)</h3><ul><li><strong>Point Light:</strong> แสงกระจายออกทุกทิศทางจากจุดเดียว เหมาะกับหลอดไฟหรือเทียน</li><li><strong>Sun Light:</strong> แสงสว่างสม่ำเสมอทั่วฉาก เหมาะกับแสงอาทิตย์หรือแสงจันทร์กลางแจ้ง</li><li><strong>Spotlight:</strong> แสงพุ่งเป็นทรงกรวย เหมาะกับไฟฉายหรือไฟหน้ารถ</li><li><strong>Area Light:</strong> ให้เงาที่นุ่มนวล เหมาะกับแสงจากหน้าต่างหรือไฟสตูดิโอ</li><li><strong>Mesh Lighting:</strong> เปลี่ยนวัตถุให้เรืองแสงด้วย Emission Shader</li><li><strong>World Lighting (HDRI):</strong> ใช้ภาพสภาพแวดล้อม 360 องศาเพื่อสร้างแสงและเงาสะท้อนที่สมจริง</li></ul>
                    <h3>2. หลักการของแสง (Lighting Concepts)</h3><p>แสงจากด้านบนให้ความรู้สึกเป็นธรรมชาติ แสงจากด้านข้างหรือด้านหลังช่วยเน้นรูปทรงและพื้นผิว ส่วนแสงจากด้านล่างให้ความรู้สึกน่ากลัวหรือลึกลับ แหล่งกำเนิดแสงขนาดเล็กให้เงาคมชัด ส่วนแหล่งกำเนิดแสงขนาดใหญ่ให้เงานุ่มนวล แสงโทนอุ่นให้ความรู้สึกอบอุ่น และแสงโทนเย็นให้ความรู้สึกสงบ ลึกลับ หรือตึงเครียด</p>
                    <h3>3. เทคนิคการจัดแสงพื้นฐาน (Lighting Techniques)</h3><p><strong>Three-Point Lighting:</strong> ประกอบด้วย Key Light ไฟหลักสร้างมิติ, Fill Light ไฟเสริมลดความเข้มของเงา และ Rim/Back Light ไฟด้านหลังเพื่อตัดขอบวัตถุ นอกจากนี้ยังมี High-Key ที่สว่างทั่วถึง, Low-Key ที่มืดและเงาจัด และ Rembrandt ที่ให้แสงสามเหลี่ยมใต้ตาฝั่งเงา</p>
                    """;
            }
            else if (category == "3D & Animation" && lesson.Number == 4 && mode == "read")
            {
                content.ContentTitle = "การใส่กระดูกให้ตัวละครเบื้องต้น (Character Rigging)";
                content.Content = """
                    <p>Rigging คือการสร้างโครงกระดูก (Armature) และเชื่อมเข้ากับโมเดล เพื่อให้สามารถดัดท่าทาง (Pose) และทำแอนิเมชันได้</p><h3>การเตรียมตัวละคร</h3><p>ตรวจสอบให้ Object Origin และ 3D Cursor อยู่ที่กึ่งกลางพื้นโลกด้วย Shift + S แล้วเลือก Cursor to World Origin จากนั้นปรับขนาดตัวละครให้สมจริง</p><h3>การสร้างโครงกระดูก (Armature)</h3><p>กด Shift + A แล้วเลือก Armature และ Single Bone จากนั้นเปิดโหมด In Front เพื่อให้เห็นกระดูกทะลุผ่านโมเดล เข้าสู่ Edit Mode ใช้ E ต่อกระดูกจากลำตัวไปยังคอ ศีรษะ ไหล่ แขน และมือ</p><h3>การตั้งชื่อและทำสมมาตร</h3><p>ตั้งชื่อกระดูกฝั่งซ้ายลงท้ายด้วย .L เช่น arm.L และ hip.L จากนั้นเลือก Symmetrize เพื่อสร้างกระดูกฝั่งขวา (.R) อัตโนมัติ สร้าง Root Bone และเชื่อมกระดูกสะโพกด้วย Ctrl + P แล้วเลือก Keep Offset</p><h3>การผูกกระดูกเข้ากับโมเดล (Parenting)</h3><p>ใน Object Mode เลือกตัวละครก่อน แล้ว Shift คลิกเลือกกระดูกเป็นลำดับสุดท้าย กด Ctrl + P เลือก With Automatic Weights เพื่อคำนวณน้ำหนักการขยับ จากนั้นสลับไป Pose Mode ด้วย Ctrl + Tab เพื่อทดสอบการขยับ</p>
                    """;
            }
            else if (category == "3D & Animation" && lesson.Number == 5 && mode == "read")
            {
                content.ContentTitle = "เวิร์กโฟลว์ครบวงจร ปั้น-ใส่กระดูก-ทำแอนิเมชัน (Complete Animation Workflow)";
                content.Content = """
                    <p>ขั้นตอนการสร้างงานแอนิเมชันสั้น ๆ ตั้งแต่ขึ้นรูปทรง จนถึงการเรนเดอร์เป็นไฟล์วิดีโอ</p><h3>ขึ้นรูปทรงสมมาตรอย่างรวดเร็ว (Modeling)</h3><p>วาดเส้นโครงร่างด้านข้างเพียงครึ่งเดียวของวัตถุ แล้วใช้ Screw Modifier หมุนเส้นโครงร่างให้กลายเป็นวัตถุทรงกลม 3 มิติ จากนั้นใส่ Subdivision Surface เพิ่มความเนียน ตรวจสอบ Face Orientation หากขึ้นสีแดงให้กด Shift + N เพื่อกลับด้าน</p><h3>ใส่กระดูกและเช็กน้ำหนัก (Rigging &amp; Weight Paint)</h3><p>สร้างกระดูกเรียงตามแกนของวัตถุและผูกด้วย With Automatic Weights จากนั้นเข้า Weight Paint เพื่อตรวจสอบและปรับแต่งขอบเขตอิทธิพลของกระดูก</p><h3>การสร้างแอนิเมชัน (Keyframing &amp; Looping)</h3><p>ใน Pose Mode ปรับท่าทางตามเฟรมแล้วกด I เพื่อบันทึก Keyframe (Location &amp; Rotation) ใช้ Empty Object เป็นตัวควบคุมการเคลื่อนที่ และตั้งกราฟแบบวนซ้ำด้วย Shift + E</p><h3>การใส่วัสดุ จัดฉาก และเรนเดอร์</h3><p>กำหนด Material แยกตามส่วนประกอบ ปรับ Transmission ให้โปร่งใสและ Roughness ให้เงาวาว ตั้งมุมกล้องด้วย Ctrl + Alt + 0 บน Numpad จัดไฟ Area Light เปิด Motion Blur ตั้งค่า Output เป็น FFmpeg / MP4 แล้วกด Ctrl + F12 เพื่อเรนเดอร์วิดีโอ</p>
                    """;
            }
            else if (category == "Video Editing" && lesson.Number == 1 && mode == "read")
            {
                content.ContentTitle = "เทคนิคการตัดต่อพื้นฐาน 10 รูปแบบ (10 Essential Editing Cuts)";
                content.Content = """
                    <p>การตัดต่อคือการเล่าเรื่องผ่านจังหวะและรอยต่อของภาพ การเลือกใช้เทคนิคการตัด (Cut) ที่เหมาะสมจะช่วยกำหนดอารมณ์และความลื่นไหลของวิดีโอ</p>
                    <ul><li><strong>Standard Cut (Hard Cut):</strong> ตัดสลับจากช็อตหนึ่งไปอีกช็อตทันทีทั้งภาพและเสียง</li><li><strong>Jump Cut:</strong> ตัดช่วงกลางของช็อตเดียวกันออกเพื่อให้เวลาเกิดการกระโดด เหมาะกับ Vlog และ Talking Head</li><li><strong>Match Cut:</strong> เชื่อมสองช็อตด้วยความคล้ายของรูปทรง การเคลื่อนไหว หรือเสียง</li><li><strong>J-Cut:</strong> ให้เสียงของช็อตถัดไปดังขึ้นก่อนภาพตัดตาม</li><li><strong>L-Cut:</strong> ให้เสียงของช็อตปัจจุบันต่อเนื่องแม้ภาพเปลี่ยนไปแล้ว</li><li><strong>Fade:</strong> ค่อย ๆ เลือนจากหรือสู่สีพื้นเพื่อเปิดหรือปิดเรื่อง</li><li><strong>Dissolve (Cross Dissolve):</strong> ซ้อนและค่อย ๆ จางภาพเก่าเป็นภาพใหม่ มักสื่อการผ่านไปของเวลา</li><li><strong>Cutting on Action:</strong> ตัดระหว่างการเคลื่อนไหวเพื่อให้การเคลื่อนไหวต่อเนื่องข้ามช็อต</li><li><strong>Cross Cutting (Parallel Editing):</strong> ตัดสลับเหตุการณ์ที่เกิดพร้อมกันต่างสถานที่ เพื่อสร้างความตื่นเต้น</li><li><strong>Smash Cut:</strong> ตัดเปลี่ยนฉากอย่างกะทันหันและรุนแรงเพื่อสร้างความตกใจหรืออารมณ์ขัน</li></ul>
                    """;
            }
            else if (category == "Video Editing" && lesson.Number == 2 && mode == "read")
            {
                content.ContentTitle = "ศิลปะการเล่าเรื่องด้วยการตัดต่อ (Storytelling Through Cuts)";
                content.Content = """
                    <p>การตัดต่อที่ดีไม่ได้ขึ้นอยู่กับเอฟเฟกต์ที่ซับซ้อน แต่ขึ้นอยู่กับการควบคุมสายตาและจังหวะอารมณ์ของผู้ชม</p><h3>Flow Cut</h3><p>หากวัตถุหรือกล้องในช็อตแรกเคลื่อนที่ไปทางใด ช็อตถัดไปควรรับช่วงในทิศทางเดียวกัน เพื่อให้สายตาคนดูไหลลื่น</p><h3>ลำดับขนาดภาพ (Wide – Medium – Close Pattern)</h3><p>เริ่มจากภาพกว้างเพื่อบอกสถานที่ ภาพปานกลางเพื่อบอกเหตุการณ์ และภาพใกล้เพื่อเน้นอารมณ์หรือรายละเอียดสำคัญ</p><h3>Eye-trace</h3><p>เมื่อตัดสลับบุคคล ควรวางตำแหน่งดวงตาของตัวละครให้อยู่ในจุดเดิมของเฟรมเพื่อให้รอยต่อเป็นธรรมชาติ</p><h3>Pacing &amp; Rhythm</h3><p>ใช้ Fast Cuts เพื่อสร้างความตื่นเต้น กดดัน หรือความวุ่นวาย และใช้ Slow Cuts เพื่อให้ผู้ชมซึมซับอารมณ์</p>
                    """;
            }
            else if (category == "Video Editing" && lesson.Number == 3 && mode == "read")
            {
                content.ContentTitle = "เวิร์กโฟลว์การตัดต่อให้เร็วขึ้น 10 เท่า (Fast Editing Workflow)";
                content.Content = """
                    <p>เทคนิคการจัดระบบงานและปรับพฤติกรรมการทำงานเพื่อลดเวลาตัดต่อและเพิ่มคุณภาพงาน</p><h3>สร้างโปรเจกต์ต้นแบบ (The “Factory” Template)</h3><p>รวบรวม Title Cards, Sound Effects, กราฟิก เพลง และพรีเซ็ตสีไว้ในโปรเจกต์กลาง เมื่อต้องเริ่มงานใหม่ให้คัดลอกมาใช้เพื่อคุมโทนและประหยัดเวลา</p><h3>ใช้ไฟล์ Proxy</h3><p>สร้างไฟล์จำลองความละเอียดต่ำเมื่อตัดต่อไฟล์ 4K บนเครื่องที่ไม่แรง โดยตอน Export โปรแกรมจะดึงไฟล์ต้นฉบับมาเรนเดอร์</p><h3>ฟิลเตอร์สีม่วง (Purple Filter Rule)</h3><p>วาง Adjustment Layer สีม่วงจาง ๆ ทับช่วงที่ตัดเสร็จแล้ว เพื่อเตือนให้เดินหน้าตัดช่วงต่อไปก่อน แล้วค่อยตรวจภาพรวมตอนท้าย</p><h3>Custom Shortcuts</h3><p>ย้ายปุ่มลัดที่ใช้บ่อย เช่น ปุ่มเร่งความเร็วหรือปุ่มตัดคลิป มาไว้บนปุ่มเสริมของเมาส์หรือคีย์บอร์ดที่กดได้ถนัด</p>
                    """;
            }
            else if (category == "Video Editing" && lesson.Number == 4 && mode == "read")
            {
                content.ContentTitle = "คู่มือเริ่มต้นใช้งาน CapCut สำหรับ PC & Mac";
                content.Content = """
                    <p>CapCut เป็นโปรแกรมตัดต่อฟรีที่ใช้งานง่าย เหมาะสำหรับงานโซเชียลมีเดียและวิดีโอทั่วไป</p><h3>การตั้งค่าโปรเจกต์</h3><p>กด Modify เพื่อตั้งสัดส่วนภาพ เช่น 16:9 สำหรับ YouTube หรือ 9:16 สำหรับ TikTok/Reels ความละเอียด 1920x1080 และเฟรมเรต 24 หรือ 30 FPS</p><h3>การจัดการคลิปและคีย์ลัด</h3><ul><li>กด Ctrl/Cmd + B เพื่อ Split คลิป</li><li>กด Q เพื่อลบด้านหน้า Playhead และ W เพื่อลบด้านหลัง</li><li>กด M เพื่อปัก Marker</li><li>ใช้ Freeze, Reverse และ Mirror ได้จากแถบเครื่องมือ</li></ul><h3>Keyframe และ Speed Curve</h3><p>เพิ่ม Keyframe ที่ค่า Scale หรือ Position แล้วปรับค่าที่จุดสิ้นสุดเพื่อสร้างการเคลื่อนไหว ใช้ Speed Curve เพื่อสร้างจังหวะสโลว์โมชันสลับเร็ว</p><h3>เสียง ข้อความ และ Export</h3><p>เพิ่มเพลงจาก Audio ทำ Fade In/Out เพิ่ม Text และ Auto Captions ปรับสีด้วย Filters หรือ Adjustment ใช้ Transitions แล้วกำหนดช่วงด้วย I และ O ก่อนกด Export</p>
                    """;
            }
            else if (category == "Video Editing" && lesson.Number == 5 && mode == "read")
            {
                content.ContentTitle = "คู่มือเริ่มต้นใช้งาน Adobe Premiere Pro";
                content.Content = """
                    <p>Adobe Premiere Pro เป็นโปรแกรมตัดต่อมาตรฐานระดับมืออาชีพสำหรับงานที่ซับซ้อนและยืดหยุ่นสูง</p><h3>การจัดระเบียบไฟล์และสร้าง Sequence</h3><p>สร้าง Bin ใน Project Panel เพื่อแยกวิดีโอ ภาพ และเสียง จากนั้นลากไฟล์วิดีโอลง Timeline เพื่อสร้าง Sequence</p><h3>เครื่องมือพื้นฐาน</h3><ul><li><strong>Selection Tool (V):</strong> เลือก ย้าย และหดขยายคลิป</li><li><strong>Track Select Forward (A):</strong> เลือกคลิปทั้งหมดทางขวา</li><li><strong>Rate Stretch (R):</strong> เร่งหรือทำสโลว์โมชัน</li><li><strong>Razor (C):</strong> ตัดแบ่งคลิป</li><li><strong>Type Tool (T):</strong> พิมพ์และปรับแต่งข้อความ</li></ul><h3>Keyframe แบบนุ่มนวล</h3><p>เปิด Toggle Animation ที่ Scale หรือ Position แล้วเลือก Keyframe ทั้งคู่ คลิกขวาเลือก Ease In และ Ease Out</p><h3>การปรับสีและเสียง</h3><p>ใช้ Lumetri Color ปรับแสง สี และ Saturation ใช้ Remix Tool ปรับความยาวเพลง และเลือก Audio Gain เพื่อเพิ่มหรือลดระดับเสียง</p>
                    """;
            }
            else if (category == "Digital Marketing" && lesson.Number == 1 && mode == "read")
            {
                content.ContentTitle = "ภาพรวมการตลาดดิจิทัล (Digital Marketing Overview)";
                content.Content = """
                    <p>การตลาดดิจิทัลคือการทำการตลาดผ่าน Search Engine เว็บไซต์ โซเชียลมีเดีย อีเมล และแอปพลิเคชัน เพื่อเข้าถึงกลุ่มเป้าหมาย สร้างการรับรู้ เพิ่มความน่าเชื่อถือ และกระตุ้นการตัดสินใจซื้อ โดยเจาะกลุ่มตามพฤติกรรมได้แม่นยำกว่าการตลาดแบบดั้งเดิม</p>
                    <h3>6 เครื่องมือหลักของการตลาดดิจิทัล</h3><ul><li><strong>Content Marketing:</strong> สร้างบทความ วิดีโอ อินโฟกราฟิก หรือกรณีศึกษาที่มีประโยชน์ เพื่อสร้างความไว้วางใจ</li><li><strong>SEO:</strong> ปรับเว็บไซต์และเนื้อหาให้ติดอันดับการค้นหาแบบออร์แกนิก</li><li><strong>PPC:</strong> ลงโฆษณาออนไลน์และจ่ายเงินเมื่อมีคนคลิก</li><li><strong>Social Media Marketing:</strong> โปรโมตสินค้าและสร้างปฏิสัมพันธ์ผ่าน Facebook, Instagram, YouTube หรือ LinkedIn</li><li><strong>Email Marketing:</strong> ดูแลความสัมพันธ์กับลูกค้า แจ้งโปรโมชัน และกระตุ้นการซื้อซ้ำ</li><li><strong>Affiliate Marketing:</strong> ให้พันธมิตรช่วยโปรโมตสินค้าและแบ่งค่าคอมมิชชันเมื่อเกิดยอดขายหรือการสมัคร</li></ul>
                    """;
            }
            else if (category == "Digital Marketing" && lesson.Number == 2 && mode == "read")
            {
                content.ContentTitle = "การเชื่อมโยงช่องทางและวัดผลการตลาด (Marketing Funnel & Measurement)";
                content.Content = """
                    <p>การตลาดดิจิทัลจะได้ผลดีที่สุดเมื่อมองทุกช่องทางเป็นระบบที่ทำงานสอดประสานกันตลอดเส้นทางของผู้บริโภค (Customer Journey)</p><h3>โครงสร้างกรวยการตลาด</h3><ul><li><strong>Awareness:</strong> ลูกค้าเริ่มรู้จักแบรนด์ผ่านการค้นหา โซเชียลมีเดีย หรือโฆษณา</li><li><strong>Consideration:</strong> ลูกค้าอ่านบทความ ดูรีวิว หรือเปรียบเทียบข้อมูลบน Landing Page</li><li><strong>Conversion:</strong> ลูกค้าลงมือทำตามเป้าหมาย เช่น สั่งซื้อหรือกรอกข้อมูลติดต่อ</li><li><strong>Retention:</strong> ดูแลหลังการขายผ่านอีเมลหรือคอนเทนต์เพื่อให้เกิดการซื้อซ้ำ</li></ul><h3>ตัวชี้วัดและการปรับปรุง</h3><p>วัดผลให้ตรงเป้าหมาย เช่น Conversion Rate, Customer Acquisition Cost (CAC) ซึ่งคำนวณจากงบโฆษณาหารด้วยจำนวนลูกค้าใหม่ และยอดซื้อซ้ำ ใช้ A/B Testing เพื่อพัฒนาประสบการณ์จากข้อมูลจริง โฆษณาต้องตรงไปตรงมา ไม่กล่าวอ้างเกินจริง และขออนุญาตจัดเก็บข้อมูลส่วนบุคคลตามกฎหมาย</p>
                    """;
            }
            else if (category == "Digital Marketing" && lesson.Number == 3 && mode == "read")
            {
                content.ContentTitle = "การทำ SEO ให้ติดอันดับค้นหา (Search Engine Optimization)";
                content.Content = """
                    <p>SEO คือกระบวนการปรับปรุงเว็บไซต์เพื่อดึงดูดผู้เข้าชมคุณภาพสูงจากหน้าผลการค้นหา และสร้างโอกาสในการขายอย่างยั่งยืน</p><h3>ปัจจัยหลักที่ส่งผลต่ออันดับ SEO</h3><ul><li><strong>รากฐานเว็บไซต์:</strong> มีโดเมนที่เหมาะสม โฮสติ้งเสถียร โครงสร้างดี และโหลดเร็ว</li><li><strong>เนื้อหาคุณภาพ:</strong> ตอบโจทย์สิ่งที่ผู้คนค้นหา ไม่คัดลอกผู้อื่น และใช้ Keywords อย่างเป็นธรรมชาติ</li><li><strong>ลิงก์อ้างอิงคุณภาพ:</strong> ได้รับ Backlinks จากเว็บไซต์ที่น่าเชื่อถือและเกี่ยวข้อง โดยเน้นคุณภาพมากกว่าปริมาณ</li></ul><p>หัวใจสำคัญคืออย่าหมกมุ่นกับรายละเอียดทางเทคนิคเล็ก ๆ น้อย ๆ แต่ให้โฟกัสการสร้างระบบผลิตเนื้อหาที่ดีและลิงก์คุณภาพอย่างต่อเนื่องและขยายผลได้</p>
                    """;
            }
            else if (category == "Digital Marketing" && lesson.Number == 4 && mode == "read")
            {
                content.ContentTitle = "กลยุทธ์การตลาดด้วยเนื้อหา (Content Marketing Strategy)";
                content.Content = """
                    <p>Content Marketing เป็นหัวใจของ Inbound Marketing ซึ่งต่างจาก Outbound Marketing ที่เป็นโฆษณาแบบขัดจังหวะ โดยสร้างสิ่งที่ผู้บริโภคกำลังมองหาเพื่อให้ลูกค้าเข้าหาแบรนด์อย่างเป็นธรรมชาติ</p><h3>รูปแบบคอนเทนต์ยอดนิยม</h3><p>บล็อกหรือบทความ โซเชียลมีเดีย พอดแคสต์ วิดีโอ อีบุ๊ก และแอปพลิเคชันที่ให้ประโยชน์</p><h3>4 ขั้นตอนการเริ่มต้น</h3><ol><li><strong>กำหนดเป้าหมายและ KPI:</strong> ระบุว่าต้องการสร้างการรับรู้ กระตุ้นยอดขาย หรือสร้างความภักดี</li><li><strong>เลือกช่องทางและรูปแบบ:</strong> พิจารณาว่ากลุ่มเป้าหมายอยู่ที่ไหนและควรใช้บทความ รูปภาพ วิดีโอ หรือเสียง</li><li><strong>ผลิตและเผยแพร่อย่างสม่ำเสมอ:</strong> วางตารางตามทรัพยากรและอัปเดตเนื้อหาเก่า</li><li><strong>วัดผลและวิเคราะห์:</strong> ติดตามรายเดือนหรือรายไตรมาสเพื่อปรับกลยุทธ์</li></ol>
                    """;
            }
            else if (category == "Digital Marketing" && lesson.Number == 5 && mode == "read")
            {
                content.ContentTitle = "การปรับแต่งโซเชียลมีเดียให้มีประสิทธิภาพ (Social Media Optimization - SMO)";
                content.Content = """
                    <p>SMO คือการปรับแต่งบัญชีโซเชียลมีเดียและเนื้อหาที่แชร์ให้สอดคล้องกับแนวทางที่ดี เพื่อขยายการเข้าถึงและเปลี่ยนผู้ติดตามให้เป็นลูกค้า</p><h3>วิเคราะห์กลยุทธ์และคู่แข่ง</h3><p>กำหนดเป้าหมายให้ชัด สำรวจคอนเทนต์ของคู่แข่ง และดู Analytics ว่าโพสต์แบบใดมีส่วนร่วมสูง</p><h3>ใส่คีย์เวิร์ดในโปรไฟล์</h3><p>ใส่คำค้นหาหลักที่เกี่ยวข้องในชื่อบัญชี (Name), Bio และ Hashtags</p><h3>อัปเดตข้อมูลบัญชี</h3><p>ตรวจสอบรูปโปรไฟล์ ภาพปก เบอร์ติดต่อ และลิงก์เว็บไซต์ให้ครบถ้วน ตรงกัน และใช้งานได้จริง</p><h3>ปรับแต่งเนื้อหาที่แชร์</h3><p>เมื่อนำลิงก์จากเว็บไซต์ไปแชร์ ต้องตั้ง Title ให้น่าสนใจและดึงดูดให้คลิก</p><h3>ติดตามข้อมูลและปรับตัว</h3><p>เช็กสถิติอย่างสม่ำเสมอเพื่อดูว่าคอนเทนต์หรือช่วงเวลาโพสต์ใดทำงานดีที่สุด แล้วนำข้อมูลมาวางแผนต่อไป</p>
                    """;
            }
            else if (category == "UX/UI" && lesson.Number == 1 && mode == "read")
            {
                content.ContentTitle = "ความเข้าใจพื้นฐานเกี่ยวกับ UI และ UX (UI/UX Fundamentals)";
                content.Content = """
                    <p>การออกแบบผลิตภัณฑ์ดิจิทัลให้ประสบความสำเร็จต้องอาศัยการทำงานร่วมกันของ UI และ UX</p><p><strong>UI (User Interface):</strong> หน้าตาและการจัดวาง เช่น ปุ่ม สี ตัวอักษร ไอคอน และเลย์เอาต์</p><p><strong>UX (User Experience):</strong> ความรู้สึกและความสะดวกสบายของผู้ใช้ตลอดการใช้งาน ว่าใช้งานง่าย แก้ปัญหาได้จริง และบรรลุเป้าหมายโดยไม่สับสน</p><h3>กระบวนการทำงานพื้นฐาน</h3><ol><li><strong>User Research:</strong> พูดคุย สัมภาษณ์ และทำแบบสอบถามเพื่อเข้าใจความต้องการที่แท้จริง</li><li><strong>Information Architecture:</strong> จัดระเบียบข้อมูลและเมนูให้ค้นหาง่าย</li><li><strong>Wireframing:</strong> ร่างโครงสร้างหน้าจอและ User Flow ก่อนลงรายละเอียด</li><li><strong>Visual Design:</strong> เลือกสี ฟอนต์ และไอคอนให้สวย อ่านง่าย และสื่ออารมณ์</li><li><strong>Usability Testing:</strong> ให้ผู้ใช้จริงทดลอง Prototype แล้วนำข้อเสนอแนะมาปรับปรุง</li></ol>
                    """;
            }
            else if (category == "UX/UI" && lesson.Number == 2 && mode == "read")
            {
                content.ContentTitle = "กระบวนการคิดเชิงออกแบบ (Design Thinking Process)";
                content.Content = """
                    <p>Design Thinking คือกระบวนการแก้ปัญหาที่ยึดผู้ใช้เป็นศูนย์กลางและมีความยืดหยุ่น สามารถย้อนกลับไปปรับปรุงแต่ละขั้นตอนได้</p><ol><li><strong>Empathize:</strong> เข้าใจกลุ่มเป้าหมายผ่านการสัมภาษณ์ การสังเกต และการวิจัย</li><li><strong>Define:</strong> จัดกลุ่มข้อมูล ทำ Affinity Mapping, Empathy Map และ User Persona เพื่อสรุป Pain Points</li><li><strong>Ideate:</strong> ระดมความคิด สร้าง Mind Map, User Flow และ Low-Fidelity Wireframe</li><li><strong>Prototype:</strong> กำหนดระบบสี ตัวอักษร และสร้าง Interactive Prototype</li><li><strong>Test:</strong> ทดสอบกับผู้ใช้จริงเพื่อหาจุดติดขัดและนำฟีดแบ็กมาพัฒนาต่อ</li></ol>
                    """;
            }
            else if (category == "UX/UI" && lesson.Number == 3 && mode == "read")
            {
                content.ContentTitle = "ขั้นตอนการออกแบบ UI/UX และระบบการออกแบบ (Design System & Workflow)";
                content.Content = """
                    <p>การเปลี่ยนไอเดียให้กลายเป็นหน้าจอที่สมบูรณ์และเป็นระบบมีขั้นตอนมาตรฐาน</p><h3>User Flow และ Wireframing</h3><p>วาดเส้นทางตั้งแต่หน้าแรกจนบรรลุเป้าหมาย และออกแบบเลย์เอาต์แต่ละหน้าจอให้เข้าถึงฟังก์ชันสำคัญได้ง่าย</p><h3>Design System</h3><ul><li><strong>Colors:</strong> กำหนดสีพื้นหลัง ข้อความ Primary, Secondary และ Semantic Colors</li><li><strong>Typography:</strong> เลือกฟอนต์ที่อ่านง่ายและกำหนดขนาดมาตรฐาน</li><li><strong>Components:</strong> กำหนดสไตล์ปุ่ม ช่องกรอก การ์ด และไอคอนให้เป็นรูปแบบเดียวกัน</li></ul><h3>UI Design &amp; Copywriting</h3><p>จัดวางตามหลัก Visual Hierarchy, Contrast, Balance และ Simplicity พร้อมใช้ข้อความจริงแทนข้อความจำลอง</p>
                    """;
            }
            else if (category == "UX/UI" && lesson.Number == 4 && mode == "read")
            {
                content.ContentTitle = "หลักการออกแบบ UI เชิงลึก (Core UI Concepts & Details)";
                content.Content = """
                    <p>รายละเอียดเล็ก ๆ ช่วยให้หน้าจอใช้งานง่ายและดูเป็นมืออาชีพมากขึ้น</p><h3>Signifiers &amp; Affordances</h3><p>ออกแบบให้ผู้ใช้รู้ทันทีว่ากดได้หรือทำอะไรได้ เช่น ใส่กรอบรอบปุ่มที่เลือก หรือทำปุ่ม Disabled ให้สีจาง</p><h3>Hierarchy &amp; White Space</h3><p>วางข้อมูลสำคัญไว้ด้านบน ใช้ขนาดและสีที่โดดเด่น และใช้ระบบ 4-point Grid เพื่อจัดระยะห่างและจัดกลุ่มข้อมูล</p><h3>Dark Mode และเงา</h3><p>ใช้พื้นผิวของวัตถุด้านบนให้สว่างกว่าพื้นหลังและลด Saturation ส่วนโหมดสว่างควรใช้เงาที่ Opacity ต่ำและ Blur มากขึ้น</p><h3>States &amp; Micro-interactions</h3><p>ปุ่มควรมีสถานะปกติ Hover Pressed และ Disabled พร้อม Feedback ทุกการกระทำ เช่น แจ้งว่า “คัดลอกลิงก์แล้ว”</p><h3>Overlays</h3><p>หลีกเลี่ยงข้อความบนรูปภาพโดยตรง ควรใช้ Linear Gradient หรือ Blur รองรับเพื่อให้อ่านได้ชัด</p>
                    """;
            }
            else if (category == "UX/UI" && lesson.Number == 5 && mode == "read")
            {
                content.ContentTitle = "เทคนิคการร่างโครงสร้างหน้าเว็บลงบนกระดาษ (Sketching Wireframes)";
                content.Content = """
                    <p>ก่อนออกแบบในคอมพิวเตอร์ การร่างโครงสร้างบนกระดาษช่วยให้คิดได้เร็วและไม่ยึดติดกับรายละเอียด</p><h3>ตอบคำถาม 4 ข้อก่อนเริ่มวาด</h3><ol><li><strong>Business Goals:</strong> หน้าเว็บสร้างขึ้นเพื่ออะไร</li><li><strong>User Goals:</strong> ผู้ใช้เข้ามาเพราะต้องการอะไร</li><li><strong>Questions to Answer:</strong> ผู้ใช้มีข้อสงสัยอะไรบ้าง</li><li><strong>Emotions to Invoke:</strong> ต้องการให้ผู้ใช้รู้สึกอย่างไร</li></ol><h3>จุดตัดของเป้าหมาย (Intersecting Goal)</h3><p>สิ่งแรกที่ควรปรากฏคือจุดที่เป้าหมายธุรกิจและผู้ใช้มาบรรจบกัน ตามด้วยเนื้อหาที่ตอบข้อสงสัย</p><h3>Assets และ Sketching Symbols</h3><p>วางแผนพื้นที่ตามรูปภาพ วิดีโอ หรือแผนภาพที่มีจริง และใช้เส้นแทนข้อความ กล่องมีกากบาทแทนรูปภาพ และกล่องมีปุ่ม Play แทนวิดีโอ จากนั้นตรวจสอบย้อนกลับกับคำถามทั้ง 4 ข้อ</p>
                    """;
            }
            else if (category == "Web Development" && lesson.Number == 1 && mode == "read")
            {
                content.ContentTitle = "กลไกการทำงานของเว็บไซต์ (How the Web Works)";
                content.Content = """
                    <p>เมื่อเราพิมพ์ชื่อเว็บไซต์ลงในเบราว์เซอร์ ระบบเบื้องหลังจะทำงานเป็นวงจร Request &amp; Response</p><h3>การค้นหาที่อยู่ผ่าน DNS (DNS Lookup)</h3><p>DNS ทำหน้าที่แปลงชื่อโดเมนที่มนุษย์จำง่ายให้เป็น IP Address ของเซิร์ฟเวอร์ปลายทาง</p><h3>การส่งคำขอ (HTTP Request)</h3><p>เมื่อได้ IP Address แล้ว เบราว์เซอร์ (Client) จะส่งคำขอ เช่น HTTP GET Request ไปยังเซิร์ฟเวอร์</p><h3>การประมวลผลฝั่งเซิร์ฟเวอร์ (Server &amp; Database)</h3><p>เซิร์ฟเวอร์ตรวจสอบคำขอ และติดต่อฐานข้อมูลเพื่อดึงหรือบันทึกข้อมูล เช่น ข้อมูลสมาชิกหรือรายการสินค้า</p><h3>การส่งผลลัพธ์กลับ (HTTP Response &amp; Rendering)</h3><p>เซิร์ฟเวอร์ส่ง HTML, JSON หรือรูปภาพกลับมา แล้วเบราว์เซอร์ Render เป็นหน้าเว็บ</p><h3>ความปลอดภัยด้วย HTTPS</h3><p><strong>HTTP:</strong> รับส่งข้อมูลโดยไม่เข้ารหัส จึงเสี่ยงต่อการถูกดักจับแบบ Man-in-the-Middle</p><p><strong>HTTPS:</strong> ใช้ใบรับรอง SSL/TLS เข้ารหัสข้อมูลก่อนส่ง ทำให้ผู้ดักจับไม่สามารถอ่านข้อมูลได้</p>
                    """;
            }
            else if (category == "Web Development" && lesson.Number == 2 && mode == "read")
            {
                content.ContentTitle = "โครงสร้างสถาปัตยกรรมเว็บแอปพลิเคชัน (Frontend, Backend, API & Database)";
                content.Content = """
                    <p>เว็บแอปพลิเคชันยุคใหม่แบ่งหน้าที่ออกเป็น 4 ส่วนหลัก</p><h3>Frontend</h3><p>ส่วนที่ผู้ใช้มองเห็นและโต้ตอบ เช่น ปุ่ม ช่องค้นหา รูปภาพ และหน้าจอ พัฒนาด้วย HTML, CSS, JavaScript หรือ React และ Angular</p><h3>API</h3><p>ตัวกลางรับส่งข้อมูลระหว่างหน้าบ้านและหลังบ้าน รวมถึงเชื่อมบริการภายนอก เช่น ระบบพยากรณ์อากาศหรือชำระเงิน นิยมใช้ REST API และ GraphQL</p><h3>Backend</h3><p>สมองของระบบที่ประมวลผล Business Logic คำนวณราคา ตรวจสอบสิทธิ์ และส่งอีเมล พัฒนาด้วย Python, Node.js, Java, PHP หรือ C#</p><h3>Database</h3><p><strong>SQL:</strong> เก็บข้อมูลเป็นตารางที่มีความสัมพันธ์ เช่น MySQL และ PostgreSQL</p><p><strong>NoSQL:</strong> เก็บข้อมูลแบบ Collections/Documents มีความยืดหยุ่น เช่น MongoDB</p>
                    """;
            }
            else if (category == "Web Development" && lesson.Number == 3 && mode == "read")
            {
                content.ContentTitle = "ภาพรวมการพัฒนาเว็บครบวงจร (Web Development Stack & Infrastructure)";
                content.Content = """
                    <p>การพัฒนาเว็บวิวัฒนาการจาก Web 1.0 ที่แสดงข้อมูลนิ่ง สู่ Web 2.0 ที่ผู้ใช้โต้ตอบได้ และ Web 3.0 หรือระบบที่ขับเคลื่อนด้วย AI</p><h3>การรับส่งข้อมูลและ Backend</h3><p>ข้อมูลจาก API มักอยู่ในรูป JSON และผ่านกระบวนการ Serialization ส่วน ORM ช่วยจัดการฐานข้อมูลโดยไม่ต้องเขียน SQL ดิบทั้งหมด</p><h3>ความปลอดภัยบนเว็บ</h3><p><strong>Authentication:</strong> ตรวจสอบว่าเป็นใคร เช่น การล็อกอิน และมอบ Token สำหรับคำขอครั้งถัดไป</p><p><strong>Authorization:</strong> ควบคุมสิทธิ์ของผู้ใช้แต่ละระดับ เช่น Admin แก้ไขข้อมูลได้ แต่ User ทั่วไปดูได้อย่างเดียว</p><h3>เซิร์ฟเวอร์และ Cloud Computing</h3><p>เว็บไซต์ทำงานบนเซิร์ฟเวอร์ใน Data Center ควบคุมผ่าน SSH และใช้ Reverse Proxy คัดกรองพอร์ต ปัจจุบันนิยมใช้ Cloud เช่น AWS และ DigitalOcean หรือบริการ Firebase ที่จัดการฐานข้อมูลและล็อกอินให้</p>
                    """;
            }
            else if (category == "Web Development" && lesson.Number == 4 && mode == "read")
            {
                content.ContentTitle = "พื้นฐานภาษา HTML (HTML Essentials)";
                content.Content = """
                    <p>HTML คือภาษาหลักที่ใช้สร้างโครงสร้างและเนื้อหาของหน้าเว็บ เปรียบเสมือนคู่มือให้เบราว์เซอร์รู้ว่าต้องแสดงข้อความ รูปภาพ หรือไฟล์ใด</p><h3>โครงสร้างพื้นฐานของไฟล์ HTML</h3><ul><li><strong>&lt;html&gt;:</strong> แท็กครอบโค้ดทั้งหมด</li><li><strong>&lt;head&gt;:</strong> เก็บ title, meta สำหรับ SEO และลิงก์ CSS</li><li><strong>&lt;body&gt;:</strong> เก็บเนื้อหาที่แสดงบนหน้าจอ</li></ul><h3>แท็กและองค์ประกอบที่พบบ่อย</h3><p>ใช้ &lt;h1&gt; ถึง &lt;h6&gt; สำหรับหัวข้อ, &lt;p&gt; สำหรับย่อหน้า, &lt;strong&gt; ตัวหนา, &lt;em&gt; ตัวเอียง, &lt;img&gt; สำหรับรูปภาพ และ &lt;a&gt; สำหรับลิงก์</p><p>รายการใช้ &lt;ol&gt;, &lt;ul&gt;, &lt;li&gt; และปุ่มใช้ &lt;button&gt; ฟอร์มใช้ &lt;form&gt; คู่กับ input, select และ label</p><h3>Global Attributes และ Semantic HTML</h3><p>class ใช้จัดกลุ่มเพื่อ CSS หรือ JavaScript ส่วน id เป็นชื่อเฉพาะ Semantic HTML เช่น header, nav, article และ footer ช่วยให้โค้ดอ่านง่ายและดีต่อ SEO</p>
                    """;
            }
            else if (category == "Web Development" && lesson.Number == 5 && mode == "read")
            {
                content.ContentTitle = "การจัดวางเลย์เอาต์ด้วย CSS Flexbox (CSS Flexbox Layout)";
                content.Content = """
                    <p>Flexbox ช่วยจัดเรียงตำแหน่งและระยะห่างขององค์ประกอบอย่างยืดหยุ่น โดยกำหนด <strong>display: flex;</strong> ให้กล่องแม่</p><h3>แกนหลักและแกนรอง</h3><p>ค่าเริ่มต้น flex-direction: row ทำให้ Main Axis อยู่แนวนอนและ Cross Axis อยู่แนวตั้ง หากใช้ column แกนจะสลับกัน</p><h3>คุณสมบัติกล่องแม่</h3><ul><li><strong>justify-content:</strong> จัดตามแกนหลัก</li><li><strong>align-items:</strong> จัดตามแกนรอง</li><li><strong>flex-wrap:</strong> อนุญาตให้ขึ้นบรรทัดใหม่</li><li><strong>gap:</strong> กำหนดช่องว่างระหว่างกล่อง</li></ul><h3>คุณสมบัติกล่องลูก</h3><ul><li><strong>flex-grow:</strong> สัดส่วนการขยาย</li><li><strong>flex-shrink:</strong> อัตราการหดตัว</li><li><strong>flex-basis:</strong> ขนาดเริ่มต้น</li><li><strong>align-self:</strong> จัดตำแหน่งเฉพาะชิ้น</li><li><strong>order:</strong> สลับลำดับการแสดงผล</li></ul>
                    """;
            }

            switch (mode)
            {
                case "listen":
                    content.ModeLabel = "ฟัง";
                    content.ContentTitle = $"เสียงบรรยาย: {lesson.Title}";
                    if (category == "Graphic Design" && lesson.Number >= 1 && lesson.Number <= 5)
                    {
                        content.AudioUrl = $"~/audio/บท {lesson.Number} GD.mp3";
                        content.LearningModeNotice = $"เสียงบรรยายสำหรับบทเรียน Graphic Design บทที่ {lesson.Number}";
                    }
                    else if (category == "3D & Animation" && lesson.Number >= 1 && lesson.Number <= 5)
                    {
                        content.AudioUrl = $"~/audio/บท{lesson.Number} 3D.mp3";
                        content.LearningModeNotice = $"เสียงบรรยายสำหรับบทเรียน 3D & Animation บทที่ {lesson.Number}";
                    }
                    else if (category == "Video Editing" && lesson.Number >= 1 && lesson.Number <= 5)
                    {
                        content.AudioUrl = lesson.Number <= 3
                            ? $"~/audio/บท{lesson.Number} VE.mp3"
                            : $"~/audio/บท {lesson.Number} VE.mp3";
                        content.LearningModeNotice = $"เสียงบรรยายสำหรับบทเรียน Video Editing บทที่ {lesson.Number}";
                    }
                    else if (category == "Digital Marketing" && lesson.Number >= 1 && lesson.Number <= 5)
                    {
                        content.AudioUrl = $"~/audio/บท{lesson.Number} DM.mp3";
                        content.LearningModeNotice = $"เสียงบรรยายสำหรับบทเรียน Digital Marketing บทที่ {lesson.Number}";
                    }
                    else if (category == "UX/UI" && lesson.Number >= 1 && lesson.Number <= 5)
                    {
                        content.AudioUrl = $"~/audio/บท{lesson.Number} UX.mp3";
                        content.LearningModeNotice = $"เสียงบรรยายสำหรับบทเรียน UX/UI บทที่ {lesson.Number}";
                    }
                    else if (category == "Web Development" && lesson.Number >= 1 && lesson.Number <= 5)
                    {
                        content.AudioUrl = $"~/audio/บท{lesson.Number} WD.mp3";
                        content.LearningModeNotice = $"เสียงบรรยายสำหรับบทเรียน Web Development บทที่ {lesson.Number}";
                    }
                    else
                    {
                        content.LearningModeNotice = "พื้นที่เสียงบรรยายพร้อมใช้งาน สามารถเพิ่มไฟล์เสียงจริงได้ภายหลัง";
                    }
                    break;
                case "watch":
                    content.ModeLabel = "ดู";
                    content.ContentTitle = $"วิดีโอสอน: {lesson.Title}";
                    if (category == "Graphic Design" && lesson.Number == 1)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/YqQx75OPRa0?rel=0";
                        content.LearningModeNotice = "วิดีโอพื้นฐานสำหรับบทเรียน Graphic Design";
                    }
                    else if (category == "Graphic Design" && lesson.Number == 2)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/UmHMVU6dceA?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน Graphic Design ตอนที่ 2";
                    }
                    else if (category == "Graphic Design" && lesson.Number == 3)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/a5KYlHNKQB8?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน Graphic Design ตอนที่ 3";
                    }
                    else if (category == "Graphic Design" && lesson.Number == 4)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/7iY4QFqTlpE?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน Graphic Design ตอนที่ 4";
                    }
                    else if (category == "Graphic Design" && lesson.Number == 5)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/za8ERlMchUE?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน Graphic Design ตอนที่ 5";
                    }
                    else if (category == "Graphic Design" && lesson.Number == 6)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/3NBKRywEbNs?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน Graphic Design ตอนที่ 6";
                    }
                    else if (category == "Video Editing" && lesson.Number == 1)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/YtULPT1aBWM?rel=0";
                        content.LearningModeNotice = "วิดีโอพื้นฐานสำหรับบทเรียน Video Editing";
                    }
                    else if (category == "Video Editing" && lesson.Number == 2)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/J0ydlAqg6_8?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน Video Editing ตอนที่ 2";
                    }
                    else if (category == "Video Editing" && lesson.Number == 3)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/aEYZ76Mf2Zw?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน Video Editing ตอนที่ 3";
                    }
                    else if (category == "Video Editing" && lesson.Number == 4)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/ImKkcOE3y70?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน Video Editing ตอนที่ 4";
                    }
                    else if (category == "Video Editing" && lesson.Number == 5)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/oUlpbue-Gw0?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน Video Editing ตอนที่ 5";
                    }
                    else if (category == "Digital Marketing" && lesson.Number == 1)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/bixR-KIJKYM?rel=0";
                        content.LearningModeNotice = "วิดีโอพื้นฐานสำหรับบทเรียน Digital Marketing";
                    }
                    else if (category == "Digital Marketing" && lesson.Number == 2)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/IIhMb550ohY?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน Digital Marketing ตอนที่ 2";
                    }
                    else if (category == "Digital Marketing" && lesson.Number == 3)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/6TPJT8HGUUw?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน Digital Marketing ตอนที่ 3";
                    }
                    else if (category == "Digital Marketing" && lesson.Number == 4)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/TkskQD1Lt4A?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน Digital Marketing ตอนที่ 4";
                    }
                    else if (category == "Digital Marketing" && lesson.Number == 5)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/tUqvU4XOadE?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน Digital Marketing ตอนที่ 5";
                    }
                    else if (category == "Web Development" && lesson.Number == 1)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/PANUQGHgxCI?rel=0";
                        content.LearningModeNotice = "วิดีโอพื้นฐานสำหรับบทเรียน Web Development";
                    }
                    else if (category == "Web Development" && lesson.Number == 2)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/NzEYYemQ3_8?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน Web Development ตอนที่ 2";
                    }
                    else if (category == "Web Development" && lesson.Number == 3)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/5YDVJaItmaY?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน Web Development ตอนที่ 3";
                    }
                    else if (category == "Web Development" && lesson.Number == 4)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/salY_Sm6mv4?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน Web Development ตอนที่ 4";
                    }
                    else if (category == "Web Development" && lesson.Number == 5)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/phWxA89Dy94?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน Web Development ตอนที่ 5";
                    }
                    else if (category == "UX/UI" && lesson.Number == 1)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/ODpB9-MCa5s?rel=0";
                        content.LearningModeNotice = "วิดีโอพื้นฐานสำหรับบทเรียน UX/UI";
                    }
                    else if (category == "UX/UI" && lesson.Number == 2)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/1-uhXmKpS4k?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน UX/UI ตอนที่ 2";
                    }
                    else if (category == "UX/UI" && lesson.Number == 3)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/wIuVvCuiJhU?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน UX/UI ตอนที่ 3";
                    }
                    else if (category == "UX/UI" && lesson.Number == 4)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/EcbgbKtOELY?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน UX/UI ตอนที่ 4";
                    }
                    else if (category == "UX/UI" && lesson.Number == 5)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/ciL6FxkoLsw?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน UX/UI ตอนที่ 5";
                    }
                    else if (category == "3D & Animation" && lesson.Number == 1)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/peSv5IT5Ve4?rel=0";
                        content.LearningModeNotice = "วิดีโอพื้นฐานสำหรับบทเรียน 3D & Animation";
                    }
                    else if (category == "3D & Animation" && lesson.Number == 2)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/vq8S_0p_tZU?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน 3D & Animation ตอนที่ 2";
                    }
                    else if (category == "3D & Animation" && lesson.Number == 3)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/XcJyW5iBKlU?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน 3D & Animation ตอนที่ 3";
                    }
                    else if (category == "3D & Animation" && lesson.Number == 4)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/2nlMZx0vp6E?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน 3D & Animation ตอนที่ 4";
                    }
                    else if (category == "3D & Animation" && lesson.Number == 5)
                    {
                        content.MediaUrl = "https://www.youtube.com/embed/xb9TBFwxNFU?rel=0";
                        content.LearningModeNotice = "วิดีโอสำหรับบทเรียน 3D & Animation ตอนที่ 5";
                    }
                    else
                    {
                        content.LearningModeNotice = "พื้นที่วิดีโอการสอนพร้อมใช้งาน สามารถเพิ่มไฟล์วิดีโอจริงได้ภายหลัง";
                    }
                    break;
                case "practice":
                    content.ModeLabel = "ฝึกทำ";
                    content.ContentTitle = $"แบบฝึกหัด: {lesson.Title}";
                    content.Content = $"ลองประยุกต์ความรู้เรื่อง {lesson.Title} กับโจทย์ด้านล่าง เลือกคำตอบที่เหมาะสมที่สุด แล้วตรวจสอบความเข้าใจของคุณก่อนกดเรียนจบบทนี้";
                    break;
                default:
                    content.ModeLabel = "อ่าน";
                    content.ContentTitle = $"อ่านเนื้อหา: {lesson.Title}";
                    break;
            }

            return content;
        }

        private IActionResult RedirectToLocal(string? returnUrl, string fallbackAction = nameof(Index))
        {
            return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? Redirect(returnUrl)
                : RedirectToAction(fallbackAction);
        }

        private static PreLearningQuizViewModel CreatePreLearningQuiz(string? skill)
        {
            var category = string.IsNullOrWhiteSpace(skill) ? "3D & Animation" : skill.Trim();
            var questions = category switch
            {
                "Graphic Design" => new[]
                {
                    Question("หลักการใดช่วยให้ข้อความในงานออกแบบอ่านง่ายขึ้น?", "การเลือกฟอนต์และลำดับชั้นของข้อความ", "ใส่องค์ประกอบให้มากที่สุด", "ใช้สีทุกสีในงาน", 0),
                    Question("สีคู่ตรงข้ามบนวงล้อสีเรียกว่าอะไร?", "สีโทนเย็น", "สีคู่ตรงข้าม (Complementary)", "สีเอกรงค์", 1),
                    Question("โปรแกรมใดเหมาะกับการออกแบบงานกราฟิกแบบเวกเตอร์?", "Illustrator", "Notion", "Excel", 0)
                },
                "Video Editing" => new[]
                {
                    Question("Timeline ในโปรแกรมตัดต่อใช้ทำอะไร?", "จัดลำดับและช่วงเวลาของคลิป", "วาดภาพเวกเตอร์", "สร้างฐานข้อมูล", 0),
                    Question("การตัดภาพตามจังหวะเพลงช่วยเรื่องใดมากที่สุด?", "ความลื่นไหลของการเล่าเรื่อง", "ขนาดไฟล์ภาพนิ่ง", "ความละเอียดหน้าจอ", 0),
                    Question("โปรแกรมใดเหมาะกับการตัดต่อวิดีโอ?", "Premiere Pro", "Figma", "Power BI", 0)
                },
                "UX/UI" => new[]
                {
                    Question("UX ให้ความสำคัญกับอะไรเป็นหลัก?", "ประสบการณ์และความต้องการของผู้ใช้", "จำนวนสีในหน้าจอ", "ความยาวของโค้ด", 0),
                    Question("Wireframe ใช้ในขั้นตอนใดของการออกแบบ?", "วางโครงสร้างหน้าจอเบื้องต้น", "ตัดต่อเสียง", "สร้างโมเดล 3D", 0),
                    Question("Prototype มีประโยชน์อย่างไร?", "ทดสอบแนวคิดการใช้งานก่อนพัฒนาจริง", "บีบอัดไฟล์วิดีโอ", "วิเคราะห์ยอดขาย", 0)
                },
                "Web Development" => new[]
                {
                    Question("HTML ใช้สำหรับอะไร?", "กำหนดโครงสร้างเนื้อหาบนเว็บ", "จัดการฐานข้อมูลเท่านั้น", "ตัดต่อวิดีโอ", 0),
                    Question("CSS มีหน้าที่หลักคืออะไร?", "กำหนดรูปแบบและการจัดวางหน้าเว็บ", "ส่งอีเมล", "สร้างโมเดล 3D", 0),
                    Question("JavaScript ช่วยเพิ่มความสามารถใดให้เว็บไซต์?", "การโต้ตอบและพฤติกรรมแบบไดนามิก", "การพิมพ์เอกสาร", "การวาดภาพด้วยมือ", 0)
                },
                "Digital Marketing" => new[]
                {
                    Question("กลุ่มเป้าหมายมีความสำคัญต่อการตลาดอย่างไร?", "ช่วยวางสารและช่องทางให้ตรงคน", "ช่วยเพิ่มจำนวนสีของโลโก้", "ช่วยลดความละเอียดรูปภาพ", 0),
                    Question("SEO มีเป้าหมายหลักคืออะไร?", "เพิ่มโอกาสให้เว็บไซต์ค้นพบได้ง่ายขึ้น", "สร้างแอนิเมชัน 3D", "ตัดต่อเสียง", 0),
                    Question("ตัวชี้วัดใดใช้ดูว่าคนคลิกโฆษณาหรือไม่?", "CTR", "FPS", "RGB", 0)
                },
                "ทักษะอื่นๆ" => new[]
                {
                    Question("ก่อนเริ่มเรียนทักษะใหม่ ควรทำอะไร?", "กำหนดเป้าหมายและสำรวจพื้นฐานของตนเอง", "เรียนทุกหัวข้อพร้อมกัน", "ข้ามการฝึกปฏิบัติ", 0),
                    Question("วิธีใดช่วยให้เรียนรู้ได้ต่อเนื่อง?", "แบ่งเป้าหมายเป็นขั้นตอนเล็ก ๆ", "รอให้มีเวลาว่างเท่านั้น", "อ่านโดยไม่ทบทวน", 0),
                    Question("การทำโปรเจกต์ช่วยพัฒนาทักษะอย่างไร?", "เปลี่ยนความรู้ให้เป็นประสบการณ์จริง", "ทำให้ไม่ต้องเรียนพื้นฐาน", "ลดความจำเป็นในการฝึกฝน", 0)
                },
                _ => new[]
                {
                    Question("การขึ้นโมเดล 3D คืออะไร?", "การสร้างรูปทรงสามมิติ", "การเขียนบทความ", "การวิเคราะห์แคมเปญโฆษณา", 0),
                    Question("แสงและวัสดุช่วยอะไรในงาน 3D?", "ทำให้ภาพดูมีมิติและสมจริงขึ้น", "เพิ่มจำนวนหน้าเว็บ", "จัดกลุ่มข้อมูล", 0),
                    Question("โปรแกรมใดใช้สร้างงาน 3D ได้?", "Blender", "Google Analytics", "Figma", 0)
                }
            };

            var additionalQuestions = category switch
            {
                "Graphic Design" => new[]
                {
                    Question("Typography หมายถึงอะไร?", "การออกแบบและจัดการตัวอักษร", "การตัดต่อวิดีโอ", "การเขียนโปรแกรม", 0),
                    Question("หลักการใดช่วยสร้างลำดับความสำคัญให้เนื้อหา?", "Visual Hierarchy", "Frame Rate", "Database", 0)
                },
                "Video Editing" => new[]
                {
                    Question("การตัด (Cut) ในงานวิดีโอมีหน้าที่ใด?", "เชื่อมภาพหนึ่งไปยังอีกภาพหนึ่ง", "ปรับความสว่างหน้าจอ", "สร้างฐานข้อมูล", 0),
                    Question("Frame Rate ใช้บอกอะไร?", "จำนวนเฟรมที่แสดงต่อวินาที", "ขนาดของไฟล์เสียง", "จำนวนเลเยอร์ในภาพ", 0)
                },
                "UX/UI" => new[]
                {
                    Question("Usability หมายถึงอะไร?", "ความง่ายและสะดวกในการใช้งาน", "ความเร็วของอินเทอร์เน็ต", "จำนวนสีของหน้าจอ", 0),
                    Question("User Research ใช้เพื่ออะไร?", "ทำความเข้าใจผู้ใช้และปัญหาของผู้ใช้", "สร้างไฟล์วิดีโอ", "เขียนโค้ดฐานข้อมูล", 0)
                },
                "Web Development" => new[]
                {
                    Question("แท็กใดใช้สร้างลิงก์ใน HTML?", "<a>", "<img>", "<table>", 0),
                    Question("Responsive Web Design คืออะไร?", "เว็บไซต์ปรับรูปแบบตามขนาดหน้าจอ", "เว็บไซต์ที่ใช้ได้เฉพาะมือถือ", "เว็บไซต์ที่ไม่มีรูปภาพ", 0)
                },
                "Digital Marketing" => new[]
                {
                    Question("Conversion ในการตลาดดิจิทัลหมายถึงอะไร?", "การที่ผู้ใช้ทำสิ่งที่เราตั้งเป้าหมายไว้", "จำนวนผู้ติดตามทั้งหมด", "การออกแบบโลโก้", 0),
                    Question("Organic Traffic คืออะไร?", "ผู้เข้าชมที่มาจากผลการค้นหาหรือช่องทางธรรมชาติ", "ผู้เข้าชมจากโฆษณาเท่านั้น", "จำนวนโพสต์ที่เผยแพร่", 0)
                },
                "ทักษะอื่นๆ" => new[]
                {
                    Question("การคิดเชิงวิพากษ์ช่วยเรื่องใด?", "วิเคราะห์ข้อมูลและเหตุผลก่อนตัดสินใจ", "จำข้อมูลโดยไม่ตรวจสอบ", "ทำงานโดยไม่วางแผน", 0),
                    Question("การตั้งเป้าหมายแบบ SMART ควรมีลักษณะอย่างไร?", "ชัดเจน วัดผลได้ และมีกรอบเวลา", "กว้างและไม่กำหนดเวลา", "เปลี่ยนเป้าหมายทุกวัน", 0)
                },
                _ => new[]
                {
                    Question("การทำ UV Mapping ในงาน 3D ใช้ทำอะไร?", "กำหนดตำแหน่งพื้นผิวบนโมเดล", "เพิ่มจำนวนวัตถุในฉาก", "ตัดต่อเสียง", 0),
                    Question("Rendering คือขั้นตอนใด?", "ประมวลผลฉากให้กลายเป็นภาพหรือวิดีโอ", "สร้างโครงสร้าง HTML", "วิเคราะห์คำค้นหา", 0)
                }
            };

            questions = questions.Concat(additionalQuestions).ToArray();

            return new PreLearningQuizViewModel
            {
                CategoryName = category,
                Questions = questions
            };
        }

        private static PreLearningQuizQuestionViewModel Question(string text, string firstOption, string secondOption, string thirdOption, int correctOptionIndex)
        {
            return new PreLearningQuizQuestionViewModel
            {
                Text = text,
                Options = new[]
                {
                    firstOption,
                    secondOption,
                    thirdOption,
                    "การทำงานโดยไม่ต้องใช้หลักการของหัวข้อนี้",
                    "การใช้เครื่องมือโดยไม่ต้องเข้าใจพื้นฐาน"
                },
                CorrectOptionIndex = correctOptionIndex
            };
        }
    }
}
