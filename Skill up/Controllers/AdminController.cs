using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Skill_up.Data;
using Skill_up.Models;

namespace Skill_up.Controllers;

public class AdminController : Controller
{
    private readonly ISharedUserStore _sharedUserStore;
    private readonly ApplicationDbContext _db;

    public AdminController(ISharedUserStore sharedUserStore, ApplicationDbContext db)
    {
        _sharedUserStore = sharedUserStore;
        _db = db;
    }

    public async Task<IActionResult> Dashboard()
    {
        if (!IsAdmin()) return RedirectToHome();

        var totalLearners = await GetTotalLearnersAsync();

        var courses = await _db.Courses
            .AsNoTracking()
            .Include(course => course.LearningCourses)
            .OrderByDescending(course => course.CreatedAtUtc)
            .ToListAsync();

        var enrollments = await _db.LearningCourses
            .AsNoTracking()
            .Include(item => item.Course)
            .ToListAsync();

        var recentDays = Enumerable.Range(0, 7)
            .Select(offset => DateTime.UtcNow.Date.AddDays(-6 + offset))
            .ToList();

        var model = new AdminDashboardViewModel
        {
            TotalLearners = totalLearners,
            ActiveCourses = courses.Count(course => course.IsActive),
            AverageCompletionRate = enrollments.Count == 0
                ? 0
                : (int)Math.Round(enrollments.Average(item => Math.Clamp(item.ProgressPercent, 0, 100))),
            EngagementLabels = recentDays.Select(day => day.ToString("ddd")).ToList(),
            EngagementData = recentDays.Select(day => enrollments.Count(item => item.AddedAtUtc.ToLocalTime().Date == day)).ToList(),
            CategoryLabels = enrollments
                .GroupBy(item => string.IsNullOrWhiteSpace(item.Course.Category) ? "Other" : item.Course.Category)
                .OrderByDescending(group => group.Count())
                .Select(group => group.Key)
                .ToList(),
            CategoryData = enrollments
                .GroupBy(item => string.IsNullOrWhiteSpace(item.Course.Category) ? "Other" : item.Course.Category)
                .OrderByDescending(group => group.Count())
                .Select(group => group.Count())
                .ToList(),
            RecentCourses = courses.Take(6).Select(course =>
            {
                var courseEnrollments = course.LearningCourses.ToList();
                return new AdminDashboardCourseViewModel
                {
                    Name = course.Name,
                    Status = course.IsActive ? "Active" : "Archived",
                    Learners = courseEnrollments.Count,
                    CompletionRate = courseEnrollments.Count == 0
                        ? 0
                        : (int)Math.Round(courseEnrollments.Average(item => Math.Clamp(item.ProgressPercent, 0, 100))),
                    RecentActivity = courseEnrollments.Count == 0
                        ? "No enrollments yet"
                        : $"{courseEnrollments.Count} enrollment{(courseEnrollments.Count == 1 ? "" : "s")}"
                };
            }).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Courses(string? search = null, string? category = null)
    {
        if (!IsAdmin()) return RedirectToHome();

        var allCourses = await _db.Courses
            .AsNoTracking()
            .Include(course => course.LearningCourses)
            .OrderBy(course => course.Name)
            .ToListAsync();

        var enrollments = allCourses.SelectMany(course => course.LearningCourses).ToList();
        var quizAttempts = await _db.QuizAttempts
            .AsNoTracking()
            .ToListAsync();
        var totalLearners = await GetTotalLearnersAsync();
        var courses = allCourses.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            courses = courses.Where(course => course.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || course.Category.Contains(search, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(category)) courses = courses.Where(course => course.Category == category);

        ViewData["Search"] = search;
        ViewData["Category"] = category;
        ViewData["Categories"] = allCourses.Select(course => course.Category).Where(category => !string.IsNullOrWhiteSpace(category)).Distinct().Order().ToList();

        var model = new AdminCoursesViewModel
        {
            Courses = courses.Select(course => new AdminCourseViewModel
            {
                Id = course.Id.ToString(),
                Name = course.Name,
                Category = course.Category,
                CoverImage = GetCoverImage(course.Category),
                Type = course.Type,
                LearnersEnrolled = course.LearningCourses.Count,
                Status = course.IsActive ? "Active" : "Archived"
            }).ToList(),
            TotalLearners = totalLearners,
            ActiveCourses = allCourses.Count(course => course.IsActive),
            AverageCompletionRate = enrollments.Count == 0
                ? 0
                : (int)Math.Round(enrollments.Average(item => Math.Clamp(item.ProgressPercent, 0, 100))),
            QuizPassRate = quizAttempts.Count == 0
                ? "—"
                : $"{Math.Round(quizAttempts.Count(attempt => attempt.IsPassed) * 100d / quizAttempts.Count)}%"
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCourse(string? id, string name, string category, string type, string status)
    {
        if (!IsAdmin()) return RedirectToHome();

        Course? course = int.TryParse(id, out var courseId)
            ? await _db.Courses.FirstOrDefaultAsync(item => item.Id == courseId)
            : null;

        if (course is null)
        {
            _db.Courses.Add(new Course
            {
                Name = name.Trim(),
                Category = category.Trim(),
                Type = type,
                IsActive = string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)
            });
        }
        else
        {
            course.Name = name.Trim();
            course.Category = category.Trim();
            course.Type = type;
            course.IsActive = string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase);
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Courses));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCourse(string id)
    {
        if (!IsAdmin()) return RedirectToHome();

        if (int.TryParse(id, out var courseId))
        {
            var course = await _db.Courses.FirstOrDefaultAsync(item => item.Id == courseId);
            if (course is not null) _db.Courses.Remove(course);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Courses));
    }

    [HttpGet]
    public async Task<IActionResult> Quizzes(string? search = null)
    {
        if (!IsAdmin()) return RedirectToHome();

        var courses = await _db.Courses
            .AsNoTracking()
            .OrderBy(course => course.Name)
            .ToListAsync();
        var attempts = await _db.QuizAttempts
            .AsNoTracking()
            .ToListAsync();

        var categories = courses
            .Select(course => course.Category)
            .Concat(attempts.Select(attempt => attempt.Category))
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var quizzes = categories.Select(category =>
        {
            var categoryCourses = courses
                .Where(course => string.Equals(course.Category, category, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var categoryAttempts = attempts
                .Where(attempt => string.Equals(attempt.Category, category, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var courseName = categoryCourses.FirstOrDefault()?.Name ?? category;

            return new AdminQuizViewModel
            {
                Id = $"category:{category}",
                Name = $"{courseName} Assessment",
                CourseName = courseName,
                Category = category,
                PassRate = categoryAttempts.Count == 0
                    ? 0
                    : (int)Math.Round(categoryAttempts.Count(attempt => attempt.IsPassed) * 100d / categoryAttempts.Count),
                PassedAttempts = categoryAttempts.Count(attempt => attempt.IsPassed),
                Attempts = categoryAttempts.Count,
                Status = categoryCourses.Any(course => course.IsActive) ? "Published" : "Archived"
            };
        });

        if (!string.IsNullOrWhiteSpace(search))
        {
            quizzes = quizzes.Where(quiz => quiz.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || quiz.CourseName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || quiz.Category.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        ViewData["Search"] = search;
        return View(quizzes.ToList());
    }

    [HttpGet]
    public async Task<IActionResult> Users(string? search = null, string? status = null)
    {
        if (!IsAdmin()) return RedirectToHome();

        var users = _sharedUserStore.GetAll()
            .Where(user => !string.Equals(user.Email, "admin@skillup.com", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(user.Email, "admin@skillup.local", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(search))
        {
            users = users.Where(user => user.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || user.Email.Contains(search, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(status)) users = users.Where(user => user.Status == status);

        ViewData["Search"] = search;
        ViewData["Status"] = status;
        ViewData["TotalLearners"] = await GetTotalLearnersAsync();
        return View(users.ToList());
    }

    private async Task<int> GetTotalLearnersAsync()
    {
        var adminRoleId = await _db.Roles
            .Where(role => role.Name == "Admin")
            .Select(role => role.Id)
            .FirstOrDefaultAsync();

        return await _db.Users
            .CountAsync(user =>
                user.Email != "admin@skillup.com"
                && user.Email != "admin@skillup.local"
                && (string.IsNullOrEmpty(adminRoleId)
                    || !_db.UserRoles.Any(userRole => userRole.UserId == user.Id && userRole.RoleId == adminRoleId)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddUser(string name, string email, string careerGoal)
    {
        if (!IsAdmin()) return RedirectToHome();
        if (!string.Equals(email.Trim(), "admin@skillup.com", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(email.Trim(), "admin@skillup.local", StringComparison.OrdinalIgnoreCase))
        {
            _sharedUserStore.Add(name, email, careerGoal);
        }
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ToggleStatus(string id)
    {
        if (!IsAdmin()) return RedirectToHome();
        var user = _sharedUserStore.GetAll().FirstOrDefault(item => item.Id == id);
        if (user is not null) _sharedUserStore.UpdateStatus(id, user.Status == "Active" ? "Suspended" : "Active");
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(string id)
    {
        if (!IsAdmin()) return RedirectToHome();
        _sharedUserStore.Delete(id);
        return RedirectToAction(nameof(Users));
    }

    private static string GetCoverImage(string? category)
    {
        return category?.Trim().ToLowerInvariant() switch
        {
            "3d & animation" => "/3D & Animation.jpg",
            "ux/ui" => "/UX UI.jpg",
            "digital marketing" => "/Digital Marketing.jpg",
            "web development" => "/Web Development.jpg",
            "video editing" => "/Video Editing.jpg",
            "graphic design" => "/Graphic Design.jpg",
            _ => "/Homepic.png"
        };
    }

    private bool IsAdmin() => string.Equals(HttpContext.Session.GetString("UserRole"), "Admin", StringComparison.Ordinal);
    private IActionResult RedirectToHome() => RedirectToAction("Index", "Home")!;
}