namespace Skill_up.Models;

public class ProfileViewModel
{
    public string DisplayName { get; set; } = "ผู้เรียน";
    public string Email { get; set; } = "ยังไม่ได้เพิ่มอีเมล";
    public string AvatarLetter { get; set; } = "ผ";
    public int AverageProgress { get; set; }
    public int CompletedCourseCount { get; set; }
    public ProfileSkillViewModel? CurrentCourse { get; set; }
    public IReadOnlyList<ProfileSkillViewModel> Skills { get; set; } = Array.Empty<ProfileSkillViewModel>();
}

public class ProfileSkillViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int ProgressPercent { get; set; }
}
