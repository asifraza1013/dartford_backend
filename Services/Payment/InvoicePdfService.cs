using System.Reflection;
using inflan_api.Interfaces;
using inflan_api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace inflan_api.Services.Payment;

public interface IInvoicePdfService
{
    Task<byte[]> GenerateInvoicePdfAsync(Invoice invoice);

    /// <summary>
    /// A full campaign statement: every milestone (or the single full-payment line
    /// for a one-time campaign), paid or still pending, with a summary of what's
    /// been paid and what's still outstanding. Brand-facing only.
    /// </summary>
    Task<byte[]> GenerateCampaignInvoicePdfAsync(
        Campaign campaign,
        User? brand,
        User? influencer,
        List<PaymentMilestone> milestones);
}

public class InvoicePdfService : IInvoicePdfService
{
    private readonly ILogger<InvoicePdfService> _logger;

    // Theme — sampled from the real Inflan logo/brand palette, not approximated.
    private const string Navy = "#101828";
    private const string Slate = "#475467";
    private const string MutedSlate = "#667085";
    private const string LineColor = "#EAECF0";
    private const string Panel = "#F9FAFB";
    private const string BrandOrange = "#FFB61B";
    private const string PaidGreen = "#027A48";
    private const string PaidGreenBg = "#ECFDF3";
    private const string PendingAmber = "#B54708";
    private const string PendingAmberBg = "#FFFAEB";
    private const string OverdueRed = "#B42318";
    private const string OverdueRedBg = "#FEF3F2";
    private const string AccentBlue = "#3B71FE";
    private const string AccentBlueBg = "#EFF4FF";

    // Logo bytes never change at runtime — read once, reuse for every invoice.
    private static readonly object LogoLock = new();
    private static byte[]? _logoBytes;

    public InvoicePdfService(ILogger<InvoicePdfService> logger)
    {
        _logger = logger;
    }

    private byte[]? LoadLogoBytes()
    {
        if (_logoBytes != null)
            return _logoBytes;

        lock (LogoLock)
        {
            if (_logoBytes != null)
                return _logoBytes;

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("inflan-logo.png", StringComparison.OrdinalIgnoreCase));

                if (resourceName != null)
                {
                    using var resourceStream = assembly.GetManifestResourceStream(resourceName);
                    if (resourceStream != null)
                    {
                        using var memoryStream = new MemoryStream();
                        resourceStream.CopyTo(memoryStream);
                        _logoBytes = memoryStream.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load invoice logo asset; invoice PDFs will render without it");
            }

            return _logoBytes;
        }
    }

    public async Task<byte[]> GenerateInvoicePdfAsync(Invoice invoice)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var logo = LoadLogoBytes();
        var isPaid = invoice.PaidAt.HasValue;
        var feePercent = invoice.SubtotalInPence > 0
            ? Math.Round((decimal)invoice.PlatformFeeInPence / invoice.SubtotalInPence * 100, 1)
            : 0m;

        // The invoice bills the Brand (who paid) on behalf of the Inflan platform
        // (which processed the payment and takes the platform fee) — not the
        // influencer, who is the one being paid out, not the one being billed.
        var brandName = invoice.Brand?.BrandName ?? invoice.Brand?.Name ?? "Brand";
        var brandEmail = invoice.Brand?.Email ?? "";
        var influencerName = invoice.Influencer?.Name ?? invoice.Influencer?.BrandName ?? "Influencer";
        var campaignName = invoice.Campaign?.ProjectName ?? $"Campaign #{invoice.CampaignId}";

        return await Task.Run(() =>
        {
            using var stream = new MemoryStream();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(10).FontColor(Navy));

                    page.Header().Element(c => ComposeHeader(c, logo, "INVOICE", invoice.InvoiceNumber));
                    page.Content().Element(c => ComposeContent(
                        c, invoice, brandName, brandEmail, influencerName, campaignName, isPaid, feePercent));
                    page.Footer().Element(c => ComposeFooter(c, "Invoice", invoice.InvoiceNumber));
                });
            }).GeneratePdf(stream);

            return stream.ToArray();
        });
    }

    public async Task<byte[]> GenerateCampaignInvoicePdfAsync(
        Campaign campaign,
        User? brand,
        User? influencer,
        List<PaymentMilestone> milestones)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var logo = LoadLogoBytes();
        var reference = $"CAMP-{campaign.Id}";
        var brandName = brand?.BrandName ?? brand?.Name ?? "Brand";
        var brandEmail = brand?.Email ?? "";
        var influencerName = influencer?.Name ?? influencer?.BrandName ?? "Influencer";
        var campaignName = campaign.ProjectName ?? $"Campaign #{campaign.Id}";
        var statusLabel = campaign.CampaignStatus == 7 ? "Completed" : "Active";

        // Rows to render: real milestones if the campaign has any, otherwise a single
        // synthetic row for a one-time payment campaign (which never gets milestone rows).
        var rows = BuildCampaignInvoiceRows(campaign, milestones);
        long grandTotal = rows.Sum(r => r.AmountInPence);
        long paidTotal = rows.Where(r => r.IsPaid).Sum(r => r.AmountInPence);
        long outstandingTotal = grandTotal - paidTotal;
        var includesFeeNote = milestones.Any(m => m.PlatformFeeInPence > 0);

        return await Task.Run(() =>
        {
            using var stream = new MemoryStream();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(10).FontColor(Navy));

                    page.Header().Element(c => ComposeHeader(c, logo, "CAMPAIGN INVOICE", reference));
                    page.Content().Element(c => ComposeCampaignContent(
                        c, brandName, brandEmail, influencerName, campaignName, statusLabel,
                        campaign.CampaignStartDate, campaign.CampaignEndDate, campaign.Currency ?? "NGN",
                        rows, grandTotal, paidTotal, outstandingTotal, includesFeeNote));
                    page.Footer().Element(c => ComposeFooter(c, "Statement", reference));
                });
            }).GeneratePdf(stream);

            return stream.ToArray();
        });
    }

    private record CampaignInvoiceRow(string Description, long AmountInPence, int Status, DateTime DueDate, DateTime? PaidAt)
    {
        public bool IsPaid => Status == (int)Utils.MilestoneStatus.PAID;
    }

    private static List<CampaignInvoiceRow> BuildCampaignInvoiceRows(Campaign campaign, List<PaymentMilestone> milestones)
    {
        var activeMilestones = milestones
            .Where(m => m.Status != (int)Utils.MilestoneStatus.CANCELLED)
            .OrderBy(m => m.MilestoneNumber)
            .ToList();

        if (activeMilestones.Count > 0)
        {
            return activeMilestones
                .Select(m => new CampaignInvoiceRow(
                    string.IsNullOrWhiteSpace(m.Title) ? $"Milestone {m.MilestoneNumber}" : m.Title!,
                    m.AmountInPence + m.PlatformFeeInPence,
                    m.Status,
                    m.DueDate,
                    m.PaidAt))
                .ToList();
        }

        // One-time payment campaigns never get PaymentMilestone rows — represent the
        // whole campaign as a single line using the campaign's own totals.
        var isPaid = campaign.TotalAmountInPence > 0 && campaign.PaidAmountInPence >= campaign.TotalAmountInPence;
        return new List<CampaignInvoiceRow>
        {
            new(
                "Full campaign payment",
                campaign.TotalAmountInPence,
                isPaid ? (int)Utils.MilestoneStatus.PAID : (int)Utils.MilestoneStatus.PENDING,
                campaign.CampaignEndDate.ToDateTime(TimeOnly.MinValue),
                campaign.PaymentCompletedAt)
        };
    }

    private static void ComposeHeader(IContainer container, byte[]? logo, string title, string reference)
    {
        container.Column(col =>
        {
            col.Item().Height(5).Background(BrandOrange);
            col.Item().PaddingHorizontal(2.2f, Unit.Centimetre).PaddingTop(1.4f, Unit.Centimetre).Row(row =>
            {
                row.RelativeItem().Height(34).Element(logoContainer =>
                {
                    if (logo != null)
                        logoContainer.Image(logo).FitHeight();
                    else
                        logoContainer.AlignLeft().Text("Inflan").FontSize(18).Bold().FontColor(Navy);
                });

                row.RelativeItem().AlignRight().Column(c =>
                {
                    c.Item().Text(title).FontSize(20).Bold().FontColor(Navy).LetterSpacing(0.04f);
                    c.Item().PaddingTop(4).Text(reference).FontSize(10).FontColor(MutedSlate);
                });
            });
        });
    }

    private static void ComposeContent(
        IContainer container,
        Invoice invoice,
        string brandName,
        string brandEmail,
        string influencerName,
        string campaignName,
        bool isPaid,
        decimal feePercent)
    {
        container.PaddingHorizontal(2.2f, Unit.Centimetre).PaddingTop(1f, Unit.Centimetre).Column(col =>
        {
            col.Item().LineHorizontal(1).LineColor(LineColor);

            // Billed by / Billed to / Issue date + status
            col.Item().PaddingTop(20).Row(row =>
            {
                row.Spacing(16);

                row.RelativeItem(1.1f).Column(c =>
                {
                    c.Item().Text("Billed by").FontSize(8.5f).Bold().FontColor(MutedSlate).LetterSpacing(0.03f);
                    c.Item().PaddingTop(6).Text("Inflan Platform").FontSize(11).Bold().FontColor(Navy);
                    c.Item().PaddingTop(2).Text("support@inflan.com").FontSize(9.5f).FontColor(Slate);
                    c.Item().Text("www.inflan.com").FontSize(9.5f).FontColor(Slate);
                });

                row.RelativeItem(1.1f).Column(c =>
                {
                    c.Item().Text("Billed to").FontSize(8.5f).Bold().FontColor(MutedSlate).LetterSpacing(0.03f);
                    c.Item().PaddingTop(6).Text(brandName).FontSize(11).Bold().FontColor(Navy);
                    if (!string.IsNullOrWhiteSpace(brandEmail))
                        c.Item().PaddingTop(2).Text(brandEmail).FontSize(9.5f).FontColor(Slate);
                });

                row.RelativeItem(1f).Column(c =>
                {
                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Issue date").FontSize(8.5f).FontColor(MutedSlate);
                        r.RelativeItem().AlignRight().Text(invoice.IssuedAt.ToString("dd MMM yyyy")).FontSize(9.5f).FontColor(Navy);
                    });
                    c.Item().PaddingTop(8).Row(r =>
                    {
                        r.RelativeItem().Text("Status").FontSize(8.5f).FontColor(MutedSlate);
                        r.RelativeItem().AlignRight().Element(e =>
                        {
                            e.Background(isPaid ? PaidGreenBg : PendingAmberBg)
                             .PaddingVertical(2).PaddingHorizontal(8)
                             .Text(isPaid ? "PAID" : "PENDING")
                             .FontSize(8).Bold().FontColor(isPaid ? PaidGreen : PendingAmber);
                        });
                    });
                });
            });

            // Campaign details panel
            col.Item().PaddingTop(22).Background(Panel).Padding(14).Row(r =>
            {
                r.RelativeItem().Column(cc =>
                {
                    cc.Item().Text("Campaign").FontSize(8.5f).FontColor(MutedSlate);
                    cc.Item().PaddingTop(2).Text(campaignName).FontSize(10).Bold().FontColor(Navy);
                });
                r.RelativeItem().Column(cc =>
                {
                    cc.Item().Text("Influencer").FontSize(8.5f).FontColor(MutedSlate);
                    cc.Item().PaddingTop(2).Text(influencerName).FontSize(10).Bold().FontColor(Navy);
                });
                if (invoice.Milestone != null)
                {
                    r.RelativeItem().Column(cc =>
                    {
                        cc.Item().Text("Milestone").FontSize(8.5f).FontColor(MutedSlate);
                        cc.Item().PaddingTop(2).Text($"Milestone {invoice.Milestone.MilestoneNumber}").FontSize(10).Bold().FontColor(Navy);
                    });
                }
            });

            // Line items
            var hasFee = invoice.PlatformFeeInPence > 0;
            col.Item().PaddingTop(22).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(4);
                    c.RelativeColumn(1.3f);
                });

                table.Header(h =>
                {
                    h.Cell().BorderBottom(1.3f).BorderColor(Navy).PaddingBottom(8)
                        .Text("DESCRIPTION").FontSize(8.5f).Bold().FontColor(Navy).LetterSpacing(0.03f);
                    h.Cell().BorderBottom(1.3f).BorderColor(Navy).PaddingBottom(8).AlignRight()
                        .Text("AMOUNT").FontSize(8.5f).Bold().FontColor(Navy).LetterSpacing(0.03f);
                });

                var description = invoice.Milestone != null
                    ? $"{campaignName} — Milestone {invoice.Milestone.MilestoneNumber} payment to {influencerName}"
                    : $"{campaignName} — payment to {influencerName}";

                table.Cell().BorderBottom(1).BorderColor(LineColor).PaddingVertical(12)
                    .Text(description).FontSize(10).FontColor(Navy);
                table.Cell().BorderBottom(1).BorderColor(LineColor).PaddingVertical(12).AlignRight()
                    .Text(FormatAmount(invoice.SubtotalInPence, invoice.Currency)).FontSize(10).FontColor(Navy);

                if (hasFee)
                {
                    table.Cell().BorderBottom(1).BorderColor(LineColor).PaddingVertical(12)
                        .Text($"Platform fee ({feePercent:0.#}%)").FontSize(10).FontColor(Slate);
                    table.Cell().BorderBottom(1).BorderColor(LineColor).PaddingVertical(12).AlignRight()
                        .Text(FormatAmount(invoice.PlatformFeeInPence, invoice.Currency)).FontSize(10).FontColor(Slate);
                }
            });

            // Total
            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem(2f);
                row.RelativeItem(2f).Background(Navy).Padding(14).Row(r =>
                {
                    r.AutoItem().AlignMiddle().Text(isPaid ? "Total paid" : "Total due").FontSize(10).FontColor(Colors.White);
                    r.RelativeItem().AlignRight().Text(FormatAmount(invoice.TotalAmountInPence, invoice.Currency)).FontSize(13).Bold().FontColor(Colors.White);
                });
            });

            if (isPaid)
            {
                col.Item().PaddingTop(8).AlignRight()
                    .Text($"Paid on {invoice.PaidAt!.Value:dd MMM yyyy}").FontSize(9).FontColor(MutedSlate);
            }

            // Footer notes
            col.Item().PaddingTop(32).LineHorizontal(1).LineColor(LineColor);
            col.Item().PaddingTop(14).Text("This invoice was generated automatically by Inflan and reflects a payment processed through the platform on behalf of the campaign above.")
                .FontSize(8.5f).FontColor(MutedSlate);
            col.Item().PaddingTop(4).Text("Questions about this invoice? Email support@inflan.com.")
                .FontSize(8.5f).FontColor(MutedSlate);
        });
    }

    private static void ComposeFooter(IContainer container, string label, string reference)
    {
        container.Column(col =>
        {
            col.Item().PaddingHorizontal(2.2f, Unit.Centimetre).PaddingBottom(10).Row(row =>
            {
                row.RelativeItem().Text("Inflan Platform · www.inflan.com").FontSize(8).FontColor(MutedSlate);
                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.Span($"{label} ").FontSize(8).FontColor(MutedSlate);
                    text.Span(reference).FontSize(8).FontColor(MutedSlate);
                });
            });
            col.Item().Height(5).Background(Navy);
        });
    }

    private static void ComposeCampaignContent(
        IContainer container,
        string brandName,
        string brandEmail,
        string influencerName,
        string campaignName,
        string statusLabel,
        DateOnly startDate,
        DateOnly endDate,
        string currency,
        List<CampaignInvoiceRow> rows,
        long grandTotal,
        long paidTotal,
        long outstandingTotal,
        bool includesFeeNote)
    {
        container.PaddingHorizontal(2.2f, Unit.Centimetre).PaddingTop(1f, Unit.Centimetre).Column(col =>
        {
            col.Item().LineHorizontal(1).LineColor(LineColor);

            // Billed by / Billed to / Generated date + campaign status
            col.Item().PaddingTop(20).Row(row =>
            {
                row.Spacing(16);

                row.RelativeItem(1.1f).Column(c =>
                {
                    c.Item().Text("Billed by").FontSize(8.5f).Bold().FontColor(MutedSlate).LetterSpacing(0.03f);
                    c.Item().PaddingTop(6).Text("Inflan Platform").FontSize(11).Bold().FontColor(Navy);
                    c.Item().PaddingTop(2).Text("support@inflan.com").FontSize(9.5f).FontColor(Slate);
                    c.Item().Text("www.inflan.com").FontSize(9.5f).FontColor(Slate);
                });

                row.RelativeItem(1.1f).Column(c =>
                {
                    c.Item().Text("Billed to").FontSize(8.5f).Bold().FontColor(MutedSlate).LetterSpacing(0.03f);
                    c.Item().PaddingTop(6).Text(brandName).FontSize(11).Bold().FontColor(Navy);
                    if (!string.IsNullOrWhiteSpace(brandEmail))
                        c.Item().PaddingTop(2).Text(brandEmail).FontSize(9.5f).FontColor(Slate);
                });

                row.RelativeItem(1f).Column(c =>
                {
                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Generated").FontSize(8.5f).FontColor(MutedSlate);
                        r.RelativeItem().AlignRight().Text(DateTime.UtcNow.ToString("dd MMM yyyy")).FontSize(9.5f).FontColor(Navy);
                    });
                    c.Item().PaddingTop(8).Row(r =>
                    {
                        r.RelativeItem().Text("Campaign").FontSize(8.5f).FontColor(MutedSlate);
                        r.RelativeItem().AlignRight().Element(e =>
                        {
                            e.Background(AccentBlueBg).PaddingVertical(2).PaddingHorizontal(8)
                             .Text(statusLabel.ToUpper()).FontSize(8).Bold().FontColor(AccentBlue);
                        });
                    });
                });
            });

            // Campaign details panel
            col.Item().PaddingTop(22).Background(Panel).Padding(14).Row(r =>
            {
                r.RelativeItem(1.6f).Column(cc =>
                {
                    cc.Item().Text("Campaign").FontSize(8.5f).FontColor(MutedSlate);
                    cc.Item().PaddingTop(2).Text(campaignName).FontSize(10).Bold().FontColor(Navy);
                });
                r.RelativeItem().Column(cc =>
                {
                    cc.Item().Text("Influencer").FontSize(8.5f).FontColor(MutedSlate);
                    cc.Item().PaddingTop(2).Text(influencerName).FontSize(10).Bold().FontColor(Navy);
                });
                r.RelativeItem().Column(cc =>
                {
                    cc.Item().Text("Campaign period").FontSize(8.5f).FontColor(MutedSlate);
                    cc.Item().PaddingTop(2)
                        .Text($"{startDate:dd MMM yyyy} – {endDate:dd MMM yyyy}")
                        .FontSize(10).Bold().FontColor(Navy);
                });
            });

            // Summary cards: Total / Paid / Outstanding
            col.Item().PaddingTop(18).Row(row =>
            {
                row.Spacing(12);
                row.RelativeItem().Border(1).BorderColor(LineColor).Padding(12).Column(c =>
                {
                    c.Item().Text("CAMPAIGN TOTAL").FontSize(7.5f).Bold().FontColor(MutedSlate).LetterSpacing(0.03f);
                    c.Item().PaddingTop(5).Text(FormatAmount(grandTotal, currency)).FontSize(15).Bold().FontColor(Navy);
                });
                row.RelativeItem().Background(PaidGreenBg).Padding(12).Column(c =>
                {
                    c.Item().Text("PAID TO DATE").FontSize(7.5f).Bold().FontColor(PaidGreen).LetterSpacing(0.03f);
                    c.Item().PaddingTop(5).Text(FormatAmount(paidTotal, currency)).FontSize(15).Bold().FontColor(PaidGreen);
                });
                var outstandingColor = outstandingTotal > 0 ? PendingAmber : PaidGreen;
                var outstandingBg = outstandingTotal > 0 ? PendingAmberBg : PaidGreenBg;
                row.RelativeItem().Background(outstandingBg).Padding(12).Column(c =>
                {
                    c.Item().Text("OUTSTANDING").FontSize(7.5f).Bold().FontColor(outstandingColor).LetterSpacing(0.03f);
                    c.Item().PaddingTop(5).Text(FormatAmount(outstandingTotal, currency)).FontSize(15).Bold().FontColor(outstandingColor);
                });
            });

            // Milestone / line item table
            col.Item().PaddingTop(22).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3.2f);
                    c.RelativeColumn(1.6f);
                    c.RelativeColumn(1.2f);
                    c.RelativeColumn(1.4f);
                });

                table.Header(h =>
                {
                    h.Cell().BorderBottom(1.3f).BorderColor(Navy).PaddingBottom(8).Text("DESCRIPTION").FontSize(8.5f).Bold().FontColor(Navy).LetterSpacing(0.03f);
                    h.Cell().BorderBottom(1.3f).BorderColor(Navy).PaddingBottom(8).Text("DATE").FontSize(8.5f).Bold().FontColor(Navy).LetterSpacing(0.03f);
                    h.Cell().BorderBottom(1.3f).BorderColor(Navy).PaddingBottom(8).Text("STATUS").FontSize(8.5f).Bold().FontColor(Navy).LetterSpacing(0.03f);
                    h.Cell().BorderBottom(1.3f).BorderColor(Navy).PaddingBottom(8).AlignRight().Text("AMOUNT").FontSize(8.5f).Bold().FontColor(Navy).LetterSpacing(0.03f);
                });

                foreach (var row in rows)
                {
                    var (label, fg, bg) = row.Status switch
                    {
                        (int)Utils.MilestoneStatus.PAID => ("PAID", PaidGreen, PaidGreenBg),
                        (int)Utils.MilestoneStatus.OVERDUE => ("OVERDUE", OverdueRed, OverdueRedBg),
                        _ => ("PENDING", PendingAmber, PendingAmberBg),
                    };
                    var dateLabel = row.PaidAt.HasValue ? $"Paid {row.PaidAt:dd MMM yyyy}" : $"Due {row.DueDate:dd MMM yyyy}";

                    table.Cell().BorderBottom(1).BorderColor(LineColor).PaddingVertical(11).Text(row.Description).FontSize(10).FontColor(Navy);
                    table.Cell().BorderBottom(1).BorderColor(LineColor).PaddingVertical(11).Text(dateLabel).FontSize(9).FontColor(Slate);
                    table.Cell().BorderBottom(1).BorderColor(LineColor).PaddingVertical(11).Element(e =>
                        e.Background(bg).PaddingVertical(2).PaddingHorizontal(6).AlignLeft().Width(58)
                         .Text(label).FontSize(7.5f).Bold().FontColor(fg));
                    table.Cell().BorderBottom(1).BorderColor(LineColor).PaddingVertical(11).AlignRight()
                        .Text(FormatAmount(row.AmountInPence, currency)).FontSize(10).FontColor(Navy);
                }
            });

            if (includesFeeNote)
            {
                col.Item().PaddingTop(10).AlignRight()
                    .Text("Amounts shown include the Inflan platform fee.").FontSize(8).FontColor(MutedSlate);
            }

            // Footer notes
            col.Item().PaddingTop(28).LineHorizontal(1).LineColor(LineColor);
            col.Item().PaddingTop(14).Text("This statement reflects the full billing position of this campaign at the time it was generated, across every milestone whether paid or still pending.")
                .FontSize(8.5f).FontColor(MutedSlate);
            col.Item().PaddingTop(4).Text("Questions about this statement? Email support@inflan.com.")
                .FontSize(8.5f).FontColor(MutedSlate);
        });
    }

    private static string FormatAmount(long amountInPence, string currency)
    {
        var amount = amountInPence / 100.0m;
        return currency.ToUpper() switch
        {
            "GBP" => $"£{amount:N2}",
            "NGN" => $"₦{amount:N2}",
            "USD" => $"${amount:N2}",
            _ => $"{currency} {amount:N2}"
        };
    }
}
