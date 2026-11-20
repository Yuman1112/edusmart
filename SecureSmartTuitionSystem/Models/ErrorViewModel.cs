namespace SecureSmartTuitionSystem.Models;
public class ErrorViewModel
{
    public int StatusCode { get; set; } = 500;
    public string ErrorCode { get; set; } = "HTTP_500";
    public string Title { get; set; } = "Something went wrong";
    public string Message { get; set; } = "We could not complete your request. Please try again later.";
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
