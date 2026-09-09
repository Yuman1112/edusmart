using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Models.Enrollment;
using SecureSmartTuitionSystem.Models.Identity;

namespace SecureSmartTuitionSystem.Data
{
    public static class DbInitializer
    {
        private const string AdminEmail = "admin@gmail.com";
        private const string TutorEmail = "tutor@gmail.com";

        public static async Task SeedRolesAsync(
            IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var roleManager =
                scope.ServiceProvider
                    .GetRequiredService<RoleManager<IdentityRole>>();

            var userManager =
                scope.ServiceProvider
                    .GetRequiredService<UserManager<ApplicationUser>>();

            var context =
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

            await CreateRolesAsync(roleManager);

            await AssignRoleAsync(
                userManager,
                AdminEmail,
                "Admin");

            await AssignRoleAsync(
                userManager,
                TutorEmail,
                "Tutor");

            await UpdateTestUserNamesAsync(userManager);

            await SeedClassDataAsync(context, userManager);
        }

        private static async Task CreateRolesAsync(
            RoleManager<IdentityRole> roleManager)
        {
            string[] roles =
            {
                "Admin",
                "Tutor",
                "Parent",
                "Student"
            };

            foreach (var roleName in roles)
            {
                if (await roleManager.RoleExistsAsync(roleName))
                {
                    continue;
                }

                var result = await roleManager.CreateAsync(
                    new IdentityRole(roleName));

                if (!result.Succeeded)
                {
                    var errors = string.Join(
                        ", ",
                        result.Errors.Select(error =>
                            error.Description));

                    throw new InvalidOperationException(
                        $"Unable to create role {roleName}: {errors}");
                }
            }
        }

        private static async Task AssignRoleAsync(
            UserManager<ApplicationUser> userManager,
            string email,
            string roleName)
        {
            var user = await userManager.FindByEmailAsync(email);

            // The account must be registered before
            // the role can be assigned.
            if (user == null)
            {
                return;
            }

            if (await userManager.IsInRoleAsync(user, roleName))
            {
                return;
            }

            var result = await userManager.AddToRoleAsync(
                user,
                roleName);

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    result.Errors.Select(error =>
                        error.Description));

                throw new InvalidOperationException(
                    $"Unable to assign {roleName} role " +
                    $"to {email}: {errors}");
            }
        }

        private static async Task UpdateTestUserNamesAsync(
            UserManager<ApplicationUser> userManager)
        {
            var admin =
                await userManager.FindByEmailAsync(AdminEmail);

            if (admin != null &&
                string.IsNullOrWhiteSpace(admin.FullName))
            {
                admin.FullName = "System Administrator";

                var result =
                    await userManager.UpdateAsync(admin);

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Unable to update Admin profile.");
                }
            }

            var tutor =
                await userManager.FindByEmailAsync(TutorEmail);

            if (tutor != null &&
                string.IsNullOrWhiteSpace(tutor.FullName))
            {
                tutor.FullName = "Test Tutor";

                var result =
                    await userManager.UpdateAsync(tutor);

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Unable to update Tutor profile.");
                }
            }
        }

        private static async Task SeedClassDataAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            var mathematics =
                await context.Subjects.FirstOrDefaultAsync(
                    subject =>
                        subject.SubjectName == "Mathematics" &&
                        subject.AcademicLevel == "Form 3");

            if (mathematics == null)
            {
                mathematics = new Subject
                {
                    EducationStage = EducationStage.Secondary,
                    AcademicLevel = "Form 3",
                    SubjectName = "Mathematics",
                    MonthlyFee = 200.00m,
                    IsActive = true
                };

                context.Subjects.Add(mathematics);
            }

            var science =
                await context.Subjects.FirstOrDefaultAsync(
                    subject =>
                        subject.SubjectName == "Science" &&
                        subject.AcademicLevel == "Form 3");

            if (science == null)
            {
                science = new Subject
                {
                    EducationStage = EducationStage.Secondary,
                    AcademicLevel = "Form 3",
                    SubjectName = "Science",
                    MonthlyFee = 220.00m,
                    IsActive = true
                };

                context.Subjects.Add(science);
            }

            if (mathematics != null)
            {
                mathematics.EducationStage =
                    EducationStage.Secondary;
            }

            if (science != null)
            {
                science.EducationStage =
                    EducationStage.Secondary;
            }

            await context.SaveChangesAsync();
            
            if (mathematics == null || science == null)
            {
                throw new InvalidOperationException(
                    "Unable to initialise Mathematics and Science subjects.");
            }

            var tutor =
                await userManager.FindByEmailAsync(TutorEmail);

            // ClassSession requires an existing Tutor account.
            if (tutor == null)
            {
                return;
            }

            bool mathematicsClassExists =
                await context.ClassSessions.AnyAsync(
                    session =>
                        session.ClassCode == "MATH-F3-01");

            if (!mathematicsClassExists)
            {
                context.ClassSessions.Add(
                    new ClassSession
                    {
                        ClassCode = "MATH-F3-01",
                        SubjectId = mathematics.SubjectId,
                        TutorUserId = tutor.Id,
                        DayOfWeek = DayOfWeek.Monday,
                        StartTime = new TimeOnly(18, 0),
                        EndTime = new TimeOnly(20, 0),
                        Venue = "https://meet.google.com/math-f3-demo",
                        Capacity = 25,
                        Status = ClassSessionStatus.Active
                    });
            }

            bool scienceClassExists =
                await context.ClassSessions.AnyAsync(
                    session =>
                        session.ClassCode == "SCI-F3-01");

            if (!scienceClassExists)
            {
                context.ClassSessions.Add(
                    new ClassSession
                    {
                        ClassCode = "SCI-F3-01",
                        SubjectId = science.SubjectId,
                        TutorUserId = tutor.Id,
                        DayOfWeek = DayOfWeek.Wednesday,
                        StartTime = new TimeOnly(18, 0),
                        EndTime = new TimeOnly(20, 0),
                        Venue = "https://meet.google.com/science-f3-demo",
                        Capacity = 25,
                        Status = ClassSessionStatus.Active
                    });
            }

            var existingMathematicsClass =
                await context.ClassSessions.FirstOrDefaultAsync(
                    session =>
                        session.ClassCode == "MATH-F3-01");

            if (existingMathematicsClass != null)
            {
                if (string.IsNullOrWhiteSpace(
                        existingMathematicsClass.Venue) ||
                    !existingMathematicsClass.Venue.StartsWith(
                        "https://meet.google.com/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    existingMathematicsClass.Venue =
                        "https://meet.google.com/math-f3-demo";
                }

                if (string.IsNullOrWhiteSpace(
                        existingMathematicsClass.ClassName))
                {
                    existingMathematicsClass.ClassName =
                        "FORM 3 - MM - Test Tutor - 06:00 PM";
                }
            }

            var existingScienceClass =
                await context.ClassSessions.FirstOrDefaultAsync(
                    session =>
                        session.ClassCode == "SCI-F3-01");

            if (existingScienceClass != null)
            {
                if (string.IsNullOrWhiteSpace(
                        existingScienceClass.Venue) ||
                    !existingScienceClass.Venue.StartsWith(
                        "https://meet.google.com/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    existingScienceClass.Venue =
                        "https://meet.google.com/science-f3-demo";
                }

                if (string.IsNullOrWhiteSpace(
                        existingScienceClass.ClassName))
                {
                    existingScienceClass.ClassName =
                        "FORM 3 - SCI - Test Tutor - 06:00 PM";
                }
            }

            await context.SaveChangesAsync();

        }
    }
}