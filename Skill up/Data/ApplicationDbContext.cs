using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Skill_up.Models;

namespace Skill_up.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<LearningCourse> LearningCourses => Set<LearningCourse>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Course>(entity =>
        {
            entity.HasIndex(course => course.Name);
            entity.HasMany(course => course.Quizzes)
                .WithOne(quiz => quiz.Course)
                .HasForeignKey(quiz => quiz.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<LearningCourse>(entity =>
        {
            entity.HasIndex(learningCourse => new { learningCourse.UserId, learningCourse.CourseId })
                .IsUnique();

            entity.HasOne(learningCourse => learningCourse.User)
                .WithMany(user => user.LearningCourses)
                .HasForeignKey(learningCourse => learningCourse.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(learningCourse => learningCourse.Course)
                .WithMany(course => course.LearningCourses)
                .HasForeignKey(learningCourse => learningCourse.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<QuizAttempt>(entity =>
        {
            entity.HasIndex(attempt => new { attempt.Category, attempt.AttemptedAtUtc });

            entity.HasOne(attempt => attempt.User)
                .WithMany()
                .HasForeignKey(attempt => attempt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
