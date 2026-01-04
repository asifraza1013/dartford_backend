using inflan_api.Interfaces;
using inflan_api.Utils;
using System.Net;
using System.Net.Mail;

namespace inflan_api.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly bool _enableSsl;

    public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        // Load SMTP configuration
        _smtpHost = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
        _smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
        _smtpUsername = _configuration["Email:SmtpUsername"] ?? "";
        _smtpPassword = _configuration["Email:SmtpPassword"] ?? "";
        _fromEmail = _configuration["Email:FromEmail"] ?? "noreply@inflan.com";
        _fromName = _configuration["Email:FromName"] ?? "Inflan Team";
        _enableSsl = bool.Parse(_configuration["Email:EnableSsl"] ?? "true");
    }

    private string GetEmailTemplate(string title, string greeting, string content, string ctaText = "", string ctaUrl = "")
    {
        var ctaButton = !string.IsNullOrEmpty(ctaText) && !string.IsNullOrEmpty(ctaUrl)
            ? $@"
                <tr>
                    <td style=""padding: 20px 0;"">
                        <table cellpadding=""0"" cellspacing=""0"" border=""0"" align=""center"">
                            <tr>
                                <td align=""center"" style=""border-radius: 6px;"" bgcolor=""#3B71FE"">
                                    <a href=""{ctaUrl}"" target=""_blank"" style=""padding: 12px 32px; border: 1px solid #3B71FE; border-radius: 6px; font-family: 'Inter', Arial, sans-serif; font-size: 16px; font-weight: 600; color: #ffffff; text-decoration: none; display: inline-block;"">{ctaText}</a>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>"
            : "";

        return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{title}</title>
    <link href=""https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap"" rel=""stylesheet"">
</head>
<body style=""margin: 0; padding: 0; font-family: 'Inter', Arial, sans-serif; background-color: #f8f9fb;"">
    <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background-color: #f8f9fb; padding: 40px 20px;"">
        <tr>
            <td align=""center"">
                <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""600"" style=""background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 6px rgba(0, 0, 0, 0.05);"">
                    <!-- Header -->
                    <tr>
                        <td style=""background: linear-gradient(135deg, #3B71FE 0%, #2856D6 100%); padding: 40px 40px 30px 40px; text-align: center;"">
                            <h1 style=""margin: 0; font-size: 28px; font-weight: 700; color: #ffffff; font-family: 'Inter', Arial, sans-serif;"">{title}</h1>
                        </td>
                    </tr>

                    <!-- Body -->
                    <tr>
                        <td style=""padding: 40px;"">
                            <p style=""margin: 0 0 20px 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">{greeting}</p>

                            {content}

                            {ctaButton}

                            <p style=""margin: 30px 0 0 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                                Best regards,<br>
                                <strong style=""color: #101828;"">The Inflan Team</strong>
                            </p>
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td style=""background-color: #f8f9fb; padding: 30px 40px; border-top: 1px solid #EAECF0;"">
                            <p style=""margin: 0 0 10px 0; font-size: 14px; color: #667085; text-align: center; font-family: 'Inter', Arial, sans-serif;"">
                                © 2025 Inflan. All rights reserved.
                            </p>
                            <p style=""margin: 0; font-size: 12px; color: #98A2B3; text-align: center; font-family: 'Inter', Arial, sans-serif;"">
                                This is an automated message. Please do not reply to this email.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        try
        {
            // Check if SMTP is configured
            if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
            {
                _logger.LogWarning("SMTP credentials not configured. Email not sent.");
                _logger.LogInformation($"[EMAIL - NOT SENT] To: {toEmail} | Subject: {subject}");
                return;
            }

            using var message = new MailMessage();
            message.From = new MailAddress(_fromEmail, _fromName);
            message.To.Add(new MailAddress(toEmail));
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            using var smtpClient = new SmtpClient(_smtpHost, _smtpPort);
            smtpClient.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
            smtpClient.EnableSsl = _enableSsl;

            await smtpClient.SendMailAsync(message);

            _logger.LogInformation($"[EMAIL SENT] To: {toEmail} | Subject: {subject}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send email to {toEmail}. Subject: {subject}");
            throw;
        }
    }

    private async Task SendEmailWithAttachmentAsync(string toEmail, string subject, string body, string attachmentPath)
    {
        try
        {
            // Check if SMTP is configured
            if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
            {
                _logger.LogWarning("SMTP credentials not configured. Email not sent.");
                _logger.LogInformation($"[EMAIL - NOT SENT] To: {toEmail} | Subject: {subject}");
                return;
            }

            using var message = new MailMessage();
            message.From = new MailAddress(_fromEmail, _fromName);
            message.To.Add(new MailAddress(toEmail));
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            // Add attachment if file exists
            if (!string.IsNullOrEmpty(attachmentPath) && File.Exists(attachmentPath))
            {
                var attachment = new Attachment(attachmentPath);
                message.Attachments.Add(attachment);
                _logger.LogInformation($"Attachment added: {attachmentPath}");
            }
            else
            {
                _logger.LogWarning($"Attachment file not found: {attachmentPath}");
            }

            using var smtpClient = new SmtpClient(_smtpHost, _smtpPort);
            smtpClient.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
            smtpClient.EnableSsl = _enableSsl;

            await smtpClient.SendMailAsync(message);

            _logger.LogInformation($"[EMAIL SENT WITH ATTACHMENT] To: {toEmail} | Subject: {subject}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send email with attachment to {toEmail}. Subject: {subject}");
            throw;
        }
    }

    public async Task SendContractSignatureRequestAsync(string brandEmail, string brandName, int campaignId, string contractUrl)
    {
        var subject = $"Action Required: Sign Contract for Campaign #{campaignId}";

        var content = $@"
            <div style=""background-color: #F0F7FF; border-left: 4px solid #3B71FE; padding: 20px; border-radius: 8px; margin: 20px 0;"">
                <p style=""margin: 0 0 10px 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">
                    🎉 Great News!
                </p>
                <p style=""margin: 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                    The influencer has accepted your campaign request. You're one step closer to launching your campaign!
                </p>
            </div>

            <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""margin: 24px 0;"">
                <tr>
                    <td style=""padding: 12px 0; border-bottom: 1px solid #EAECF0;"">
                        <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Campaign ID</p>
                        <p style=""margin: 4px 0 0 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">#{campaignId}</p>
                    </td>
                </tr>
            </table>

            <p style=""margin: 20px 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                Please review and sign the contract to proceed with the campaign. Once the contract is signed, you will receive a payment link to activate the campaign.
            </p>";

        var body = GetEmailTemplate(
            "Contract Signature Required",
            $"Dear {brandName},",
            content,
            "Review & Sign Contract",
            contractUrl
        );

        await SendEmailAsync(brandEmail, subject, body);
    }

    public async Task SendPaymentRequestAsync(string brandEmail, string brandName, int campaignId, decimal amount, string currency)
    {
        var subject = $"Payment Required: Campaign #{campaignId}";

        var currencySymbol = currency.ToUpper() switch
        {
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            "NGN" => "₦",
            _ => currency
        };

        var content = $@"
            <div style=""background-color: #F0FDF4; border-left: 4px solid #10B981; padding: 20px; border-radius: 8px; margin: 20px 0;"">
                <p style=""margin: 0 0 10px 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">
                    ✅ Contract Signed Successfully
                </p>
                <p style=""margin: 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                    Thank you for signing the contract! You're almost ready to launch your campaign.
                </p>
            </div>

            <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""margin: 24px 0; background-color: #f8f9fb; border-radius: 8px;"">
                <tr>
                    <td style=""padding: 20px;"">
                        <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"">
                            <tr>
                                <td style=""padding: 8px 0; border-bottom: 1px solid #EAECF0;"">
                                    <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Campaign ID</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">#{campaignId}</p>
                                </td>
                            </tr>
                            <tr>
                                <td style=""padding: 16px 0 8px 0;"">
                                    <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Amount Due</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 28px; font-weight: 700; color: #3B71FE; font-family: 'Inter', Arial, sans-serif;"">{currencySymbol}{amount:N2}</p>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>

            <p style=""margin: 20px 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                Please proceed with the payment to activate your campaign. Once payment is received, your campaign will be activated and the influencer will begin work.
            </p>";

        var dashboardUrl = "https://dev.inflan.com/brand/dashboard/bookings";
        var body = GetEmailTemplate(
            "Payment Required",
            $"Dear {brandName},",
            content,
            "Go to Dashboard & Pay",
            dashboardUrl
        );

        await SendEmailAsync(brandEmail, subject, body);
    }

    public async Task SendCampaignActivatedAsync(string influencerEmail, string influencerName, int campaignId, string projectName)
    {
        var subject = $"Campaign Activated: {projectName}";

        var content = $@"
            <div style=""background-color: #F0FDF4; border-left: 4px solid #10B981; padding: 20px; border-radius: 8px; margin: 20px 0;"">
                <p style=""margin: 0 0 10px 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">
                    🎉 Great News!
                </p>
                <p style=""margin: 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                    Your campaign has been activated and is ready to begin!
                </p>
            </div>

            <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""margin: 24px 0;"">
                <tr>
                    <td style=""padding: 12px 0; border-bottom: 1px solid #EAECF0;"">
                        <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Campaign Name</p>
                        <p style=""margin: 4px 0 0 0; font-size: 18px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">{projectName}</p>
                    </td>
                </tr>
                <tr>
                    <td style=""padding: 12px 0; border-bottom: 1px solid #EAECF0;"">
                        <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Campaign ID</p>
                        <p style=""margin: 4px 0 0 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">#{campaignId}</p>
                    </td>
                </tr>
            </table>

            <p style=""margin: 20px 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                The brand has completed payment and you can now begin work on the campaign. Please log in to your dashboard to view the campaign details and deliverables.
            </p>";

        var dashboardUrl = "https://dev.inflan.com/influencer/dashboard/bookings";
        var body = GetEmailTemplate(
            "Campaign Activated",
            $"Dear {influencerName},",
            content,
            "View Campaign Details",
            dashboardUrl
        );

        await SendEmailAsync(influencerEmail, subject, body);
    }

    public async Task SendInfluencerResponseNotificationAsync(string brandEmail, string brandName, int campaignId, string projectName, bool accepted, string? contractPdfPath = null)
    {
        var subject = $"Campaign {(accepted ? "Accepted" : "Rejected")}: {projectName}";

        string content;
        string ctaText = "";
        string ctaUrl = "";

        if (accepted)
        {
            content = $@"
                <div style=""background-color: #F0FDF4; border-left: 4px solid #10B981; padding: 20px; border-radius: 8px; margin: 20px 0;"">
                    <p style=""margin: 0 0 10px 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">
                        🎉 Excellent News!
                    </p>
                    <p style=""margin: 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                        The influencer has accepted your campaign request!
                    </p>
                </div>

                <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""margin: 24px 0;"">
                    <tr>
                        <td style=""padding: 12px 0; border-bottom: 1px solid #EAECF0;"">
                            <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Campaign Name</p>
                            <p style=""margin: 4px 0 0 0; font-size: 18px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">{projectName}</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 12px 0; border-bottom: 1px solid #EAECF0;"">
                            <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Campaign ID</p>
                            <p style=""margin: 4px 0 0 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">#{campaignId}</p>
                        </td>
                    </tr>
                </table>

                <p style=""margin: 20px 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                    A contract has been generated and attached to this email. Please review and sign the contract via your dashboard to proceed to the next step.
                </p>";

            ctaText = "Review & Sign Contract";
            ctaUrl = "https://dev.inflan.com/brand/dashboard/bookings";
        }
        else
        {
            content = $@"
                <div style=""background-color: #FEF3F2; border-left: 4px solid #F04438; padding: 20px; border-radius: 8px; margin: 20px 0;"">
                    <p style=""margin: 0 0 10px 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">
                        Campaign Request Declined
                    </p>
                    <p style=""margin: 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                        Unfortunately, the influencer has declined your campaign request.
                    </p>
                </div>

                <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""margin: 24px 0;"">
                    <tr>
                        <td style=""padding: 12px 0; border-bottom: 1px solid #EAECF0;"">
                            <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Campaign Name</p>
                            <p style=""margin: 4px 0 0 0; font-size: 18px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">{projectName}</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 12px 0; border-bottom: 1px solid #EAECF0;"">
                            <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Campaign ID</p>
                            <p style=""margin: 4px 0 0 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">#{campaignId}</p>
                        </td>
                    </tr>
                </table>

                <p style=""margin: 20px 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                    Don't worry! You may try reaching out to other influencers or create a new campaign.
                </p>";

            ctaText = "Browse Influencers";
            ctaUrl = "https://dev.inflan.com/brand/dashboard/influencers";
        }

        var body = GetEmailTemplate(
            accepted ? "Campaign Accepted" : "Campaign Rejected",
            $"Dear {brandName},",
            content,
            ctaText,
            ctaUrl
        );

        // Send email with attachment if accepted and PDF path is provided
        if (accepted && !string.IsNullOrEmpty(contractPdfPath))
        {
            await SendEmailWithAttachmentAsync(brandEmail, subject, body, contractPdfPath);
        }
        else
        {
            await SendEmailAsync(brandEmail, subject, body);
        }
    }

    public async Task SendNewCampaignNotificationAsync(string influencerEmail, string influencerName, int campaignId, string projectName, string brandName)
    {
        var subject = $"New Campaign Request from {brandName}";

        var content = $@"
            <div style=""background-color: #F0F7FF; border-left: 4px solid #3B71FE; padding: 20px; border-radius: 8px; margin: 20px 0;"">
                <p style=""margin: 0 0 10px 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">
                    📢 New Campaign Opportunity!
                </p>
                <p style=""margin: 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                    You have received a new campaign booking request. This could be a great opportunity for collaboration!
                </p>
            </div>

            <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""margin: 24px 0; background-color: #f8f9fb; border-radius: 8px;"">
                <tr>
                    <td style=""padding: 20px;"">
                        <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"">
                            <tr>
                                <td style=""padding: 8px 0; border-bottom: 1px solid #EAECF0;"">
                                    <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Brand Name</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 18px; font-weight: 600; color: #3B71FE; font-family: 'Inter', Arial, sans-serif;"">{brandName}</p>
                                </td>
                            </tr>
                            <tr>
                                <td style=""padding: 12px 0 8px 0; border-bottom: 1px solid #EAECF0;"">
                                    <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Campaign Name</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 18px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">{projectName}</p>
                                </td>
                            </tr>
                            <tr>
                                <td style=""padding: 12px 0 8px 0;"">
                                    <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Campaign ID</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">#{campaignId}</p>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>

            <p style=""margin: 20px 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                Please review the campaign details carefully. You can accept or reject this request through your dashboard.
            </p>

            <div style=""background-color: #FFFBEB; border-left: 4px solid #F59E0B; padding: 16px; border-radius: 8px; margin: 20px 0;"">
                <p style=""margin: 0; font-size: 14px; line-height: 1.5; color: #92400E; font-family: 'Inter', Arial, sans-serif;"">
                    <strong>⏰ Action Required:</strong> Please respond to this request within 48 hours to maintain good standing with brands.
                </p>
            </div>";

        var dashboardUrl = "https://dev.inflan.com/influencer/dashboard/bookings";
        var body = GetEmailTemplate(
            "New Campaign Request",
            $"Dear {influencerName},",
            content,
            "Review Campaign",
            dashboardUrl
        );

        await SendEmailAsync(influencerEmail, subject, body);
    }

    public async Task SendSignedContractReviewRequestAsync(string influencerEmail, string influencerName, int campaignId, string projectName, string brandName)
    {
        var subject = $"Action Required: Review Signed Contract for Campaign #{campaignId}";

        var content = $@"
            <div style=""background-color: #F0F7FF; border-left: 4px solid #3B71FE; padding: 20px; border-radius: 8px; margin: 20px 0;"">
                <p style=""margin: 0 0 10px 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">
                    📄 Signed Contract Received
                </p>
                <p style=""margin: 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                    {brandName} has uploaded the signed contract for your campaign. Please review and approve it to proceed.
                </p>
            </div>

            <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""margin: 24px 0; background-color: #f8f9fb; border-radius: 8px;"">
                <tr>
                    <td style=""padding: 20px;"">
                        <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"">
                            <tr>
                                <td style=""padding: 8px 0; border-bottom: 1px solid #EAECF0;"">
                                    <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Brand Name</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 18px; font-weight: 600; color: #3B71FE; font-family: 'Inter', Arial, sans-serif;"">{brandName}</p>
                                </td>
                            </tr>
                            <tr>
                                <td style=""padding: 12px 0 8px 0; border-bottom: 1px solid #EAECF0;"">
                                    <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Campaign Name</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 18px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">{projectName}</p>
                                </td>
                            </tr>
                            <tr>
                                <td style=""padding: 12px 0 8px 0;"">
                                    <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Campaign ID</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">#{campaignId}</p>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>

            <p style=""margin: 20px 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                Please review the signed contract carefully. You can download it, approve it, or reject it if there are any issues. If you reject it, the brand will be notified to revise and resubmit the contract.
            </p>

            <div style=""background-color: #FFFBEB; border-left: 4px solid #F59E0B; padding: 16px; border-radius: 8px; margin: 20px 0;"">
                <p style=""margin: 0; font-size: 14px; line-height: 1.5; color: #92400E; font-family: 'Inter', Arial, sans-serif;"">
                    <strong>⏰ Next Steps:</strong> Once you approve the contract, the brand will receive a payment request to activate the campaign.
                </p>
            </div>";

        var dashboardUrl = "https://dev.inflan.com/influencer/dashboard/bookings";
        var body = GetEmailTemplate(
            "Review Signed Contract",
            $"Dear {influencerName},",
            content,
            "Review Signed Contract",
            dashboardUrl
        );

        await SendEmailAsync(influencerEmail, subject, body);
    }

    public async Task SendContractRevisionRequestAsync(string brandEmail, string brandName, int campaignId, string projectName, string? reason = null)
    {
        var subject = $"Contract Revision Required: Campaign #{campaignId}";

        var reasonSection = !string.IsNullOrEmpty(reason)
            ? $@"
                <div style=""background-color: #FFF4ED; border-left: 4px solid #F79009; padding: 16px; border-radius: 8px; margin: 20px 0;"">
                    <p style=""margin: 0 0 8px 0; font-size: 14px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">
                        Reason for Rejection:
                    </p>
                    <p style=""margin: 0; font-size: 14px; line-height: 1.5; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                        {reason}
                    </p>
                </div>"
            : "";

        var content = $@"
            <div style=""background-color: #FFF4ED; border-left: 4px solid #F79009; padding: 20px; border-radius: 8px; margin: 20px 0;"">
                <p style=""margin: 0 0 10px 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">
                    ⚠️ Contract Revision Needed
                </p>
                <p style=""margin: 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                    The influencer has reviewed your signed contract and requested revisions before proceeding.
                </p>
            </div>

            <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""margin: 24px 0;"">
                <tr>
                    <td style=""padding: 12px 0; border-bottom: 1px solid #EAECF0;"">
                        <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Campaign Name</p>
                        <p style=""margin: 4px 0 0 0; font-size: 18px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">{projectName}</p>
                    </td>
                </tr>
                <tr>
                    <td style=""padding: 12px 0; border-bottom: 1px solid #EAECF0;"">
                        <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Campaign ID</p>
                        <p style=""margin: 4px 0 0 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">#{campaignId}</p>
                    </td>
                </tr>
            </table>

            {reasonSection}

            <p style=""margin: 20px 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                Please review the original contract, make the necessary corrections, and upload the revised signed contract through your dashboard. Once the corrected contract is uploaded, the influencer will review it again.
            </p>";

        var dashboardUrl = "https://dev.inflan.com/brand/dashboard/bookings";
        var body = GetEmailTemplate(
            "Contract Revision Required",
            $"Dear {brandName},",
            content,
            "Review & Resubmit Contract",
            dashboardUrl
        );

        await SendEmailAsync(brandEmail, subject, body);
    }

    private string GetCurrencySymbol(string currency)
    {
        return currency.ToUpper() switch
        {
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            "NGN" => "₦",
            _ => currency
        };
    }

    private string FormatAmountFromPence(long amountInPence, string currency)
    {
        var symbol = GetCurrencySymbol(currency);
        var amount = amountInPence / 100.0m;
        return $"{symbol}{amount:N2}";
    }

    public async Task SendWithdrawalSuccessAsync(string influencerEmail, string influencerName, long amountInPence, string currency, string bankName, string accountNumberLast4)
    {
        var subject = "Withdrawal Successful";
        var formattedAmount = FormatAmountFromPence(amountInPence, currency);

        var content = $@"
            <div style=""background-color: #F0FDF4; border-left: 4px solid #10B981; padding: 20px; border-radius: 8px; margin: 20px 0;"">
                <p style=""margin: 0 0 10px 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">
                    ✅ Withdrawal Completed
                </p>
                <p style=""margin: 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                    Your withdrawal has been successfully processed and sent to your bank account.
                </p>
            </div>

            <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""margin: 24px 0; background-color: #f8f9fb; border-radius: 8px;"">
                <tr>
                    <td style=""padding: 20px;"">
                        <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"">
                            <tr>
                                <td style=""padding: 8px 0; border-bottom: 1px solid #EAECF0;"">
                                    <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Amount</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 28px; font-weight: 700; color: #10B981; font-family: 'Inter', Arial, sans-serif;"">{formattedAmount}</p>
                                </td>
                            </tr>
                            <tr>
                                <td style=""padding: 12px 0 8px 0; border-bottom: 1px solid #EAECF0;"">
                                    <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Bank</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">{bankName}</p>
                                </td>
                            </tr>
                            <tr>
                                <td style=""padding: 12px 0 8px 0;"">
                                    <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Account</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">****{accountNumberLast4}</p>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>

            <p style=""margin: 20px 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                The funds should appear in your bank account within 1-3 business days depending on your bank. You can view your withdrawal history in your dashboard.
            </p>";

        var dashboardUrl = "https://dev.inflan.com/influencer/dashboard/earnings";
        var body = GetEmailTemplate(
            "Withdrawal Successful",
            $"Dear {influencerName},",
            content,
            "View Earnings",
            dashboardUrl
        );

        await SendEmailAsync(influencerEmail, subject, body);
    }

    public async Task SendWithdrawalFailedAsync(string influencerEmail, string influencerName, long amountInPence, string currency, string? failureReason)
    {
        var subject = "Withdrawal Failed";
        var formattedAmount = FormatAmountFromPence(amountInPence, currency);

        var reasonSection = !string.IsNullOrEmpty(failureReason)
            ? $@"
                <div style=""background-color: #FEF3F2; border-left: 4px solid #F04438; padding: 16px; border-radius: 8px; margin: 20px 0;"">
                    <p style=""margin: 0 0 8px 0; font-size: 14px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">
                        Reason:
                    </p>
                    <p style=""margin: 0; font-size: 14px; line-height: 1.5; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                        {failureReason}
                    </p>
                </div>"
            : "";

        var content = $@"
            <div style=""background-color: #FEF3F2; border-left: 4px solid #F04438; padding: 20px; border-radius: 8px; margin: 20px 0;"">
                <p style=""margin: 0 0 10px 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">
                    ❌ Withdrawal Failed
                </p>
                <p style=""margin: 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                    Unfortunately, your withdrawal could not be processed.
                </p>
            </div>

            <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""margin: 24px 0; background-color: #f8f9fb; border-radius: 8px;"">
                <tr>
                    <td style=""padding: 20px;"">
                        <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Amount</p>
                        <p style=""margin: 4px 0 0 0; font-size: 28px; font-weight: 700; color: #F04438; font-family: 'Inter', Arial, sans-serif;"">{formattedAmount}</p>
                    </td>
                </tr>
            </table>

            {reasonSection}

            <p style=""margin: 20px 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                The amount has been returned to your available balance. Please check your bank account details and try again. If the issue persists, please contact our support team.
            </p>";

        var dashboardUrl = "https://dev.inflan.com/influencer/dashboard/settings/bank-accounts";
        var body = GetEmailTemplate(
            "Withdrawal Failed",
            $"Dear {influencerName},",
            content,
            "Check Bank Details",
            dashboardUrl
        );

        await SendEmailAsync(influencerEmail, subject, body);
    }

    public async Task SendWithdrawalProcessingAsync(string influencerEmail, string influencerName, long amountInPence, string currency, string bankName, string accountNumberLast4)
    {
        var subject = "Withdrawal Processing";
        var formattedAmount = FormatAmountFromPence(amountInPence, currency);

        var content = $@"
            <div style=""background-color: #F0F7FF; border-left: 4px solid #3B71FE; padding: 20px; border-radius: 8px; margin: 20px 0;"">
                <p style=""margin: 0 0 10px 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">
                    ⏳ Withdrawal In Progress
                </p>
                <p style=""margin: 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                    Your withdrawal request has been received and is being processed.
                </p>
            </div>

            <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""margin: 24px 0; background-color: #f8f9fb; border-radius: 8px;"">
                <tr>
                    <td style=""padding: 20px;"">
                        <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"">
                            <tr>
                                <td style=""padding: 8px 0; border-bottom: 1px solid #EAECF0;"">
                                    <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Amount</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 28px; font-weight: 700; color: #3B71FE; font-family: 'Inter', Arial, sans-serif;"">{formattedAmount}</p>
                                </td>
                            </tr>
                            <tr>
                                <td style=""padding: 12px 0 8px 0; border-bottom: 1px solid #EAECF0;"">
                                    <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Bank</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">{bankName}</p>
                                </td>
                            </tr>
                            <tr>
                                <td style=""padding: 12px 0 8px 0;"">
                                    <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Account</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">****{accountNumberLast4}</p>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>

            <p style=""margin: 20px 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                We'll send you another email once the withdrawal is complete. This usually takes 1-3 business days depending on your bank.
            </p>";

        var dashboardUrl = "https://dev.inflan.com/influencer/dashboard/earnings";
        var body = GetEmailTemplate(
            "Withdrawal Processing",
            $"Dear {influencerName},",
            content,
            "View Earnings",
            dashboardUrl
        );

        await SendEmailAsync(influencerEmail, subject, body);
    }

    public async Task SendPaymentReleasedAsync(string influencerEmail, string influencerName, long amountInPence, string currency, string campaignName, int milestoneNumber)
    {
        var subject = $"Payment Released: {campaignName}";
        var formattedAmount = FormatAmountFromPence(amountInPence, currency);

        var content = $@"
            <div style=""background-color: #F0FDF4; border-left: 4px solid #10B981; padding: 20px; border-radius: 8px; margin: 20px 0;"">
                <p style=""margin: 0 0 10px 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">
                    💰 Payment Released!
                </p>
                <p style=""margin: 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                    Great news! A milestone payment has been released to your available balance.
                </p>
            </div>

            <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""margin: 24px 0; background-color: #f8f9fb; border-radius: 8px;"">
                <tr>
                    <td style=""padding: 20px;"">
                        <table cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"">
                            <tr>
                                <td style=""padding: 8px 0; border-bottom: 1px solid #EAECF0;"">
                                    <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Amount</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 28px; font-weight: 700; color: #10B981; font-family: 'Inter', Arial, sans-serif;"">{formattedAmount}</p>
                                </td>
                            </tr>
                            <tr>
                                <td style=""padding: 12px 0 8px 0; border-bottom: 1px solid #EAECF0;"">
                                    <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Campaign</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">{campaignName}</p>
                                </td>
                            </tr>
                            <tr>
                                <td style=""padding: 12px 0 8px 0;"">
                                    <p style=""margin: 0; font-size: 14px; color: #667085; font-family: 'Inter', Arial, sans-serif;"">Milestone</p>
                                    <p style=""margin: 4px 0 0 0; font-size: 16px; font-weight: 600; color: #101828; font-family: 'Inter', Arial, sans-serif;"">Milestone {milestoneNumber}</p>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>

            <p style=""margin: 20px 0; font-size: 16px; line-height: 1.6; color: #344054; font-family: 'Inter', Arial, sans-serif;"">
                This payment is now available in your balance and ready for withdrawal. Visit your earnings page to withdraw funds to your bank account.
            </p>";

        var dashboardUrl = "https://dev.inflan.com/influencer/dashboard/earnings";
        var body = GetEmailTemplate(
            "Payment Released",
            $"Dear {influencerName},",
            content,
            "Withdraw Funds",
            dashboardUrl
        );

        await SendEmailAsync(influencerEmail, subject, body);
    }
}
