namespace Skill_up.Models;

public sealed class AdminCoursesViewModel
{
    public IReadOnlyList<AdminCourseViewModel> Courses { get; set; } = [];
    public int TotalLearners { get; set; }
    public int ActiveCourses { get; set; }
    public int AverageCompletionRate { get; set; }
    public string QuizPassRate { get; set; } = "—";
}

public sealed class AdminCourseViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string CoverImage { get; set; } = string.Empty;
    public string Type { get; set; } = "Self-paced";
    public int LearnersEnrolled { get; set; }
    public string Status { get; set; } = "Active";
}

public sealed class AdminQuizViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int PassRate { get; set; }
    public int PassedAttempts { get; set; }
    public int Attempts { get; set; }
    public string Status { get; set; } = "Published";
}
