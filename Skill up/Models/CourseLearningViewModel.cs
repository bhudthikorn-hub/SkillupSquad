namespace Skill_up.Models;

public class CourseLearningViewModel
{
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageName { get; set; } = string.Empty;
    public int ProgressPercent { get; set; }
    public bool IsEnrolled { get; set; }
    public int? LearningCourseId { get; set; }
    public IReadOnlyList<CourseLessonViewModel> Lessons { get; set; } = Array.Empty<CourseLessonViewModel>();
    public PostCourseQuizViewModel PostCourseQuiz { get; set; } = new();
}

public class CourseLessonViewModel
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}
