namespace Skill_up.Models;

public class LessonContentViewModel
{
    public class PracticeQuestionViewModel
    {
        public string Question { get; set; } = string.Empty;
        public IReadOnlyList<string> Options { get; set; } = Array.Empty<string>();
        public int CorrectOptionIndex { get; set; }
    }

    public string CategoryName { get; set; } = string.Empty;
    public int LessonNumber { get; set; }
    public string LessonTitle { get; set; } = string.Empty;
    public string LessonDescription { get; set; } = string.Empty;
    public string Mode { get; set; } = "read";
    public string ModeLabel { get; set; } = "อ่าน";
    public string ContentTitle { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string PracticeQuestion { get; set; } = "ข้อใดอธิบายสิ่งที่ได้เรียนรู้ได้ดีที่สุด?";
    public IReadOnlyList<PracticeQuestionViewModel> PracticeQuestions { get; set; } = Array.Empty<PracticeQuestionViewModel>();
    public bool PracticeChecked { get; set; }
    public int PracticeScore { get; set; }
    public int PracticeTotal => PracticeQuestions.Count;
    public IReadOnlyList<string> KeyPoints { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> PracticeOptions { get; set; } = Array.Empty<string>();
    public string? LearningModeNotice { get; set; }
    public string? MediaUrl { get; set; }
    public string? AudioUrl { get; set; }
}
