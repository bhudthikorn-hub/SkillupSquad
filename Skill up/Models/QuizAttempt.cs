namespace Skill_up.Models;

public class QuizAttempt
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public string Category { get; set; } = string.Empty;
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public bool IsPassed { get; set; }
    public DateTime AttemptedAtUtc { get; set; } = DateTime.UtcNow;
}
