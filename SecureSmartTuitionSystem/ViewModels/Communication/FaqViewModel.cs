using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SecureSmartTuitionSystem.Services.Communication;

namespace SecureSmartTuitionSystem.ViewModels.Communication;

public sealed class FaqViewModel
{
    [StringLength(300, ErrorMessage = "Please keep your question within 300 characters.")]
    public string? Question { get; set; }
    [StringLength(40)] public string? Topic { get; set; }
    [StringLength(20)] public string Scope { get; set; } = "auto";
    public bool IncludeAccountData { get; set; }
    [BindNever] public List<ChatbotTurn> Turns { get; set; } = [];
    [BindNever] public string? HandoffToken { get; set; }
    [BindNever] public bool AIConfigured { get; set; }
    [BindNever]
    public FaqEntry? Answer { get; set; }
    [BindNever]
    public bool Searched { get; set; }
}
