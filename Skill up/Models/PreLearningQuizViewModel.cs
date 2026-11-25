namespace Skill_up.Models;

public class PreLearningQuizViewModel
{
    public string CategoryName { get; set; } = string.Empty;
    public IReadOnlyList<PreLearningQuizQuestionViewModel> Questions { get; set; } = Array.Empty<PreLearningQuizQuestionViewModel>();
    public bool IsSubmitted { get; set; }
    public int Score { get; set; }
    public int PassingScore { get; set; } = 60;

    public bool Passed => Score >= PassingScore;
}

public class PreLearningQuizQuestionViewModel
{
    public string Text { get; set; } = string.Empty;
    public IReadOnlyList<string> Options { get; set; } = Array.Empty<string>();
    public int CorrectOptionIndex { get; set; }
}
