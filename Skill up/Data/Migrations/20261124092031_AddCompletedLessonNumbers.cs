using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Skill_up.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompletedLessonNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[LearningCourses]', N'CompletedLessonNumbers') IS NULL
                BEGIN
                    ALTER TABLE [LearningCourses]
                    ADD [CompletedLessonNumbers] nvarchar(max) NOT NULL
                        CONSTRAINT [DF_LearningCourses_CompletedLessonNumbers] DEFAULT N'';
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[LearningCourses]', N'CompletedLessonNumbers') IS NOT NULL
                BEGIN
                    ALTER TABLE [LearningCourses]
                    DROP COLUMN [CompletedLessonNumbers];
                END
                """);
        }
    }
}
