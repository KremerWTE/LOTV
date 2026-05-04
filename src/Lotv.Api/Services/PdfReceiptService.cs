using Lotv.Api.Data;
using Lotv.Core.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Lotv.Api.Services;

public class PdfReceiptService(LotvDbContext db)
{
    static PdfReceiptService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private const string OrgName    = "Lily of the Valley Ministry";
    private const string OrgEin     = "XX-XXXXXXX";
    private const string OrgAddress = "To be configured";

    public async Task<byte[]?> RenderReceiptAsync(int donationId)
    {
        var donation = await db.Donations
            .Include(d => d.Donor)
            .FirstOrDefaultAsync(d => d.Id == donationId);
        if (donation is null || donation.Donor is null) return null;

        var donor = donation.Donor;
        var donorName = donor.IsAnonymous ? "Anonymous Donor" : donor.FullName;
        var confirmId = $"LOTV-{donation.Id:D6}-{donation.Date:yyyyMMdd}";

        return Document.Create(c =>
        {
            c.Page(p =>
            {
                p.Margin(40);
                p.Size(PageSizes.Letter);
                p.DefaultTextStyle(t => t.FontFamily(Fonts.Georgia).FontSize(11));

                p.Header().Column(col =>
                {
                    col.Item().Text(OrgName).FontSize(20).Bold().FontColor("#1a4a6b");
                    col.Item().Text($"EIN: {OrgEin}   |   {OrgAddress}").FontSize(9).FontColor("#666");
                });

                p.Content().PaddingVertical(18).Column(col =>
                {
                    col.Spacing(8);
                    col.Item().LineHorizontal(1).LineColor("#ccc");
                    col.Item().Text("Charitable Contribution Receipt").FontSize(14).Bold();

                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(2); });
                        var pairs = new (string, string)[]
                        {
                            ("Donor",          donorName),
                            ("Date",           donation.Date.ToString("MMMM d, yyyy")),
                            ("Amount",         donation.Amount.ToString("C")),
                            ("Channel",        donation.Channel.ToString()),
                            ("Confirmation #", confirmId),
                        };
                        foreach (var (label, value) in pairs)
                        {
                            t.Cell().Padding(2).Text(label + ":").Bold();
                            t.Cell().Padding(2).Text(value);
                        }
                    });

                    col.Item().PaddingTop(20).Text(
                        "No goods or services were provided in exchange for this contribution. " +
                        "This letter serves as your official receipt for federal income tax purposes under IRC § 170."
                    ).FontSize(9).FontColor("#555");

                    col.Item().PaddingTop(8).Text($"Thank you for your generous support of {OrgName}.")
                        .FontSize(9).FontColor("#888");
                });
            });
        }).GeneratePdf();
    }

    public async Task<byte[]?> RenderYearEndAsync(int donorId, int year)
    {
        var donor = await db.Donors.FindAsync(donorId);
        if (donor is null) return null;

        var donations = await db.Donations
            .Where(d => d.DonorId == donorId && d.Date.Year == year)
            .OrderBy(d => d.Date)
            .ToListAsync();

        var donorName = donor.IsAnonymous ? "Anonymous Donor" : donor.FullName;
        var total = donations.Sum(d => d.Amount);

        return Document.Create(c =>
        {
            c.Page(p =>
            {
                p.Margin(40);
                p.Size(PageSizes.Letter);
                p.DefaultTextStyle(t => t.FontFamily(Fonts.Georgia).FontSize(11));

                p.Header().Column(col =>
                {
                    col.Item().Text(OrgName).FontSize(20).Bold().FontColor("#1a4a6b");
                    col.Item().Text($"EIN: {OrgEin}   |   {OrgAddress}").FontSize(9).FontColor("#666");
                });

                p.Content().PaddingVertical(18).Column(col =>
                {
                    col.Spacing(8);
                    col.Item().LineHorizontal(1).LineColor("#ccc");
                    col.Item().Text($"{year} Year-End Giving Statement").FontSize(14).Bold();
                    col.Item().Text($"Donor: {donorName}").Bold();

                    col.Item().PaddingTop(8).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); });
                        t.Header(h =>
                        {
                            h.Cell().Background("#f0f4f8").Padding(4).Text("Date").Bold();
                            h.Cell().Background("#f0f4f8").Padding(4).Text("Channel").Bold();
                            h.Cell().Background("#f0f4f8").Padding(4).AlignRight().Text("Amount").Bold();
                        });
                        foreach (var d in donations)
                        {
                            t.Cell().Padding(4).Text(d.Date.ToString("MM/dd/yyyy"));
                            t.Cell().Padding(4).Text(d.Channel.ToString());
                            t.Cell().Padding(4).AlignRight().Text(d.Amount.ToString("C"));
                        }
                        t.Cell().ColumnSpan(2).Padding(4).BorderTop(2).BorderColor("#333").Text($"Total Contributions — {year}").Bold();
                        t.Cell().Padding(4).BorderTop(2).BorderColor("#333").AlignRight().Text(total.ToString("C")).Bold();
                    });

                    col.Item().PaddingTop(20).Text(
                        "No goods or services were provided in exchange for these contributions. " +
                        "This statement serves as your official record for federal income tax purposes under IRC § 170."
                    ).FontSize(9).FontColor("#555");
                });
            });
        }).GeneratePdf();
    }

}
