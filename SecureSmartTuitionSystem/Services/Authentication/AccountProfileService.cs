using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Models.Identity;
using SecureSmartTuitionSystem.ViewModels.Identity;

namespace SecureSmartTuitionSystem.Services.Authentication;

public sealed class AccountProfileService(ApplicationDbContext db)
{
    public async Task<AccountProfileModel?> LoadAsync(ApplicationUser account, string role)
    {
        var model = new AccountProfileModel
        {
            Role = role,
            UserId = account.UserName ?? "",
            Email = account.Email,
            FullName = account.FullName ?? "",
            PhoneNumber = account.PhoneNumber
        };

        if (role == "Admin")
        {
            var admin = await db.AdminProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.ApplicationUserId == account.Id);
            model.FullName = admin?.FullName ?? model.FullName;
            model.Detail = admin?.Position;
        }
        else if (role == "Student")
        {
            var student = await db.StudentProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.ApplicationUserId == account.Id);
            if (student == null) return null;
            model.FullName = student.FullName;
            model.School = student.School;
            model.Detail = student.AcademicLevel;
        }
        else
        {
            var parent = await db.ParentProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.ApplicationUserId == account.Id);
            if (parent == null) return null;
            model.FullName = parent.FullName;
            model.Address = parent.Address;
        }
        return model;
    }

    public async Task<bool> SaveAsync(ApplicationUser account, AccountProfileModel model, AccountProfileModel input)
    {
        model.FullName = input.FullName.Trim();
        model.School = input.School?.Trim();
        model.Address = input.Address?.Trim();
        model.PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();

        if (model.Role == "Student")
        {
            var student = await db.StudentProfiles.SingleOrDefaultAsync(p => p.ApplicationUserId == account.Id);
            if (student == null) return false;
            student.FullName = model.FullName;
            student.School = model.School!;
        }
        else if (model.Role == "Parent")
        {
            var parent = await db.ParentProfiles.SingleOrDefaultAsync(p => p.ApplicationUserId == account.Id);
            if (parent == null) return false;
            parent.FullName = model.FullName;
            parent.Address = model.Address;
        }
        else
        {
            var admin = await db.AdminProfiles.SingleOrDefaultAsync(p => p.ApplicationUserId == account.Id);
            if (admin != null)
                admin.FullName = model.FullName;
        }

        if (model.Role != "Student" && account.PhoneNumber != model.PhoneNumber)
        {
            account.PhoneNumber = model.PhoneNumber;
            account.PhoneNumberConfirmed = false;
        }

        account.FullName = model.FullName;
        account.ConcurrencyStamp = Guid.NewGuid().ToString();
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }

        return true;
    }
}
