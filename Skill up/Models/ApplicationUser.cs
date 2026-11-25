using Microsoft.AspNetCore.Identity;

namespace Skill_up.Models;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<LearningCourse> LearningCourses { get; set; } = new List<LearningCourse>();
}
