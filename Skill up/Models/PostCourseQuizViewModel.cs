namespace Skill_up.Models;

public class PostCourseQuizViewModel
{
    public string CategoryName { get; set; } = string.Empty;
    public IReadOnlyList<PostCourseQuizQuestionViewModel> Questions { get; set; } = Array.Empty<PostCourseQuizQuestionViewModel>();
    public bool IsChecked { get; set; }
    public int Score { get; set; }
    public string? ErrorMessage { get; set; }
}

public class PostCourseQuizQuestionViewModel
{
    public string Question { get; set; } = string.Empty;
    public IReadOnlyList<string> Options { get; set; } = Array.Empty<string>();
    public int CorrectOptionIndex { get; set; }
}
