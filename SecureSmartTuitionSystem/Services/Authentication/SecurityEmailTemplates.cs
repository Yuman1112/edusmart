
using System.Net;

namespace SecureSmartTuitionSystem.Services.Authentication;

public static class SecurityEmailTemplates
{
    private static string Layout(string title, string content)
    {
        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
            </head>
            <body style="margin:0;padding:25px;background:#f4f9fb;
                         font-family:Arial,sans-serif;color:#07356b;">
                <div style="max-width:520px;margin:0 auto;
                            background:#ffffff;border:1px solid #cce5ea;
                            border-radius:14px;overflow:hidden;">

                    <div style="background:#e7f9fa;padding:25px;">
                        <p style="font-size:12px;font-weight:bold;
                                  color:#0097a7;margin:0 0 12px;">
                            EDUSMART ONLINE TUITION
                        </p>

                        <h2 style="font-size:23px;color:#07356b;margin:0;">
                            {{WebUtility.HtmlEncode(title)}}
                        </h2>
                    </div>

                    <div style="padding:30px 25px;font-size:14px;
                                line-height:1.7;color:#07356b;">
                        {{content}}
                    </div>
                </div>
            </body>
            </html>
            """;
    }

    public static string LoginOtp(string code, string? studentUserName = null)
        => VerificationCode(
            "Your sign-in verification code",
            "A request was made to sign in to the EduSmart account.",
            "Enter this code on the sign-in page to complete the login.",
            code, studentUserName);

    public static string TwoFactorSettingsOtp(string code, string? studentUserName = null)
        => VerificationCode(
            "Confirm your 2FA settings change",
            "A request was made to change Email 2FA for the EduSmart account.",
            "Enter this code in 2FA Settings to confirm the change.",
            code, studentUserName);

    private static string VerificationCode(
        string title, string request, string instruction, string code, string? studentUserName)
    {
        string safeCode = WebUtility.HtmlEncode(code);
        string student = studentUserName == null
            ? ""
            : $"<p>Student User ID: <strong>{WebUtility.HtmlEncode(studentUserName)}</strong></p>";

        return Layout(title, $$"""
            <p>Hello,</p>

            <p>{{WebUtility.HtmlEncode(request)}}</p>
            {{student}}

            <p>{{WebUtility.HtmlEncode(instruction)}}</p>

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
                    {{safeCode}}
                </div>

                <p style="font-size:12px;margin:12px 0 0;">
                    This code expires in 5 minutes.
                </p>
            </div>

            <p>For your security, do not share this
               verification code with anyone.</p>

            <p>If you did not make or expect this request,
               please ignore this email.</p>

            <p style="font-size:12px;color:#667;">
                This is an automated security notification
                from EduSmart.
            </p>
            """);
    }

    public static string ForgotPassword(string resetUrl)
    {
        string safeUrl = WebUtility.HtmlEncode(resetUrl);

        return Layout("Reset your password", $$"""
            <p>Hello,</p>

            <p>We received a request to reset the password
               for your EduSmart account.</p>

            <p>Click the button below to create a new password.</p>

            <p style="text-align:center;margin:30px 0;">
                <a href="{{safeUrl}}"
                   style="display:inline-block;background:#0799a4;
                          color:#ffffff;padding:13px 25px;
                          text-decoration:none;border-radius:7px;
                          font-weight:bold;">
                    Reset Password
                </a>
            </p>

            <p>This link expires in 2 hours.</p>

            <p>If you did not request a password reset,
               please ignore this email. Your password
               will remain unchanged.</p>

            <p style="font-size:12px;margin-top:25px;">
                If the button does not work, copy this
                address into your browser:
            </p>

            <p style="font-size:12px;overflow-wrap:anywhere;">
                <a href="{{safeUrl}}">{{safeUrl}}</a>
            </p>
            """);
    }
}
