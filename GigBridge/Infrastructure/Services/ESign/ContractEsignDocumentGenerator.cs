using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Application.Common.Interfaces.IService;
using Application.Features.Contracts.Common.DTOs;
using Domain.Services.Payments;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace Infrastructure.Services.ESign;

public sealed class ContractEsignDocumentGenerator(HttpClient httpClient) : IContractEsignDocumentGenerator
{
    private const string ResourceName = "Infrastructure.Templates.ESign.GigBridge_Hop_dong_Esign_Template.docx";
    private const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const int MaxSignatureBytes = 5 * 1024 * 1024;
    private static readonly CultureInfo VietnamCulture = CultureInfo.GetCultureInfo("vi-VN");

    public string RenderPreview(ContractDocumentSnapshot snapshot)
    {
        var bytes = Render(snapshot, null, null, ComputeSnapshotHash(snapshot));
        using var stream = new MemoryStream(bytes);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body
            ?? throw new InvalidOperationException("The ESign template has no document body.");
        var html = new StringBuilder("<article class=\"esign-document\">");

        foreach (var element in body.ChildElements)
        {
            if (element is Paragraph paragraph)
            {
                var text = paragraph.InnerText.Trim();
                if (text.Length > 0)
                {
                    html.Append("<p>").Append(WebUtility.HtmlEncode(text)).Append("</p>");
                }
            }
            else if (element is Table table)
            {
                html.Append("<table><tbody>");
                foreach (var row in table.Elements<TableRow>())
                {
                    html.Append("<tr>");
                    foreach (var cell in row.Elements<TableCell>())
                    {
                        html.Append("<td>").Append(WebUtility.HtmlEncode(cell.InnerText.Trim())).Append("</td>");
                    }
                    html.Append("</tr>");
                }
                html.Append("</tbody></table>");
            }
        }

        return html.Append("</article>").ToString();
    }

    public async Task<GeneratedContractDocument> GenerateAsync(
        ContractDocumentSnapshot snapshot,
        ContractSignatureSnapshot? clientSignature,
        ContractSignatureSnapshot? freelancerSignature,
        string documentHash,
        CancellationToken cancellationToken)
    {
        var clientImage = clientSignature is null
            ? null
            : await DownloadSignatureAsync(clientSignature.SignatureImageUrl, cancellationToken);
        var freelancerImage = freelancerSignature is null
            ? null
            : await DownloadSignatureAsync(freelancerSignature.SignatureImageUrl, cancellationToken);
        var content = Render(
            snapshot,
            clientSignature is null || clientImage is null
                ? null
                : new SignatureArtifact(clientSignature, clientImage.Content, clientImage.ContentType),
            freelancerSignature is null || freelancerImage is null
                ? null
                : new SignatureArtifact(freelancerSignature, freelancerImage.Content, freelancerImage.ContentType),
            documentHash);

        return new GeneratedContractDocument(
            content,
            $"GigBridge-{snapshot.ContractCode}.docx",
            DocxMimeType);
    }

    private static byte[] Render(
        ContractDocumentSnapshot snapshot,
        SignatureArtifact? clientSignature,
        SignatureArtifact? freelancerSignature,
        string documentHash)
    {
        using var template = typeof(ContractEsignDocumentGenerator).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded ESign template '{ResourceName}' was not found.");
        using var output = new MemoryStream();
        template.CopyTo(output);
        output.Position = 0;

        using (var document = WordprocessingDocument.Open(output, true))
        {
            var mainPart = document.MainDocumentPart
                ?? throw new InvalidOperationException("The ESign template has no main document part.");
            var body = mainPart.Document?.Body
                ?? throw new InvalidOperationException("The ESign template has no document body.");

            RemoveDeveloperAppendix(body);
            NormalizeTemplateMarkup(body);
            ExpandMilestones(body, snapshot.Milestones);

            if (clientSignature is not null)
            {
                AddSignatureImage(mainPart, body, "{{ClientSignatureImage}}", clientSignature, 1U);
            }

            if (freelancerSignature is not null)
            {
                AddSignatureImage(mainPart, body, "{{FreelancerSignatureImage}}", freelancerSignature, 2U);
            }

            var replacements = BuildReplacements(snapshot, clientSignature?.Signature, freelancerSignature?.Signature, documentHash);
            ReplaceAll(body, replacements);

            if (body.InnerText.Contains("{{", StringComparison.Ordinal))
            {
                var unresolved = Regex.Matches(body.InnerText, @"\{\{.*?\}\}")
                    .Select(match => match.Value)
                    .Distinct(StringComparer.Ordinal)
                    .Take(10);
                throw new InvalidOperationException(
                    $"The final ESign document still contains unresolved placeholders: {string.Join(", ", unresolved)}");
            }

            mainPart.Document.Save();
            var errors = new OpenXmlValidator(FileFormatVersions.Microsoft365)
                .Validate(document).Take(5).Select(error =>
                    $"{error.Description} at {error.Path?.XPath}: {error.Node?.OuterXml}").ToArray();
            if (errors.Length > 0)
            {
                throw new InvalidOperationException($"The generated ESign document is invalid: {string.Join("; ", errors)}");
            }
        }

        return output.ToArray();
    }

    private static void RemoveDeveloperAppendix(Body body)
    {
        var marker = body.Descendants<Paragraph>()
            .FirstOrDefault(paragraph => paragraph.InnerText.Contains("PHỤ LỤC D – DANH MỤC BIẾN", StringComparison.Ordinal));
        if (marker is null)
        {
            throw new InvalidOperationException("The ESign template developer appendix marker was not found.");
        }

        OpenXmlElement topLevel = marker;
        while (topLevel.Parent is not Body)
        {
            topLevel = topLevel.Parent
                ?? throw new InvalidOperationException("The developer appendix is outside the document body.");
        }

        var topLevelIndex = body.ChildElements.ToList().IndexOf(topLevel);
        var remove = body.ChildElements.Skip(topLevelIndex).Where(element => element is not SectionProperties).ToArray();
        foreach (var element in remove)
        {
            element.Remove();
        }
    }

    private static void NormalizeTemplateMarkup(Body body)
    {
        foreach (var properties in body.Descendants<TableCellProperties>())
        {
            var shading = properties.GetFirstChild<Shading>();
            var margins = properties.GetFirstChild<TableCellMargin>();
            var alignment = properties.GetFirstChild<TableCellVerticalAlignment>();

            if (shading is not null)
            {
                shading.Val ??= ShadingPatternValues.Clear;
            }

            foreach (var element in new OpenXmlElement?[] { shading, margins, alignment })
            {
                if (element is null) continue;
                element.Remove();
                properties.Append(element);
            }
        }

        foreach (var header in body.Descendants<TableHeader>())
        {
            header.Val = null;
        }

        foreach (var properties in body.Descendants<ParagraphProperties>())
        {
            var borders = properties.GetFirstChild<ParagraphBorders>();
            var spacing = properties.GetFirstChild<SpacingBetweenLines>();
            if (borders is null || spacing is null) continue;
            borders.Remove();
            properties.InsertBefore(borders, spacing);
        }
    }

    private static void ExpandMilestones(Body body, IReadOnlyList<ContractMilestoneSnapshot> milestones)
    {
        if (milestones.Count == 0)
        {
            throw new InvalidOperationException("At least one milestone is required to create an ESign contract.");
        }

        var templateRow = body.Descendants<TableRow>()
            .FirstOrDefault(row => row.InnerText.Contains("{{#Milestones}}", StringComparison.Ordinal));
        if (templateRow is null)
        {
            throw new InvalidOperationException("The ESign template milestone row was not found.");
        }

        foreach (var milestone in milestones.OrderBy(item => item.Number))
        {
            var row = (TableRow)templateRow.CloneNode(true);
            ReplaceAll(row, BuildMilestoneReplacements(milestone));
            templateRow.InsertBeforeSelf(row);
        }

        templateRow.Remove();
    }

    private static Dictionary<string, string> BuildMilestoneReplacements(ContractMilestoneSnapshot milestone) => new(StringComparer.Ordinal)
    {
        ["{{#Milestones}}"] = string.Empty,
        ["{{/Milestones}}"] = string.Empty,
        ["{{MilestoneNo}}"] = milestone.Number.ToString(VietnamCulture),
        ["{{MilestoneTitle}}"] = milestone.Title,
        ["{{MilestoneDescription}}"] = ValueOr(milestone.Description, "Không áp dụng"),
        ["{{MilestoneDeliverable}}"] = ValueOr(milestone.Deliverable, "Không áp dụng"),
        ["{{MilestoneAcceptanceCriteria}}"] = ValueOr(milestone.AcceptanceCriteria, "Không áp dụng"),
        ["{{MilestoneStartDate}}"] = FormatDate(milestone.StartDate),
        ["{{MilestoneDueDate}}"] = FormatDate(milestone.DueDate),
        ["{{MilestoneRevisionLimit}}"] = ValueOr(milestone.RevisionLimit, "Không áp dụng"),
        // Snapshot amounts are G-coin: the VND column converts to VND, the G-coin column renders the value directly.
        ["{{MilestoneValueVND}}"] = FormatNumber(TokenWalletRules.ToVnd(milestone.ValueVnd)),
        ["{{MilestoneValueGigCoin}}"] = FormatNumber(milestone.ValueVnd)
    };

    private static Dictionary<string, string> BuildReplacements(
        ContractDocumentSnapshot snapshot,
        ContractSignatureSnapshot? clientSignature,
        ContractSignatureSnapshot? freelancerSignature,
        string documentHash)
    {
        var clientFee = snapshot.TotalContractValueVnd * 0.01m;
        var initialRelease = snapshot.TotalContractValueVnd * 0.80m;
        var retention = snapshot.TotalContractValueVnd - initialRelease;
        var effectiveAt = clientSignature is not null && freelancerSignature is not null
            ? new[] { clientSignature.SignedAtUtc, freelancerSignature.SignedAtUtc }.Max()
            : (DateTime?)null;

        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{#Milestones}}"] = string.Empty,
            ["{{/Milestones}}"] = string.Empty,
            ["{{ContractCode}}"] = snapshot.ContractCode,
            ["{{ContractDate}}"] = FormatVietnamTime(snapshot.CreatedAtUtc, false),
            ["{{ContractId}}"] = snapshot.ContractId.ToString("D"),
            ["{{ContractVersion}}"] = snapshot.ContractVersion.ToString(VietnamCulture),
            ["{{TemplateVersion}}"] = snapshot.TemplateVersion.ToString(VietnamCulture),
            ["{{PolicyVersion}}"] = snapshot.PolicyVersion,
            ["{{CreatedAt}}"] = FormatVietnamTime(snapshot.CreatedAtUtc, true),
            ["{{EffectiveAt}}"] = effectiveAt.HasValue ? FormatVietnamTime(effectiveAt.Value, true) : "Chờ đủ chữ ký",
            ["{{JobPostId}}"] = snapshot.JobPostId.ToString("D"),
            ["{{ProposalId}}"] = snapshot.ProposalId?.ToString("D") ?? "Không áp dụng",
            ["{{FinalOfferId}}"] = snapshot.FinalOfferId?.ToString("D") ?? "Không áp dụng",
            ["{{ProjectTitle}}"] = snapshot.ProjectTitle,
            ["{{ProjectDescription}}"] = ValueOr(snapshot.ProjectDescription, "Không áp dụng"),
            ["{{ScopeOfWork}}"] = ValueOr(snapshot.ScopeOfWork, "Không áp dụng"),
            ["{{OutOfScope}}"] = ValueOr(snapshot.OutOfScope, "Không áp dụng"),
            ["{{StartDate}}"] = FormatDate(snapshot.StartDate),
            ["{{EndDate}}"] = FormatDate(snapshot.EndDate),
            ["{{TotalContractValueVND}}"] = FormatNumber(TokenWalletRules.ToVnd(snapshot.TotalContractValueVnd)),
            ["{{TotalContractValueGigCoin}}"] = FormatNumber(snapshot.TotalContractValueVnd),
            ["{{ClientPlatformFeeVND}}"] = FormatNumber(TokenWalletRules.ToVnd(clientFee)),
            ["{{ClientPlatformFeeGigCoin}}"] = FormatNumber(clientFee),
            ["{{ClientFundingTotalVND}}"] = FormatNumber(TokenWalletRules.ToVnd(snapshot.TotalContractValueVnd + clientFee)),
            ["{{ClientFundingTotalGigCoin}}"] = FormatNumber(snapshot.TotalContractValueVnd + clientFee),
            ["{{InitialReleaseTotalVND}}"] = FormatNumber(TokenWalletRules.ToVnd(initialRelease)),
            ["{{InitialReleaseTotalGigCoin}}"] = FormatNumber(initialRelease),
            ["{{RetentionTotalVND}}"] = FormatNumber(TokenWalletRules.ToVnd(retention)),
            ["{{RetentionTotalGigCoin}}"] = FormatNumber(retention),
            ["{{DeliverableFormats}}"] = "Theo mô tả milestone và thỏa thuận trên GigBridge",
            ["{{RepositoryOrFolder}}"] = "Theo thỏa thuận trên GigBridge",
            ["{{RequiredSourceFiles}}"] = "Theo phạm vi công việc và milestone",
            ["{{RequiredDocumentation}}"] = "Theo phạm vi công việc và milestone",
            ["{{CredentialHandoverRequirements}}"] = "Chỉ bàn giao qua kênh an toàn do hai Bên thỏa thuận",
            ["{{OpenSourceComponents}}"] = "Bên B phải khai báo khi bàn giao",
            ["{{ThirdPartyAssetsAndLicenses}}"] = "Bên B phải khai báo khi bàn giao",
            ["{{IpTransferScope}}"] = "Theo Điều 9 của Hợp đồng",
            ["{{PortfolioPermission}}"] = "Cần sự đồng ý của Bên A",
            ["{{SpecialConfidentialInformation}}"] = "Không áp dụng",
            ["{{AdditionalConfidentialityTerm}}"] = "Không áp dụng",
            ["{{AppendixBNotes}}"] = "Không áp dụng",
            ["{{DocumentId}}"] = snapshot.DocumentId.ToString("D"),
            ["{{DocumentHashSHA256}}"] = documentHash,
            ["{{GeneratedAt}}"] = FormatVietnamTime(effectiveAt ?? snapshot.CreatedAtUtc, true),
            ["{{DocumentStorageKey}}"] = $"database://ESignDocuments/{snapshot.DocumentId:D}/FinalizedDocumentContent",
            ["{{PreviousDocumentHash}}"] = ValueOr(snapshot.PreviousDocumentHash, "Không áp dụng")
        };

        AddPartyReplacements(replacements, "Client", snapshot.Client, clientSignature);
        AddPartyReplacements(replacements, "Freelancer", snapshot.Freelancer, freelancerSignature);
        return replacements;
    }

    private static void AddPartyReplacements(
        IDictionary<string, string> replacements,
        string prefix,
        ContractPartySnapshot party,
        ContractSignatureSnapshot? signature)
    {
        replacements[$"{{{{{prefix}FullName}}}}"] = party.FullName;
        replacements[$"{{{{{prefix}UserId}}}}"] = party.UserId.ToString("D");
        replacements[$"{{{{{prefix}ProfileId}}}}"] = party.ProfileId.ToString("D");
        replacements[$"{{{{{prefix}Email}}}}"] = party.Email;
        replacements[$"{{{{{prefix}Phone}}}}"] = ValueOr(party.Phone, "Không cung cấp");
        replacements[$"{{{{{prefix}Address}}}}"] = ValueOr(party.Address, "Không cung cấp");
        replacements[$"{{{{{prefix}IdentityOrTaxCode}}}}"] = ValueOr(party.IdentityOrTaxCode, "Không cung cấp");
        replacements[$"{{{{{prefix}SignStatus}}}}"] = signature is null ? "Chưa ký" : "Đã ký";
        replacements[$"{{{{{prefix}SignedAt}}}}"] = signature is null ? "Chưa ký" : FormatVietnamTime(signature.SignedAtUtc, true);
        replacements[$"{{{{{prefix}SignMethod}}}}"] = signature is null ? "Chưa ký" : "Xác nhận điện tử trên GigBridge";
        replacements[$"{{{{{prefix}SignIp}}}}"] = signature is null ? "Chưa ký" : ValueOr(signature.IpAddress, "Không ghi nhận");
        replacements[$"{{{{{prefix}UserAgent}}}}"] = signature is null ? "Chưa ký" : ValueOr(signature.UserAgent, "Không ghi nhận");
        replacements[$"{{{{{prefix}ConsentVersion}}}}"] = signature is null ? "Chưa ký" : "1.0-DATN";
        replacements[$"{{{{{prefix}SignatureImage}}}}"] = signature is null ? "Chưa ký" : string.Empty;

        if (prefix == "Client")
        {
            replacements["{{ClientRepresentative}}"] = ValueOr(party.Representative, party.FullName);
            replacements["{{ClientRepresentativeTitle}}"] = ValueOr(party.RepresentativeTitle, "Không áp dụng");
        }
        else
        {
            replacements["{{FreelancerMaskedBankAccount}}"] = ValueOr(party.MaskedBankAccount, "Không cung cấp");
        }
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
            if (index < 0)
            {
                return;
            }

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
                throw new InvalidOperationException($"Unable to replace ESign placeholder '{oldValue}'.");
            }

            var prefix = startNode.Text[..startOffset];
            var suffix = endNode.Text[endOffset..];
            if (ReferenceEquals(startNode, endNode))
            {
                startNode.Text = prefix + newValue + suffix;
            }
            else
            {
                var clearing = false;
                foreach (var node in nodes)
                {
                    if (ReferenceEquals(node, startNode))
                    {
                        node.Text = prefix + newValue;
                        clearing = true;
                    }
                    else if (ReferenceEquals(node, endNode))
                    {
                        node.Text = suffix;
                        break;
                    }
                    else if (clearing)
                    {
                        node.Text = string.Empty;
                    }
                }
            }

            startNode.Space = SpaceProcessingModeValues.Preserve;
        }
    }

    private static void AddSignatureImage(
        MainDocumentPart mainPart,
        Body body,
        string placeholder,
        SignatureArtifact artifact,
        uint drawingId)
    {
        var paragraph = body.Descendants<Paragraph>()
            .FirstOrDefault(item => item.InnerText.Contains(placeholder, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Signature placeholder '{placeholder}' was not found.");
        ReplaceText(paragraph, placeholder, string.Empty);

        var imagePart = mainPart.AddImagePart(artifact.ContentType);
        using (var image = new MemoryStream(artifact.Content, writable: false))
        {
            imagePart.FeedData(image);
        }

        var width = Math.Clamp(artifact.Signature.SignatureWidth ?? 300, 100, 600);
        var height = Math.Clamp(artifact.Signature.SignatureHeight ?? 100, 40, 250);
        var cx = width * 9525L;
        var cy = height * 9525L;
        var relationshipId = mainPart.GetIdOfPart(imagePart);

        var drawing = new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = drawingId, Name = $"Signature {drawingId}" },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = drawingId, Name = $"signature-{drawingId}" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relationshipId, CompressionState = A.BlipCompressionValues.Print },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = cx, Cy = cy }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U
            });

        paragraph.AppendChild(new Run(drawing));
    }

    private async Task<DownloadedImage> DownloadSignatureAsync(string imageUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !(uri.Host.Equals("res.cloudinary.com", StringComparison.OrdinalIgnoreCase) ||
              uri.Host.EndsWith(".cloudinary.com", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("The stored signature image URL is not a trusted Cloudinary HTTPS URL.");
        }

        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaxSignatureBytes)
        {
            throw new InvalidOperationException("The signature image exceeds the 5 MB limit.");
        }

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (content.Length == 0 || content.Length > MaxSignatureBytes)
        {
            throw new InvalidOperationException("The signature image is empty or exceeds the 5 MB limit.");
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is not ("image/png" or "image/jpeg"))
        {
            throw new InvalidOperationException("The stored signature has an unsupported image type.");
        }

        return new DownloadedImage(content, contentType);
    }

    private static string ComputeSnapshotHash(ContractDocumentSnapshot snapshot) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(snapshot))))
            .ToLowerInvariant();

    private static string FormatNumber(decimal value) => value.ToString("#,##0.###", VietnamCulture);

    private static string FormatDate(DateOnly? value) => value?.ToString("dd/MM/yyyy", VietnamCulture) ?? "Không áp dụng";

    private static string FormatVietnamTime(DateTime value, bool includeTime)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, VietnamTimeZone);
        return local.ToString(includeTime ? "dd/MM/yyyy HH:mm:ss 'ICT'" : "dd/MM/yyyy", VietnamCulture);
    }

    private static TimeZoneInfo VietnamTimeZone
    {
        get
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        }
    }

    private static string ValueOr(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private sealed record DownloadedImage(byte[] Content, string ContentType);
    private sealed record SignatureArtifact(ContractSignatureSnapshot Signature, byte[] Content, string ContentType);
}
