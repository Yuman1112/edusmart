using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecureSmartTuitionSystem.Services.Communication;
using SecureSmartTuitionSystem.ViewModels.Communication;
namespace SecureSmartTuitionSystem.Controllers;
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class FaqController(ChatbotAssistantService assistant, ChatbotSessionService sessions) : Controller
{
    private string UserId
    {
        get
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(id)) return "user:" + id;
            var guest = HttpContext.Session.GetString("Chatbot.GuestId");
            if (guest == null) { guest = Guid.NewGuid().ToString("N"); HttpContext.Session.SetString("Chatbot.GuestId", guest); }
            return "guest:" + guest;
        }
    }
    private string? AuthenticatedId => User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
    private bool IsAsyncRequest => Request.Headers["X-Requested-With"] == "XMLHttpRequest";
    private IActionResult InvalidInput(FaqViewModel model) => IsAsyncRequest
        ? BadRequest(new { error = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Please check your question." : e.ErrorMessage)) })
        : View("Index", Populate(model));
    [HttpGet] public IActionResult Index() => View(Populate(new()));
    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("ChatbotPolicy")]
    public async Task<IActionResult> Index(FaqViewModel model, CancellationToken ct)
    {
        // Ignore posted scope/consent: routing and permission decisions belong to the server.
        model.Scope = "auto";
        model.IncludeAccountData = User.Identity?.IsAuthenticated == true && (User.IsInRole("Parent") || User.IsInRole("Student"));
        ModelState.Remove(nameof(model.Scope));
        ModelState.Remove(nameof(model.IncludeAccountData));
        if (!ModelState.IsValid) return InvalidInput(model);
        bool streaming = IsAsyncRequest && Request.Headers.Accept.ToString().Contains("application/x-ndjson", StringComparison.OrdinalIgnoreCase);
        bool started = false;
        async Task Emit(string type, object payload)
        {
            if (!started)
            {
                Response.ContentType = "application/x-ndjson; charset=utf-8";
                Response.Headers["X-Accel-Buffering"] = "no";
                Response.Headers.CacheControl = "no-store";
                // Establish the guest session cookie before the first flush.
                HttpContext.Session.SetString("Chatbot.StreamSession", "1");
                await HttpContext.Session.CommitAsync(ct);
                await Response.StartAsync(ct); started = true;
            }
            await Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { type, payload }, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase }) + "\n", ct);
            await Response.Body.FlushAsync(ct);
        }
        ChatbotTurn turn;
        if (!string.IsNullOrWhiteSpace(model.Topic))
        {
            var answer = FaqService.FindById(model.Topic);
            if (answer == null) { ModelState.AddModelError(nameof(model.Topic), "Choose a valid FAQ."); return InvalidInput(model); }
            turn = new(answer.Question, answer.Answer, false, false, [new("faq-" + answer.Id, answer.Question, answer.Answer)], false);
        }
        else
        {
            string question = model.Question?.Trim() ?? "";
            if (question.Length == 0) { ModelState.AddModelError(nameof(model.Question), "Choose a question or type your question."); return InvalidInput(model); }
            var history = sessions.Read(HttpContext.Session, UserId);
            if (streaming) await Emit("start", new { question = ChatbotPrivacyPolicy.ForHistory(question) });
            try
            {
                var answer = await assistant.AskAsync(User, question, model.Scope, model.IncludeAccountData, history, ct,
                    streaming ? text => Emit("draft", new { text }) : null);
                turn = new(ChatbotPrivacyPolicy.ForHistory(question), answer.Text, answer.IsAI, answer.NeedsHuman, answer.Sources, ChatbotContextService.ResolveScope(question, "auto") is "bills" or "enrollments");
            }
            catch (Exception ex) when (ex is Microsoft.EntityFrameworkCore.DbUpdateException or Microsoft.Data.SqlClient.SqlException)
            { turn = new(ChatbotPrivacyPolicy.ForHistory(question), "The website records are temporarily unavailable. Please contact the centre.", false, true, [], false); }
        }
        bool transient = turn.IncludedAccountData || turn.Sources.Any(s => !(s.Id.StartsWith("faq-") || s.Id.StartsWith("kb-")));
        if (IsAsyncRequest)
        {
            if (!transient)
            {
                var saved = sessions.Read(HttpContext.Session, UserId);
                saved.Add(turn);
                sessions.Save(HttpContext.Session, UserId, saved);
            }
            var handoffToken = AuthenticatedId is {} id ? sessions.IssueHandoff(id, turn) : null;
            if (streaming) { await HttpContext.Session.CommitAsync(ct); await Emit("done", new { turn, handoffToken }); return new EmptyResult(); }
            return Json(new { turn, handoffToken });
        }
        if (turn.IncludedAccountData || turn.Sources.Any(s => !(s.Id.StartsWith("faq-") || s.Id.StartsWith("kb-"))))
        {
            // Render private records only in this POST response, never in stored chat history.
            var view = Populate(model);
            view.Turns.Add(turn);
            view.HandoffToken = AuthenticatedId is {} id ? sessions.IssueHandoff(id, turn) : null;
            return View(view);
        }
        var turns = sessions.Read(HttpContext.Session, UserId); turns.Add(turn); sessions.Save(HttpContext.Session, UserId, turns);
        return RedirectToAction(nameof(Index));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Clear()
    {
        HttpContext.Session.Remove("Chatbot.History");
        if (IsAsyncRequest) return Json(new { cleared = true });
        return RedirectToAction(nameof(Index));
    }
    private FaqViewModel Populate(FaqViewModel model)
    {
        model.AIConfigured = assistant.AIConfigured; model.Turns = sessions.Read(HttpContext.Session, UserId);
        if (model.Turns.Count > 0 && AuthenticatedId is {} id) model.HandoffToken = sessions.IssueHandoff(id, model.Turns[^1]);
        return model;
    }
}
