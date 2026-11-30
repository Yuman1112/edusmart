using System.Net;
namespace SecureSmartTuitionSystem.Services.Communication;

public static class GuestEnquiryEmailTemplate
{
    public const string Subject = "EduSmart Enquiry";
    public static string PlainText(string name, string question, string reply, int id) =>
        $"Dear {name},\n\nThank you for contacting EduSmart. We have reviewed your enquiry and are pleased to share our response below.\n\nYour enquiry:\n{question}\n\nOur response:\n{reply}\n\nIf you have any further questions or need clarification, please reply directly to this email. Our staff will be happy to assist you.\n\nKind regards,\nEduSmart Support Team\n\nEnquiry reference: #{id}";

    public static string Html(string name, string question, string reply, int id)
    {
        static string Encode(string value) => WebUtility.HtmlEncode(value)
            .Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "<br />");
        return $$"""
<!DOCTYPE html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>EduSmart Enquiry</title></head>
<body style="margin:0;padding:0;background-color:#f2fbfc;color:#083b70;font-family:Arial,Helvetica,sans-serif;">
<div style="display:none;max-height:0;overflow:hidden;opacity:0;">Thank you for contacting EduSmart. Your enquiry response is ready.</div>
<table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background-color:#f2fbfc;"><tr><td align="center" style="padding:32px 16px;">
<table role="presentation" width="600" cellspacing="0" cellpadding="0" border="0" style="width:100%;max-width:600px;background-color:#ffffff;border:1px solid #cce6eb;border-radius:16px;overflow:hidden;">
<tr><td style="padding:28px 30px;background-color:#e8fafb;border-bottom:1px solid #d7edf0;border-radius:16px 16px 0 0;">
<p style="margin:0 0 12px;color:#008e9d;font-size:12px;font-weight:700;letter-spacing:1.5px;">EDUSMART · ONLINE TUITION</p>
<h1 style="margin:0;color:#083b70;font-size:26px;line-height:1.3;font-weight:700;">A response to your enquiry</h1>
<p style="margin:10px 0 0;color:#607e99;font-size:13px;">Enquiry reference #{{id}}</p>
</td></tr>
<tr><td style="padding:30px;">
<p style="margin:0 0 16px;font-size:15px;line-height:1.7;">Dear {{Encode(name)}},</p>
<p style="margin:0 0 24px;font-size:15px;line-height:1.7;">Thank you for contacting EduSmart. We have reviewed your enquiry and are pleased to share our response below.</p>
<table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0"><tr><td style="padding:18px 20px;background-color:#f4f9fb;border:1px solid #e1edf1;border-radius:10px;">
<p style="margin:0 0 8px;color:#607e99;font-size:12px;font-weight:700;letter-spacing:1px;">YOUR ENQUIRY</p>
<p style="margin:0;color:#254c70;font-size:14px;line-height:1.7;overflow-wrap:anywhere;word-break:break-word;">{{Encode(question)}}</p>
</td></tr></table>
<h2 style="margin:26px 0 12px;color:#008e9d;font-size:18px;line-height:1.4;">Our response</h2>
<p style="margin:0 0 26px;color:#083b70;font-size:15px;line-height:1.8;overflow-wrap:anywhere;word-break:break-word;">{{Encode(reply)}}</p>
<p style="margin:0 0 22px;font-size:14px;line-height:1.7;">If you have any further questions or need clarification, please <strong>reply directly to this email</strong>. Our staff will be happy to assist you.</p>
<p style="margin:0;font-size:14px;line-height:1.7;">Kind regards,<br><strong>EduSmart Support Team</strong></p>
</td></tr>
<tr><td style="padding:18px 30px;background-color:#fafdfd;border-top:1px solid #e1edf1;border-radius:0 0 16px 16px;">
<p style="margin:0;color:#607e99;font-size:12px;line-height:1.7;">EduSmart · Secure Smart Tuition System<br>This email is a response to the enquiry you submitted to our staff.</p>
</td></tr></table>
</td></tr></table></body></html>
""";
    }
}
