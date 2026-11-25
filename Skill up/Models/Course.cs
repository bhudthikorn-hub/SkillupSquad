using System.ComponentModel.DataAnnotations;

namespace Skill_up.Models;

public class Course
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Type { get; set; } = "Micro-learning";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
    public ICollection<LearningCourse> LearningCourses { get; set; } = new List<LearningCourse>();
}
