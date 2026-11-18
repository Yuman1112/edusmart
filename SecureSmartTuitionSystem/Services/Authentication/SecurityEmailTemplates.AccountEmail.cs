using System.Net;
namespace SecureSmartTuitionSystem.Services.Authentication;
public static partial class SecurityEmailTemplates
{
    public static string CurrentEmailCode(string code, string userName, string newEmail)
    {
        return Layout("Confirm your email change", $$"""
            <p>Hello,</p>
            <p>A request was made to change the email address for your EduSmart account.</p>
            <p>User ID: <strong>{{WebUtility.HtmlEncode(userName)}}</strong></p>
            <p>New email address: <strong>{{WebUtility.HtmlEncode(newEmail)}}</strong></p>
            <p>Enter the code below on the Change Email page to verify your current email or primary guardian contact.</p>

            <div style="text-align:center;margin:30px 0;
                        padding:20px;background:#f0fbfc;
                        border:1px solid #cce9ed;
                        border-radius:10px;">
                <p style="font-size:12px;font-weight:bold;
                          color:#0097a7;margin:0 0 10px;">
                    YOUR VERIFICATION CODE
                </p>
                <div style="font-size:34px;font-weight:bold;
                            letter-spacing:8px;color:#07356b;">
                    {{WebUtility.HtmlEncode(code)}}
                </div>
                <p style="font-size:12px;margin:12px 0 0;">
                    This code expires in 10 minutes.
                </p>
            </div>

            <p>Your account email remains unchanged until your new email address is verified.</p>
            <p>Use the latest code if you requested another email. For your security, do not share this verification code with anyone.</p>
            <p>If you did not make or expect this request, do not approve it. Contact your EduSmart administrator.</p>
            <p style="font-size:12px;color:#667;">
                This is an automated security notification from EduSmart.
            </p>
            """);
    }
    public static string NewEmailVerification(string url, string userName) => Layout("Verify your new email address", $"<p>A request was made to change the email for your EduSmart account.</p><p>User ID: <strong>{WebUtility.HtmlEncode(userName)}</strong></p><p>Your current email remains unchanged until confirmation.</p><p style=\"text-align:center;margin:28px 0\"><a href=\"{WebUtility.HtmlEncode(url)}\" style=\"display:inline-block;background:#0799a4;color:#fff;padding:13px 25px;border-radius:7px;text-decoration:none;font-weight:bold\">Review Email Change</a></p><p>This link expires in two hours. If a newer verification email is requested, earlier links no longer work.</p><p>If you did not request this change, do not confirm it. Contact your EduSmart administrator.</p>");
    public static string EmailChangeNotice(string userName) => Layout("Account email updated", $"<p>The email address for account <strong>{WebUtility.HtmlEncode(userName)}</strong> has been updated. Existing sessions must sign in again.</p><p>If this was unexpected, contact the tuition centre immediately. The account's password and guardian relationships have not been changed.</p>");
    public static string RecoveryEmail(string url, string userName, bool guardian) => Layout("Reset your password", $"<p>{(guardian ? "A password reset was requested for the student account" : "A password reset was requested for account")} <strong>{WebUtility.HtmlEncode(userName)}</strong>.</p><p><a href=\"{WebUtility.HtmlEncode(url)}\">Reset password</a></p><p>This link expires after one hour and can be used only while the account and its recovery contact remain valid. If you did not request it, ignore this message.</p>");
}
