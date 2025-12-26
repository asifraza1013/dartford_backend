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

    public InvoicePdfService(ILogger<InvoicePdfService> logger)
    {
        _logger = logger;
    }

    public async Task<byte[]> GenerateInvoicePdfAsync(Invoice invoice)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return await Task.Run(() =>
        {
            using var stream = new MemoryStream();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Black));

                    // Header
                    page.Header().Element(ComposeHeader);

                    // Content
                    page.Content().PaddingVertical(0.5f, Unit.Centimetre).Element(c => ComposeContent(c, invoice));

                    // Footer
                    page.Footer().Element(ComposeFooter);
                });
            })
            .GeneratePdf(stream);

            return stream.ToArray();
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("INVOICE")
                    .FontSize(28).Bold().FontColor("#3B71FE");

                column.Item().Text("Inflan Platform")
                    .FontSize(12).FontColor(Colors.Grey.Darken2);
            });

            row.ConstantItem(120).AlignRight().Column(column =>
            {
                column.Item().AlignRight().Text("Inflan")
                    .FontSize(16).Bold().FontColor("#3B71FE");
                column.Item().AlignRight().Text("influencer marketing platform")
                    .FontSize(8).FontColor(Colors.Grey.Medium);
            });
        });
    }

    private void ComposeContent(IContainer container, Invoice invoice)
    {
        container.Column(column =>
        {
            column.Spacing(15);

            // Invoice Details Row
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Invoice Number").Bold().FontColor(Colors.Grey.Darken2);
                    col.Item().Text(invoice.InvoiceNumber).FontSize(12);
                });

                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().AlignRight().Text("Invoice Date").Bold().FontColor(Colors.Grey.Darken2);
                    col.Item().AlignRight().Text(invoice.IssuedAt.ToString("dd MMM yyyy")).FontSize(12);
                });
            });

            // Divider
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

            // From / To Section
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("From").Bold().FontColor("#3B71FE");
                    col.Item().PaddingTop(5).Text(invoice.Brand?.BrandName ?? invoice.Brand?.Name ?? "Brand").Bold();
                    col.Item().Text(invoice.Brand?.Email ?? "");
                });

                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().AlignRight().Text("To").Bold().FontColor("#3B71FE");
                    col.Item().AlignRight().PaddingTop(5).Text(invoice.Influencer?.Name ?? "Influencer").Bold();
                    col.Item().AlignRight().Text(invoice.Influencer?.Email ?? "");
                });
            });

            // Campaign Details
            column.Item().PaddingTop(10).Background("#F9FAFB").Padding(15).Column(campaignCol =>
            {
                campaignCol.Item().Text("Campaign Details").Bold().FontColor("#3B71FE");
                campaignCol.Item().PaddingTop(8).Row(r =>
                {
                    r.RelativeItem().Text("Campaign:").FontColor(Colors.Grey.Darken2);
                    r.RelativeItem().AlignRight().Text(invoice.Campaign?.ProjectName ?? $"Campaign #{invoice.CampaignId}").Bold();
                });
                if (invoice.Milestone != null)
                {
                    campaignCol.Item().PaddingTop(5).Row(r =>
                    {
                        r.RelativeItem().Text("Milestone:").FontColor(Colors.Grey.Darken2);
                        r.RelativeItem().AlignRight().Text($"Milestone {invoice.Milestone.MilestoneNumber}").Bold();
                    });
                }
            });

            // Amount Breakdown Table
            column.Item().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1);
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Background("#3B71FE").Padding(8).Text("Description").FontColor(Colors.White).Bold();
                    header.Cell().Background("#3B71FE").Padding(8).AlignRight().Text("Amount").FontColor(Colors.White).Bold();
                });

                // Rows
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                    .Text("Campaign Payment");
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                    .AlignRight().Text(FormatAmount(invoice.SubtotalInPence, invoice.Currency));

                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                    .Text("Platform Fee (2%)");
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(8)
                    .AlignRight().Text(FormatAmount(invoice.PlatformFeeInPence, invoice.Currency));

                // Total
                table.Cell().Background("#F9FAFB").Padding(8).Text("Total").Bold().FontSize(12);
                table.Cell().Background("#F9FAFB").Padding(8).AlignRight()
                    .Text(FormatAmount(invoice.TotalAmountInPence, invoice.Currency)).Bold().FontSize(12).FontColor("#3B71FE");
            });

            // Payment Status
            column.Item().PaddingTop(15).Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(150).Background(invoice.PaidAt.HasValue ? "#ECFDF3" : "#FFFAEB")
                    .Padding(10).AlignCenter().Text(invoice.PaidAt.HasValue ? "PAID" : "PENDING")
                    .Bold().FontColor(invoice.PaidAt.HasValue ? "#027A48" : "#B54708");
            });

            if (invoice.PaidAt.HasValue)
            {
                column.Item().AlignRight().Text($"Paid on: {invoice.PaidAt.Value:dd MMM yyyy}")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            }

            // Notes Section
            column.Item().PaddingTop(20).Column(notesCol =>
            {
                notesCol.Item().Text("Notes").Bold().FontColor(Colors.Grey.Darken2);
                notesCol.Item().PaddingTop(5).Text("Thank you for your business. This invoice was generated automatically by the Inflan platform.")
                    .FontSize(9).FontColor(Colors.Grey.Medium);
                notesCol.Item().PaddingTop(3).Text("For any queries, please contact support@inflan.com")
                    .FontSize(9).FontColor(Colors.Grey.Medium);
            });
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            column.Item().PaddingTop(10).Text("Inflan - Influencer Marketing Platform")
                .FontSize(9).FontColor(Colors.Grey.Medium);
            column.Item().Text("www.inflan.com | support@inflan.com")
                .FontSize(8).FontColor(Colors.Grey.Medium);
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
