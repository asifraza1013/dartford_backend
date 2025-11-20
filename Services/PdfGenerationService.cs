using inflan_api.Interfaces;
using inflan_api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace inflan_api.Services;

public class PdfGenerationService : IPdfGenerationService
{
    public async Task<string> GenerateContractPdfAsync(Campaign campaign, User brand, User influencer, Influencer? influencerProfile, Plan plan)
    {
        // Configure QuestPDF license (Community license is free for open-source projects)
        QuestPDF.Settings.License = LicenseType.Community;

        // Create contracts directory if it doesn't exist
        var contractsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "contracts");
        if (!Directory.Exists(contractsFolder))
            Directory.CreateDirectory(contractsFolder);

        // Generate unique filename
        var fileName = $"contract_{campaign.Id}_{Guid.NewGuid()}.pdf";
        var filePath = Path.Combine(contractsFolder, fileName);

        // Check if brand has a logo
        string? brandLogoPath = null;
        if (!string.IsNullOrEmpty(brand.ProfileImage))
        {
            var logoFullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", brand.ProfileImage.TrimStart('/'));
            if (File.Exists(logoFullPath))
            {
                brandLogoPath = logoFullPath;
            }
        }

        // Generate PDF matching the sample format
        await Task.Run(() =>
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Black).LineHeight(1.4f));

                    page.Header()
                        .Column(header =>
                        {
                            // Add brand logo if available
                            if (!string.IsNullOrEmpty(brandLogoPath))
                            {
                                header.Item().AlignLeft().Height(60).Image(brandLogoPath);
                                header.Item().PaddingTop(10);
                            }

                            // Title
                            header.Item().AlignLeft().Text("Influencer Marketing Agreement")
                                .FontSize(18).Bold().FontColor("#0066CC");
                        });

                    page.Content()
                        .PaddingTop(0.5f, Unit.Centimetre)
                        .Column(column =>
                        {
                            column.Spacing(12);

                            // Introduction
                            column.Item().Text(text =>
                            {
                                text.Span("This Influencer Marketing Agreement (the \"Agreement\") is entered into as of ");
                                text.Span($"{campaign.CampaignStartDate:dd/MM/yyyy}").Bold();
                                text.Span(", by and between:");
                            });

                            // Parties Section
                            column.Item().Text("Parties").FontSize(13).Bold().FontColor("#0066CC");

                            column.Item().Column(partiesCol =>
                            {
                                partiesCol.Item().Text(text =>
                                {
                                    text.Span("●  ");
                                    text.Span($"{brand.BrandName ?? brand.Name} ").Bold();
                                    text.Span("(\"Company\"), contact email: ");
                                    text.Span(brand.Email ?? "N/A");
                                    text.Span("; and");
                                });

                                partiesCol.Item().PaddingTop(5).Text(text =>
                                {
                                    text.Span("●  ");
                                    text.Span($"{influencer.Name} ").Bold();
                                    text.Span("(\"Influencer\"), contact email: ");
                                    text.Span(influencer.Email ?? "N/A");
                                });
                            });

                            column.Item().Text("Together referred to as the \"Parties.\"");

                            // Scope of Work
                            column.Item().PaddingTop(10).Text("Scope of Work").FontSize(13).Bold().FontColor("#0066CC");

                            column.Item().Text("The Influencer agrees to create and publish promotional content (the \"Content\") for the Company's product(s) (the \"Product\") as specified below:");

                            column.Item().Column(scopeCol =>
                            {
                                scopeCol.Item().Text(text =>
                                {
                                    text.Span("●  ");
                                    text.Span("Product: ").Bold();
                                    text.Span(campaign.AboutProject ?? campaign.ProjectName);
                                });

                                if (plan.PlanDetails != null && plan.PlanDetails.Any())
                                {
                                    scopeCol.Item().Text(text =>
                                    {
                                        text.Span("●  ");
                                        text.Span("Deliverables: ").Bold();
                                        text.Span(string.Join(", ", plan.PlanDetails));
                                    });
                                }

                                // Add platforms if influencer profile is available
                                if (influencerProfile != null)
                                {
                                    var platforms = new List<string>();
                                    if (!string.IsNullOrEmpty(influencerProfile.Instagram)) platforms.Add("Instagram");
                                    if (!string.IsNullOrEmpty(influencerProfile.TikTok)) platforms.Add("TikTok");
                                    if (!string.IsNullOrEmpty(influencerProfile.YouTube)) platforms.Add("YouTube");
                                    if (!string.IsNullOrEmpty(influencerProfile.Facebook)) platforms.Add("Facebook");

                                    if (platforms.Any())
                                    {
                                        scopeCol.Item().Text(text =>
                                        {
                                            text.Span("●  ");
                                            text.Span("Platforms: ").Bold();
                                            text.Span(string.Join(", ", platforms));
                                        });
                                    }
                                }

                                scopeCol.Item().Text(text =>
                                {
                                    text.Span("●  ");
                                    text.Span("Campaign Duration: ").Bold();
                                    text.Span($"{campaign.CampaignStartDate:dd/MM/yyyy} to {campaign.CampaignEndDate:dd/MM/yyyy}");
                                });
                            });

                            // Compensation
                            column.Item().PaddingTop(10).Text("Compensation").FontSize(13).Bold().FontColor("#0066CC");

                            column.Item().Column(compCol =>
                            {
                                compCol.Item().Text(text =>
                                {
                                    text.Span("●  ");
                                    text.Span("Payment: ").Bold();
                                    text.Span($"In consideration for the Content, the Company shall pay the Influencer {campaign.Currency}{campaign.Amount:N2} (the \"Payment\") for {plan.NumberOfMonths} month(s) as per the agreed plan.");
                                });
                            });

                            // Content Ownership and Usage
                            column.Item().PaddingTop(10).Text("Content Ownership and Usage").FontSize(13).Bold().FontColor("#0066CC");

                            column.Item().Column(ownershipCol =>
                            {
                                ownershipCol.Item().Text(text =>
                                {
                                    text.Span("●  ");
                                    text.Span("Ownership: ").Bold();
                                    text.Span($"The {brand.BrandName ?? "Company"} retains ownership of the Content.");
                                });

                                ownershipCol.Item().Text(text =>
                                {
                                    text.Span("●  ");
                                    text.Span("Exclusivity: ").Bold();
                                    text.Span("During the campaign the Content is published, the Influencer agrees not to promote competing products/services without prior written consent from the Company.");
                                });
                            });

                            // Disclosure and Compliance
                            column.Item().PaddingTop(10).Text("Disclosure and Compliance").FontSize(13).Bold().FontColor("#0066CC");

                            column.Item().Column(disclosureCol =>
                            {
                                disclosureCol.Item().Text(text =>
                                {
                                    text.Span("●  ");
                                    text.Span("The Influencer agrees to comply with all applicable laws, including advertising standards and guidelines on endorsements and testimonials.");
                                });
                                disclosureCol.Item().Text(text =>
                                {
                                    text.Span("●  ");
                                    text.Span("The Content must include clear disclosure of the partnership as required by law.");
                                });
                            });

                            // Approval Process
                            column.Item().PaddingTop(10).Text("Approval Process").FontSize(13).Bold().FontColor("#0066CC");

                            column.Item().Column(approvalCol =>
                            {
                                approvalCol.Item().Text(text =>
                                {
                                    text.Span("●  ");
                                    text.Span("The Influencer agrees to submit the Content for approval to the Company before publication.");
                                });
                                approvalCol.Item().Text(text =>
                                {
                                    text.Span("●  ");
                                    text.Span("The Company must provide feedback or approval within 3 business days. If no response is received, the Content shall be considered approved.");
                                });
                            });

                            // Confidentiality
                            column.Item().PaddingTop(10).Text("Confidentiality").FontSize(13).Bold().FontColor("#0066CC");

                            column.Item().Text("The Influencer agrees to keep any non-public information about the Company, its products, and this Agreement confidential and not disclose it to any third party without written consent.");

                            // Termination
                            column.Item().PaddingTop(10).Text("Termination").FontSize(13).Bold().FontColor("#0066CC");

                            column.Item().Text("This Agreement may be terminated:");

                            column.Item().Column(termCol =>
                            {
                                termCol.Item().Text("●  By mutual agreement of the Parties.");
                                termCol.Item().Text("●  By the Company, if the Influencer breaches the terms of this Agreement.");
                                termCol.Item().Text("●  By the Influencer, if the Company fails to pay the agreed Payment.");
                            });

                            column.Item().Text("Upon termination, any unused or unpublished Content shall not be used without mutual consent.");

                            // Indemnification
                            column.Item().PaddingTop(10).Text("Indemnification").FontSize(13).Bold().FontColor("#0066CC");

                            column.Item().Text("The Influencer agrees to indemnify and keep indemnified the Company from any claims, damages, or legal actions resulting from the Influencer's actions, including but not limited to intellectual property infringement or failure to comply with applicable laws.");

                            // Miscellaneous
                            column.Item().PaddingTop(10).Text("Miscellaneous").FontSize(13).Bold().FontColor("#0066CC");

                            column.Item().Column(miscCol =>
                            {
                                miscCol.Item().Text(text =>
                                {
                                    text.Span("●  ");
                                    text.Span("Governing Law: ").Bold();
                                    text.Span("This Agreement shall be governed by the laws of England and Wales.");
                                });

                                miscCol.Item().Text(text =>
                                {
                                    text.Span("●  ");
                                    text.Span("Entire Agreement: ").Bold();
                                    text.Span("This document constitutes the entire Agreement between the Parties and supersedes all prior agreements.");
                                });

                                miscCol.Item().Text(text =>
                                {
                                    text.Span("●  ");
                                    text.Span("Amendments: ").Bold();
                                    text.Span("Any amendments to this Agreement must be agreed in writing and signed by both Parties.");
                                });

                                miscCol.Item().Text(text =>
                                {
                                    text.Span("●  ");
                                    text.Span("Force Majeure: ").Bold();
                                    text.Span("Neither Party shall be held liable for delays or failure to perform due to circumstances beyond their reasonable control.");
                                });
                            });

                            // Signatures Section
                            column.Item().PaddingTop(20).Text("Signatures").FontSize(13).Bold().FontColor("#0066CC");

                            column.Item().PaddingTop(10).Row(row =>
                            {
                                // Company Signature
                                row.RelativeItem().Column(companySignCol =>
                                {
                                    companySignCol.Item().Text("For the Company:").Bold();
                                    companySignCol.Item().PaddingTop(10).Text("Name").Bold();
                                    companySignCol.Item().PaddingTop(5).Text(brand.Name ?? "");
                                    companySignCol.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Medium);

                                    companySignCol.Item().PaddingTop(10).Text("Title").Bold();
                                    companySignCol.Item().PaddingTop(5).Text("Authorized Representative");
                                    companySignCol.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Medium);

                                    companySignCol.Item().PaddingTop(10).Text("Signature").Bold();
                                    companySignCol.Item().PaddingTop(20).LineHorizontal(1).LineColor(Colors.Grey.Medium);

                                    companySignCol.Item().PaddingTop(10).Text("Date").Bold();
                                    companySignCol.Item().PaddingTop(5).Text($"{campaign.CampaignStartDate:dd/MM/yyyy}");
                                    companySignCol.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                                });

                                row.Spacing(40);

                                // Influencer Signature
                                row.RelativeItem().Column(influencerSignCol =>
                                {
                                    influencerSignCol.Item().Text("For the Influencer:").Bold();
                                    influencerSignCol.Item().PaddingTop(10).Text("Name").Bold();
                                    influencerSignCol.Item().PaddingTop(20).LineHorizontal(1).LineColor(Colors.Grey.Medium);

                                    influencerSignCol.Item().PaddingTop(10).Text("Signature").Bold();
                                    influencerSignCol.Item().PaddingTop(20).LineHorizontal(1).LineColor(Colors.Grey.Medium);

                                    influencerSignCol.Item().PaddingTop(10).Text("Date").Bold();
                                    influencerSignCol.Item().PaddingTop(20).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                                });
                            });
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(text =>
                        {
                            text.Span($"{brand.BrandName ?? brand.Name} ").FontSize(8).Bold();
                            text.Span("- Influencer Marketing Agreement").FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                });
            })
            .GeneratePdf(filePath);
        });

        // Return relative path for database storage
        return $"/contracts/{fileName}";
    }
}
