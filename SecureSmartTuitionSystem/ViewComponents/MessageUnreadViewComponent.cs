using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureSmartTuitionSystem.Data;
using SecureSmartTuitionSystem.Services.Communication;

namespace SecureSmartTuitionSystem.ViewComponents
{
    public sealed class MessageUnreadViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        public MessageUnreadViewComponent(ApplicationDbContext context) => _context = context;

        public async Task<IViewComponentResult> InvokeAsync()
        {
            string? userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId) ||
                !(UserClaimsPrincipal.IsInRole("Admin") || UserClaimsPrincipal.IsInRole("Parent") || UserClaimsPrincipal.IsInRole("Student") || UserClaimsPrincipal.IsInRole("Tutor")))
                return Content(string.Empty);

            return View(await MessageUnreadQuery.ForUser(
                _context, userId, UserClaimsPrincipal.IsInRole("Admin")).CountAsync());
        }
    }
}
