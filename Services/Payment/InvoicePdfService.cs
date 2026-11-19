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

                    page.Header().Element(c => ComposeHeader(c, logo, invoice.InvoiceNumber));
                    page.Content().Element(c => ComposeContent(
                        c, invoice, brandName, brandEmail, influencerName, campaignName, isPaid, feePercent));
                    page.Footer().Element(c => ComposeFooter(c, invoice.InvoiceNumber));
                });
            }).GeneratePdf(stream);

            return stream.ToArray();
        });
    }

    private static void ComposeHeader(IContainer container, byte[]? logo, string invoiceNumber)
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
                    c.Item().Text("INVOICE").FontSize(22).Bold().FontColor(Navy).LetterSpacing(0.05f);
                    c.Item().PaddingTop(4).Text(invoiceNumber).FontSize(10).FontColor(MutedSlate);
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

    private static void ComposeFooter(IContainer container, string invoiceNumber)
    {
        container.Column(col =>
        {
            col.Item().PaddingHorizontal(2.2f, Unit.Centimetre).PaddingBottom(10).Row(row =>
            {
                row.RelativeItem().Text("Inflan Platform · www.inflan.com").FontSize(8).FontColor(MutedSlate);
                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.Span("Invoice ").FontSize(8).FontColor(MutedSlate);
                    text.Span(invoiceNumber).FontSize(8).FontColor(MutedSlate);
                });
            });
            col.Item().Height(5).Background(Navy);
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
