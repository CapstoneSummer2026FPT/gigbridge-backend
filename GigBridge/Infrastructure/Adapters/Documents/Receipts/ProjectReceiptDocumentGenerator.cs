using System.Globalization;
using Application.Common.Interfaces.Documents;
using Application.Common.InternalServices.Receipts.Models;
using Application.Common.Interfaces.Templates;
using Domain.Enums;
using Domain.Services.Payments;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Infrastructure.Adapters.Documents.Receipts;

public sealed class ProjectReceiptDocumentGenerator(ITemplateReader templateReader)
    : IProjectReceiptDocumentGenerator
{
    private const string ClientTemplatePath =
        "Receipts/Documents/GigBridge_Client_Transaction_Receipt_Template.docx";
    private const string FreelancerTemplatePath =
        "Receipts/Documents/GigBridge_Freelancer_Transaction_Receipt_Template.docx";
    private const string DocxMimeType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private static readonly CultureInfo VietnamCulture = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    public GeneratedProjectReceiptDocument Generate(
        ProjectReceiptSnapshot snapshot,
        string _)
    {
        var templatePath = snapshot.ReceiptType == (int)ProjectReceiptType.Client
            ? ClientTemplatePath
            : FreelancerTemplatePath;
        using var template = templateReader.OpenRead(templatePath);
        using var output = new MemoryStream();
        template.CopyTo(output);
        output.Position = 0;

        using (var document = WordprocessingDocument.Open(output, true))
        {
            var body = document.MainDocumentPart?.Document?.Body
                ?? throw new InvalidOperationException("The receipt template has no document body.");
            ExpandMilestones(body, snapshot.Milestones);
            ReplaceAll(body, BuildReplacements(snapshot));
            if (body.InnerText.Contains("{{", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The generated receipt contains unresolved placeholders.");
            }

            document.MainDocumentPart!.Document.Save();
            var errors = new OpenXmlValidator(FileFormatVersions.Microsoft365)
                .Validate(document)
                .Take(5)
                .Select(error => error.Description)
                .ToArray();
            if (errors.Length > 0)
            {
                throw new InvalidOperationException(
                    $"The generated receipt document is invalid: {string.Join("; ", errors)}");
            }
        }

        return new GeneratedProjectReceiptDocument(
            output.ToArray(),
            $"GigBridge-{snapshot.ReceiptNumber}.docx",
            DocxMimeType);
    }

    private static void ExpandMilestones(
        Body body,
        IReadOnlyList<ProjectReceiptMilestoneSnapshot> milestones)
    {
        var templateRow = body.Descendants<TableRow>()
            .FirstOrDefault(row => row.InnerText.Contains("{{#Milestones}}", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("The receipt milestone template row was not found.");
        foreach (var milestone in milestones.OrderBy(item => item.Number))
        {
            var row = (TableRow)templateRow.CloneNode(true);
            ReplaceAll(row, new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["{{#Milestones}}"] = string.Empty,
                ["{{/Milestones}}"] = string.Empty,
                ["{{MilestoneNo}}"] = milestone.Number.ToString(VietnamCulture),
                ["{{MilestoneTitle}}"] = milestone.Title,
                ["{{MilestoneStartedAt}}"] = FormatVietnamTime(milestone.StartedAtUtc),
                ["{{MilestoneDueDate}}"] = FormatDate(milestone.DueDate),
                ["{{MilestoneApprovedAt}}"] = FormatVietnamTime(milestone.ApprovedAtUtc),
                ["{{MilestoneDuration}}"] = FormatDuration(
                    milestone.StartedAtUtc,
                    milestone.ApprovedAtUtc),
                ["{{MilestoneValueGigCoin}}"] = FormatGigCoin(milestone.ValueGigCoin),
                ["{{MilestoneReleasedBeforeCompletionGigCoin}}"] = FormatGigCoin(milestone.ReleasedBeforeCompletionGigCoin),
                ["{{MilestoneFinalReleaseGigCoin}}"] = FormatGigCoin(milestone.FinalReleaseGigCoin),
                ["{{MilestoneTotalReleasedGigCoin}}"] = FormatGigCoin(milestone.TotalReleasedGigCoin),
                ["{{MilestoneGrossReleasedGigCoin}}"] = FormatGigCoin(milestone.TotalReleasedGigCoin),
                ["{{MilestoneServiceFeeGigCoin}}"] = FormatGigCoin(milestone.ServiceFeeGigCoin),
                ["{{MilestoneNetReceivedGigCoin}}"] = FormatGigCoin(milestone.NetReceivedGigCoin)
            });
            templateRow.InsertBeforeSelf(row);
        }
        templateRow.Remove();
    }

    private static Dictionary<string, string> BuildReplacements(
        ProjectReceiptSnapshot snapshot) => new(StringComparer.Ordinal)
    {
        ["{{#Milestones}}"] = string.Empty,
        ["{{/Milestones}}"] = string.Empty,
        ["{{ReceiptNumber}}"] = snapshot.ReceiptNumber,
        ["{{IssuedAt}}"] = FormatVietnamTime(snapshot.IssuedAtUtc),
        ["{{ReceiptStatus}}"] = snapshot.ReceiptStatus,
        ["{{ClientFullName}}"] = snapshot.Client.FullName,
        ["{{ClientCompanyName}}"] = snapshot.Client.CompanyName,
        ["{{ClientEmail}}"] = snapshot.Client.Email,
        ["{{ClientIdentityOrTaxCode}}"] = ValueOrUnavailable(snapshot.Client.IdentityOrTaxCode),
        ["{{FreelancerFullName}}"] = snapshot.Freelancer.FullName,
        ["{{FreelancerCompanyName}}"] = snapshot.Freelancer.CompanyName,
        ["{{FreelancerEmail}}"] = snapshot.Freelancer.Email,
        ["{{FreelancerIdentityOrTaxCode}}"] = ValueOrUnavailable(snapshot.Freelancer.IdentityOrTaxCode),
        ["{{ProjectTitle}}"] = snapshot.ProjectTitle,
        ["{{ProjectStartedAt}}"] = FormatProjectStart(snapshot),
        ["{{ProjectCompletedAt}}"] = FormatVietnamTime(snapshot.CompletedAtUtc),
        ["{{ProjectDuration}}"] = FormatDuration(snapshot.ProjectStartedAtUtc, snapshot.CompletedAtUtc),
        ["{{ContractValueGigCoin}}"] = FormatGigCoin(snapshot.ContractValueGigCoin),
        ["{{ContractValueVnd}}"] = FormatVnd(snapshot.ContractValueGigCoin),
        ["{{ClientServiceFeeGigCoin}}"] = FormatGigCoin(snapshot.ClientServiceFeeGigCoin),
        ["{{ClientServiceFeeVnd}}"] = FormatVnd(snapshot.ClientServiceFeeGigCoin),
        ["{{ClientTotalPaidGigCoin}}"] = FormatGigCoin(snapshot.ClientTotalPaidGigCoin),
        ["{{ClientTotalPaidVnd}}"] = FormatVnd(snapshot.ClientTotalPaidGigCoin),
        ["{{TotalReleasedBeforeCompletionGigCoin}}"] = FormatGigCoin(snapshot.TotalReleasedBeforeCompletionGigCoin),
        ["{{TotalReleasedBeforeCompletionVnd}}"] = FormatVnd(snapshot.TotalReleasedBeforeCompletionGigCoin),
        ["{{FinalReleaseGigCoin}}"] = FormatGigCoin(snapshot.FinalReleaseGigCoin),
        ["{{FinalReleaseVnd}}"] = FormatVnd(snapshot.FinalReleaseGigCoin),
        ["{{TotalReleasedGigCoin}}"] = FormatGigCoin(snapshot.TotalReleasedGigCoin),
        ["{{TotalReleasedVnd}}"] = FormatVnd(snapshot.TotalReleasedGigCoin),
        ["{{EscrowRemainingGigCoin}}"] = FormatGigCoin(snapshot.EscrowRemainingGigCoin),
        ["{{EscrowRemainingVnd}}"] = FormatVnd(snapshot.EscrowRemainingGigCoin),
        ["{{FreelancerServiceFeeGigCoin}}"] = FormatGigCoin(snapshot.FreelancerServiceFeeGigCoin),
        ["{{FreelancerServiceFeeVnd}}"] = FormatVnd(snapshot.FreelancerServiceFeeGigCoin),
        ["{{TotalGrossReleasedGigCoin}}"] = FormatGigCoin(snapshot.TotalGrossReleasedGigCoin),
        ["{{TotalGrossReleasedVnd}}"] = FormatVnd(snapshot.TotalGrossReleasedGigCoin),
        ["{{NetReceivedBeforeCompletionGigCoin}}"] = FormatGigCoin(snapshot.NetReceivedBeforeCompletionGigCoin),
        ["{{NetReceivedBeforeCompletionVnd}}"] = FormatVnd(snapshot.NetReceivedBeforeCompletionGigCoin),
        ["{{FinalNetReceivedGigCoin}}"] = FormatGigCoin(snapshot.FinalNetReceivedGigCoin),
        ["{{FinalNetReceivedVnd}}"] = FormatVnd(snapshot.FinalNetReceivedGigCoin),
        ["{{FreelancerNetReceivedGigCoin}}"] = FormatGigCoin(snapshot.FreelancerNetReceivedGigCoin),
        ["{{FreelancerNetReceivedVnd}}"] = FormatVnd(snapshot.FreelancerNetReceivedGigCoin),
        ["{{VndPerGigCoin}}"] = snapshot.VndPerGigCoin.ToString("N0", VietnamCulture)
    };

    private static string FormatProjectStart(ProjectReceiptSnapshot snapshot)
    {
        if (snapshot.ProjectStartedAtUtc.HasValue)
        {
            return FormatVietnamTime(snapshot.ProjectStartedAtUtc);
        }

        return FormatDate(snapshot.ContractStartDate);
    }

    private static void ReplaceAll(OpenXmlElement root, IReadOnlyDictionary<string, string> replacements)
    {
        foreach (var paragraph in root.Descendants<Paragraph>().ToArray())
        {
            foreach (var replacement in replacements)
            {
                ReplaceText(paragraph, replacement.Key, replacement.Value);
            }
        }
    }

    private static void ReplaceText(Paragraph paragraph, string oldValue, string newValue)
    {
        while (true)
        {
            var nodes = paragraph.Descendants<Text>().ToArray();
            var combined = string.Concat(nodes.Select(node => node.Text));
            var index = combined.IndexOf(oldValue, StringComparison.Ordinal);
            if (index < 0) return;

            var end = index + oldValue.Length;
            var offset = 0;
            Text? startNode = null;
            Text? endNode = null;
            var startOffset = 0;
            var endOffset = 0;
            foreach (var node in nodes)
            {
                var next = offset + node.Text.Length;
                if (startNode is null && index < next)
                {
                    startNode = node;
                    startOffset = index - offset;
                }
                if (end <= next)
                {
                    endNode = node;
                    endOffset = end - offset;
                    break;
                }
                offset = next;
            }
            if (startNode is null || endNode is null)
            {
                throw new InvalidOperationException($"Unable to replace receipt placeholder '{oldValue}'.");
            }

            var prefix = startNode.Text[..startOffset];
            var suffix = endNode.Text[endOffset..];
            startNode.Text = prefix + newValue + suffix;
            startNode.Space = SpaceProcessingModeValues.Preserve;
            var clearing = false;
            foreach (var node in nodes)
            {
                if (node == startNode) clearing = true;
                if (clearing && node != startNode) node.Text = string.Empty;
                if (node == endNode) break;
            }
        }
    }

    private static string FormatDate(DateOnly? value) =>
        value?.ToString("dd/MM/yyyy", VietnamCulture) ?? "Không áp dụng";

    private static string ValueOrUnavailable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Không cung cấp" : value.Trim();

    private static string FormatVietnamTime(DateTime? value)
    {
        if (!value.HasValue) return "Không áp dụng";
        var utc = DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, VietnamTimeZone)
            .ToString("dd/MM/yyyy HH:mm:ss", VietnamCulture);
    }

    private static string FormatGigCoin(decimal value) => value.ToString("0.####", VietnamCulture);
    private static string FormatVnd(decimal gigCoin) =>
        TokenWalletRules.ToVnd(gigCoin).ToString("N0", VietnamCulture);

    private static string FormatDuration(DateTime? startedAtUtc, DateTime? completedAtUtc)
    {
        if (!startedAtUtc.HasValue || !completedAtUtc.HasValue || completedAtUtc < startedAtUtc)
        {
            return "Không áp dụng";
        }

        var duration = completedAtUtc.Value - startedAtUtc.Value;
        var parts = new List<string>(4);
        if (duration.Days > 0) parts.Add($"{duration.Days} ngày");
        if (duration.Hours > 0) parts.Add($"{duration.Hours} giờ");
        if (duration.Minutes > 0) parts.Add($"{duration.Minutes} phút");
        if (duration.Seconds > 0 || parts.Count == 0) parts.Add($"{duration.Seconds} giây");
        return string.Join(" ", parts);
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
    }
}
