namespace Skill_up.Models;

public sealed class AdminUserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string LastActive { get; set; } = "Today";
    public string LoginMethod { get; set; } = string.Empty;
    public string CareerGoal { get; set; } = string.Empty;
    public int CompletedCourses { get; set; }
    public string Status { get; set; } = "Active";
}
