namespace Skill_up.Models;

public sealed class AdminDashboardViewModel
{
    public int TotalLearners { get; set; }
    public int ActiveCourses { get; set; }
    public int AverageCompletionRate { get; set; }
    public string QuizPassRate { get; set; } = "—";
    public IReadOnlyList<string> EngagementLabels { get; set; } = [];
    public IReadOnlyList<int> EngagementData { get; set; } = [];
    public IReadOnlyList<string> CategoryLabels { get; set; } = [];
    public IReadOnlyList<int> CategoryData { get; set; } = [];
    public IReadOnlyList<AdminDashboardCourseViewModel> RecentCourses { get; set; } = [];
}

public sealed class AdminDashboardCourseViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Learners { get; set; }
    public int CompletionRate { get; set; }
    public string RecentActivity { get; set; } = string.Empty;
}
