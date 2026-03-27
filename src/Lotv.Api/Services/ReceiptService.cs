using Lotv.Api.Data;
using Lotv.Core.Common;
using Lotv.Core.Models;
using Lotv.Core.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Lotv.Api.Services;

/// <summary>
/// Generates IRS-compliant charitable contribution receipts.
/// Receipts are returned as structured HTML strings ready for email delivery or PDF conversion.
/// PDF rendering requires a future integration with a library such as QuestPDF or a cloud renderer.
/// </summary>
public class ReceiptService(LotvDbContext db, INotificationService notify) : IReceiptService
{
    private const string OrgName   = "Lily of the Valley Ministry";
    private const string OrgEin    = "XX-XXXXXXX";  // replace with real EIN before launch
    private const string OrgAddress = "To be configured";

    public async Task<Result> SendReceiptAsync(int donationId)
    {
        var donation = await db.Donations
            .Include(d => d.Donor)
            .FirstOrDefaultAsync(d => d.Id == donationId);

        if (donation is null)
            return Result.Fail("Donation not found.");

        var donor = donation.Donor;
        if (donor is null)
            return Result.Fail("Donor not found for this donation.");

        var html = BuildReceiptHtml(donor, donation);

        if (!donor.IsAnonymous && !string.IsNullOrEmpty(donor.Email))
            await notify.SendEmailAsync(donor.Email, donor.FullName,
                "Your Donation Receipt — LOTV Ministry", html);

        return Result.Ok();
    }

    public async Task<Result> SendYearEndStatementAsync(int donorId, int year)
    {
        var donor = await db.Donors.FindAsync(donorId);
        if (donor is null) return Result.Fail("Donor not found.");

        var donations = await db.Donations
            .Where(d => d.DonorId == donorId && d.Date.Year == year)
            .OrderBy(d => d.Date)
            .ToListAsync();

        if (!donations.Any())
            return Result.Fail($"No donations found for {year}.");

        var html = BuildYearEndHtml(donor, donations, year);

        if (!donor.IsAnonymous && !string.IsNullOrEmpty(donor.Email))
            await notify.SendEmailAsync(donor.Email, donor.FullName,
                $"{year} Year-End Giving Statement — LOTV Ministry", html);

        return Result.Ok();
    }

    /// <summary>
    /// Returns a formatted HTML receipt for use by the GET /api/v1/donations/{id}/receipt endpoint.
    /// </summary>
    public async Task<(bool Found, string? Html)> GetReceiptHtmlAsync(int donationId)
    {
        var donation = await db.Donations
            .Include(d => d.Donor)
            .FirstOrDefaultAsync(d => d.Id == donationId);

        if (donation is null) return (false, null);

        var html = BuildReceiptHtml(donation.Donor!, donation);
        return (true, html);
    }

    /// <summary>
    /// Returns a formatted HTML year-end giving statement.
    /// </summary>
    public async Task<(bool Found, string? Html)> GetYearEndHtmlAsync(int donorId, int year)
    {
        var donor = await db.Donors.FindAsync(donorId);
        if (donor is null) return (false, null);

        var donations = await db.Donations
            .Where(d => d.DonorId == donorId && d.Date.Year == year)
            .OrderBy(d => d.Date)
            .ToListAsync();

        var html = BuildYearEndHtml(donor, donations, year);
        return (true, html);
    }

    // ── HTML builders ─────────────────────────────────────────────────────────

    private static string BuildReceiptHtml(Donor donor, Donation donation)
    {
        var donorName  = donor.IsAnonymous ? "Anonymous Donor" : donor.FullName;
        var confirmId  = $"LOTV-{donation.Id:D6}-{donation.Date:yyyyMMdd}";

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8"><title>Donation Receipt</title></head>
            <body style="font-family:Georgia,serif;max-width:640px;margin:40px auto;color:#333">
              <h2 style="color:#1a4a6b">{OrgName}</h2>
              <p>EIN: {OrgEin} | {OrgAddress}</p>
              <hr/>
              <h3>Charitable Contribution Receipt</h3>
              <table style="width:100%;border-collapse:collapse">
                <tr><td><strong>Donor:</strong></td><td>{donorName}</td></tr>
                <tr><td><strong>Date:</strong></td><td>{donation.Date:MMMM d, yyyy}</td></tr>
                <tr><td><strong>Amount:</strong></td><td>{donation.Amount:C}</td></tr>
                <tr><td><strong>Channel:</strong></td><td>{donation.Channel}</td></tr>
                <tr><td><strong>Confirmation #:</strong></td><td>{confirmId}</td></tr>
              </table>
              <p style="margin-top:24px;font-size:0.9em;color:#555">
                <strong>No goods or services were provided in exchange for this contribution.</strong>
                This letter serves as your official receipt for federal income tax purposes
                under IRC § 170.
              </p>
              <p style="font-size:0.8em;color:#888">
                Thank you for your generous support of {OrgName}.
              </p>
            </body>
            </html>
            """;
    }

    private static string BuildYearEndHtml(Donor donor, List<Donation> donations, int year)
    {
        var donorName  = donor.IsAnonymous ? "Anonymous Donor" : donor.FullName;
        var totalGiven = donations.Sum(d => d.Amount);
        var rows = string.Join("\n", donations.Select(d =>
            $"<tr><td>{d.Date:MM/dd/yyyy}</td><td>{d.Channel}</td><td style='text-align:right'>{d.Amount:C}</td></tr>"));

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8"><title>{year} Year-End Giving Statement</title></head>
            <body style="font-family:Georgia,serif;max-width:640px;margin:40px auto;color:#333">
              <h2 style="color:#1a4a6b">{OrgName}</h2>
              <p>EIN: {OrgEin} | {OrgAddress}</p>
              <hr/>
              <h3>{year} Year-End Giving Statement</h3>
              <p><strong>Donor:</strong> {donorName}</p>
              <table style="width:100%;border-collapse:collapse;margin-top:16px">
                <thead>
                  <tr style="background:#f0f4f8">
                    <th style="text-align:left;padding:6px">Date</th>
                    <th style="text-align:left;padding:6px">Channel</th>
                    <th style="text-align:right;padding:6px">Amount</th>
                  </tr>
                </thead>
                <tbody>
                  {rows}
                </tbody>
                <tfoot>
                  <tr style="border-top:2px solid #333">
                    <td colspan="2"><strong>Total Contributions — {year}</strong></td>
                    <td style="text-align:right"><strong>{totalGiven:C}</strong></td>
                  </tr>
                </tfoot>
              </table>
              <p style="margin-top:24px;font-size:0.9em;color:#555">
                <strong>No goods or services were provided in exchange for these contributions.</strong>
                This statement serves as your official record for federal income tax purposes
                under IRC § 170.
              </p>
              <p style="font-size:0.8em;color:#888">
                Thank you for your generous support of {OrgName}.
              </p>
            </body>
            </html>
            """;
    }
}
