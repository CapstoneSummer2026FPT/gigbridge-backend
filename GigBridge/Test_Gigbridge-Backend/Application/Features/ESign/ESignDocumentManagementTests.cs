using Application.Common.Exceptions;
using Application.Common.InternalServices.ESign.Services;
using Application.Features.ESign.Common.DeleteDocument.Commands;
using Application.Features.ESign.Common.DownloadDocument.Queries;
using Application.Features.ESign.Common.GetDocument.Queries;
using Application.Features.ESign.Common.GetDocumentStatusByContract.Queries;
using Application.Features.ESign.Common.GetDocuments.Queries;
using Application.Features.ESign.Common.Internal;
using Application.Features.ESign.Common.SavePdf.Commands;
using Domain.Entities;
using Domain.Enums.Accounts;
using Domain.Enums.Contracts;
using Domain.Enums.ESign;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Infrastructure.Persistence;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.ESign;

public sealed class ESignDocumentManagementTests
{
    [Fact]
    public async Task ParticipantList_IncludesPendingAndFinalizedContractVersions()
    {
        var fixture = new Fixture();
        var handler = new GetESignDocumentsQueryHandler(fixture.Context);

        var result = await handler.Handle(
            new GetESignDocumentsQuery(fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        var pending = Assert.Single(result.Items, item => item.DocumentId == fixture.PendingDocumentId);
        Assert.Equal((int)ESignerRole.Client, pending.CurrentUserSignerRole);
        Assert.True(pending.CanCurrentUserSign);
        Assert.False(pending.HasFinalArtifact);

        var finalized = Assert.Single(result.Items, item => item.DocumentId == fixture.FinalizedDocumentId);
        Assert.False(finalized.CanCurrentUserSign);
        Assert.True(finalized.HasFinalArtifact);
        Assert.Equal("GB-CONTRACT-FINAL.docx", finalized.FinalizedDocumentFileName);
        Assert.True(finalized.HasPdfArtifact);

        var freelancerResult = await handler.Handle(
            new GetESignDocumentsQuery(
                fixture.FreelancerUserId,
                Status: (int)ESignDocumentStatus.PendingSignatures),
            CancellationToken.None);
        var freelancerPending = Assert.Single(freelancerResult.Items);
        Assert.Equal((int)ESignerRole.Freelancer, freelancerPending.CurrentUserSignerRole);
        Assert.True(freelancerPending.CanCurrentUserSign);
    }

    [Fact]
    public async Task AdminList_SearchesParticipantEmailWithoutGrantingSignerRole()
    {
        var fixture = new Fixture();
        var handler = new GetESignDocumentsQueryHandler(fixture.Context);

        var result = await handler.Handle(
            new GetESignDocumentsQuery(
                fixture.AdminUserId,
                AdminScope: true,
                Q: "freelancer@gigbridge.test"),
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item =>
        {
            Assert.Null(item.CurrentUserSignerRole);
            Assert.False(item.CanCurrentUserSign);
        });
    }

    [Fact]
    public async Task Download_ReturnsPrivateArtifactOnlyToAuthorizedParticipant()
    {
        var fixture = new Fixture();
        var handler = new DownloadESignDocumentQueryHandler(fixture.Context);

        var result = await handler.Handle(
            new DownloadESignDocumentQuery(fixture.FinalizedDocumentId, fixture.FreelancerUserId),
            CancellationToken.None);

        Assert.Equal(fixture.PdfContent, result.Content);
        Assert.Equal("Gigbridge-Client-Freelancer-Contract.pdf", result.FileName);
        Assert.Equal("application/pdf", result.ContentType);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(
            new DownloadESignDocumentQuery(fixture.FinalizedDocumentId, fixture.OutsiderUserId),
            CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new DownloadESignDocumentQuery(fixture.PendingDocumentId, fixture.ClientUserId),
            CancellationToken.None));
    }

    [Fact]
    public async Task Download_PrefersPdfArtifactWithoutReadingUnrelatedDocxArtifact()
    {
        var fixture = new Fixture();
        var artifactPdf = "%PDF-1.7 artifact-table"u8.ToArray();
        fixture.Context.AddSet(
            new EsignDocumentArtifact
            {
                EsignDocumentArtifactId = Guid.NewGuid(),
                EsignDocumentsId = fixture.FinalizedDocumentId,
                ArtifactType = (int)ESignArtifactType.Pdf,
                Content = artifactPdf,
                FileName = "artifact.pdf",
                MimeType = "application/pdf",
                SizeBytes = artifactPdf.Length,
                ContentHashSha256 = new string('a', 64),
                ArtifactRevision = 3,
                CreatedAt = fixture.Now
            },
            new EsignDocumentArtifact
            {
                EsignDocumentArtifactId = Guid.NewGuid(),
                EsignDocumentsId = fixture.FinalizedDocumentId,
                ArtifactType = (int)ESignArtifactType.FinalizedDocx,
                Content = new byte[1024 * 1024],
                FileName = "unrelated.docx",
                MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                SizeBytes = 1024 * 1024,
                ContentHashSha256 = new string('b', 64),
                ArtifactRevision = 3,
                CreatedAt = fixture.Now
            });
        var handler = new DownloadESignDocumentQueryHandler(fixture.Context);

        var result = await handler.Handle(
            new DownloadESignDocumentQuery(fixture.FinalizedDocumentId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal(artifactPdf, result.Content);
        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public async Task ContractStatus_IsSmallAndHidesCounterpartIdentityAndSignatureImage()
    {
        var fixture = new Fixture();
        var pending = fixture.Documents.Entities.Single(item => item.EsignDocumentsId == fixture.PendingDocumentId);
        pending.ContentRevision = 7;
        fixture.Signatures.AddRange(
            new EsignSignature
            {
                EsignSignaturesId = Guid.NewGuid(),
                EsignDocumentsId = fixture.PendingDocumentId,
                UserId = fixture.ClientUserId,
                SignerRole = (int)ESignerRole.Client,
                Status = (int)ESignSignatureStatus.Pending,
                SignatureImageUrl = "data:image/png;base64,current-user",
                SignatureWidth = 200,
                SignatureHeight = 70,
                IdentityOrTaxCode = "123456789",
                DraftSubmittedAt = fixture.Now,
                PolicyAcceptedAt = fixture.Now,
                PolicyVersion = ContractEsignRenderer.PolicyVersion,
                IpAddress = "10.0.0.1",
                UserAgent = "secret-current-agent",
                CreatedAt = fixture.Now
            },
            new EsignSignature
            {
                EsignSignaturesId = Guid.NewGuid(),
                EsignDocumentsId = fixture.PendingDocumentId,
                UserId = fixture.FreelancerUserId,
                SignerRole = (int)ESignerRole.Freelancer,
                Status = (int)ESignSignatureStatus.Pending,
                SignatureImageUrl = "data:image/png;base64,counterpart-secret",
                SignatureWidth = 210,
                SignatureHeight = 75,
                IdentityOrTaxCode = "987654321",
                DraftSubmittedAt = fixture.Now,
                PolicyAcceptedAt = fixture.Now,
                PolicyVersion = ContractEsignRenderer.PolicyVersion,
                IpAddress = "10.0.0.2",
                UserAgent = "secret-counterpart-agent",
                CreatedAt = fixture.Now.AddSeconds(1)
            });
        var handler = new GetESignDocumentStatusByContractQueryHandler(fixture.Context);

        var result = await handler.Handle(
            new GetESignDocumentStatusByContractQuery(fixture.ContractId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal(7, result.Revision);
        Assert.Equal(2, result.SignatureCount);
        var own = Assert.Single(result.Signatures, item => item.UserId == fixture.ClientUserId);
        Assert.Equal("123456789", own.IdentityOrTaxCode);
        Assert.NotNull(own.SignatureImageUrl);
        var counterpart = Assert.Single(result.Signatures, item => item.UserId == fixture.FreelancerUserId);
        Assert.Null(counterpart.IdentityOrTaxCode);
        Assert.Null(counterpart.SignatureImageUrl);
        Assert.Null(counterpart.SignatureWidth);
        Assert.Null(counterpart.SignatureHeight);

        var json = JsonSerializer.Serialize(result);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(json) < 16 * 1024);
        Assert.DoesNotContain("RenderedHtmlContent", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ContractSnapshotJson", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PdfDocumentContent", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-counterpart", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-current-agent", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-counterpart-agent", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ContractStatusSql_DoesNotSelectContentOrArtifactTables()
    {
        var options = new DbContextOptionsBuilder<GigbridgeDbContext>()
            .UseNpgsql("Host=localhost;Database=query_shape;Username=query_shape;Password=query_shape")
            .Options;
        using var context = new GigbridgeDbContext(options);

        var sql = GetESignDocumentStatusByContractQueryHandler
            .StatusDocumentQuery(context, Guid.NewGuid())
            .ToQueryString();

        Assert.Contains("ESign.Status.Document", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ESignDocumentContents", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ESignDocumentArtifacts", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("RenderedHtmlContent", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ContractSnapshotJson", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("PdfDocumentContent", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("FinalizedDocumentContent", sql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detail_ReportsSignerAndFinalArtifactAvailability()
    {
        var fixture = new Fixture();
        var handler = new GetESignDocumentQueryHandler(fixture.Context);

        var result = await handler.Handle(
            new GetESignDocumentQuery(fixture.FinalizedDocumentId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal((int)ESignerRole.Client, result.CurrentUserSignerRole);
        Assert.False(result.CanCurrentUserSign);
        Assert.True(result.HasFinalArtifact);
        Assert.Equal("GB-CONTRACT-FINAL.docx", result.FinalizedDocumentFileName);
        Assert.True(result.HasPdfArtifact);
    }

    [Fact]
    public async Task SavePdf_RejectsBrowserGeneratedPdfForContractDocuments()
    {
        var fixture = new Fixture();
        var handler = new SaveESignPdfCommandHandler(fixture.Context);

        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(
            new SaveESignPdfCommand(
                fixture.FinalizedDocumentId,
                fixture.ClientUserId,
                fixture.PdfContent,
                "stale.pdf",
                1),
            CancellationToken.None));

    }

    [Fact]
    public async Task AdminDelete_AllowsOnlyUnsignedDraft()
    {
        var fixture = new Fixture();
        var handler = new DeleteDraftESignDocumentCommandHandler(fixture.Context);
        var draft = fixture.AddDraftDocument();

        Assert.True(await handler.Handle(
            new DeleteDraftESignDocumentCommand(draft.EsignDocumentsId, fixture.AdminUserId),
            CancellationToken.None));
        Assert.DoesNotContain(fixture.Documents.Entities, item => item.EsignDocumentsId == draft.EsignDocumentsId);

        var signedDraft = fixture.AddDraftDocument();
        fixture.Signatures.Add(new EsignSignature
        {
            EsignSignaturesId = Guid.NewGuid(),
            EsignDocumentsId = signedDraft.EsignDocumentsId,
            UserId = fixture.ClientUserId,
            SignerRole = (int)ESignerRole.Client,
            SignatureImageUrl = "https://cdn.test/draft.png",
            Status = (int)ESignSignatureStatus.Pending,
            CreatedAt = fixture.Now
        });

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new DeleteDraftESignDocumentCommand(signedDraft.EsignDocumentsId, fixture.AdminUserId),
            CancellationToken.None));
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Context.AddSet(
                new User
                {
                    UserId = ClientUserId,
                    FullName = "GigBridge Client",
                    Email = "client@gigbridge.test",
                    Role = (int)UserRole.Client
                },
                new User
                {
                    UserId = FreelancerUserId,
                    FullName = "GigBridge Freelancer",
                    Email = "freelancer@gigbridge.test",
                    Role = (int)UserRole.Freelancer
                },
                new User
                {
                    UserId = AdminUserId,
                    FullName = "GigBridge Admin",
                    Email = "admin@gigbridge.test",
                    Role = (int)UserRole.Admin
                },
                new User
                {
                    UserId = OutsiderUserId,
                    FullName = "Other Client",
                    Email = "other@gigbridge.test",
                    Role = (int)UserRole.Client
                });

            Context.AddSet(
                new ClientProfile
                {
                    ClientProfilesId = ClientProfileId,
                    UserId = ClientUserId
                },
                new ClientProfile
                {
                    ClientProfilesId = OutsiderProfileId,
                    UserId = OutsiderUserId
                });
            Context.AddSet(new FreelancerProfile
            {
                FreelancerProfilesId = FreelancerProfileId,
                UserId = FreelancerUserId
            });
            Context.AddSet(new Contract
            {
                ContractsId = ContractId,
                JobPostsId = JobPostId,
                ClientProfilesId = ClientProfileId,
                FreelancerProfilesId = FreelancerProfileId,
                Title = "GigBridge Graduation Contract",
                TotalBudget = 1_000_000m,
                Status = (int)ContractStatus.PendingSignature,
                CreatedAt = Now.AddDays(-2)
            });

            Documents = Context.AddSet(
                new EsignDocument
                {
                    EsignDocumentsId = PendingDocumentId,
                    EsignTemplatesId = TemplateId,
                    JobPostsId = JobPostId,
                    ContractsId = ContractId,
                    DocumentCode = "GB-CONTRACT-PENDING",
                    Status = (int)ESignDocumentStatus.PendingSignatures,
                    DocumentHash = "pending-hash",
                    CreatedAt = Now
                },
                new EsignDocument
                {
                    EsignDocumentsId = FinalizedDocumentId,
                    EsignTemplatesId = TemplateId,
                    JobPostsId = JobPostId,
                    ContractsId = ContractId,
                    DocumentCode = "GB-CONTRACT-FINAL",
                    Status = (int)ESignDocumentStatus.FullySigned,
                    DocumentHash = "final-hash",
                    FinalizedAt = Now.AddDays(-1),
                    FinalizedDocumentFileName = "GB-CONTRACT-FINAL.docx",
                    FinalizedDocumentSizeBytes = FinalizedContent.Length,
                    PdfDocumentSizeBytes = PdfContent.Length,
                    PdfDocumentHash = "final-hash:contract-template-pdf-v5",
                    PdfSignatureCount = 2,
                    CreatedAt = Now.AddDays(-1)
                });

            Context.AddSet(
                new EsignDocumentContent
                {
                    EsignDocumentsId = PendingDocumentId,
                    RenderedHtmlContent = "<h1>Pending</h1>"
                },
                new EsignDocumentContent
                {
                    EsignDocumentsId = FinalizedDocumentId,
                    RenderedHtmlContent = "<h1>Final</h1>",
                    FinalizedDocumentContent = FinalizedContent,
                    FinalizedDocumentMimeType = DocxContentType,
                    PdfDocumentContent = PdfContent,
                    PdfDocumentFileName = "GB-CONTRACT-FINAL.pdf"
                });

            Signatures = Context.AddSet(
                new EsignSignature
                {
                    EsignSignaturesId = Guid.NewGuid(),
                    EsignDocumentsId = FinalizedDocumentId,
                    UserId = ClientUserId,
                    SignerRole = (int)ESignerRole.Client,
                    SignatureImageUrl = "https://cdn.test/client.png",
                    Status = (int)ESignSignatureStatus.Signed,
                    SignedAt = Now.AddDays(-1),
                    CreatedAt = Now.AddDays(-1)
                },
                new EsignSignature
                {
                    EsignSignaturesId = Guid.NewGuid(),
                    EsignDocumentsId = FinalizedDocumentId,
                    UserId = FreelancerUserId,
                    SignerRole = (int)ESignerRole.Freelancer,
                    SignatureImageUrl = "https://cdn.test/freelancer.png",
                    Status = (int)ESignSignatureStatus.Signed,
                    SignedAt = Now.AddDays(-1),
                    CreatedAt = Now.AddDays(-1)
                });
        }

        private const string DocxContentType =
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        public InMemoryApplicationDbContext Context { get; } = new();
        public TestDbSet<EsignDocument> Documents { get; }
        public TestDbSet<EsignSignature> Signatures { get; }
        public DateTime Now { get; } = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);
        public byte[] FinalizedContent { get; } = [1, 2, 3, 4];
        public byte[] PdfContent { get; } = "%PDF-1.7 test"u8.ToArray();
        public Guid ClientUserId { get; } = Guid.NewGuid();
        public Guid FreelancerUserId { get; } = Guid.NewGuid();
        public Guid AdminUserId { get; } = Guid.NewGuid();
        public Guid OutsiderUserId { get; } = Guid.NewGuid();
        public Guid ClientProfileId { get; } = Guid.NewGuid();
        public Guid OutsiderProfileId { get; } = Guid.NewGuid();
        public Guid FreelancerProfileId { get; } = Guid.NewGuid();
        public Guid ContractId { get; } = Guid.NewGuid();
        public Guid JobPostId { get; } = Guid.NewGuid();
        public Guid TemplateId { get; } = Guid.NewGuid();
        public Guid PendingDocumentId { get; } = Guid.NewGuid();
        public Guid FinalizedDocumentId { get; } = Guid.NewGuid();

        public EsignDocument AddDraftDocument()
        {
            var document = new EsignDocument
            {
                EsignDocumentsId = Guid.NewGuid(),
                EsignTemplatesId = TemplateId,
                JobPostsId = JobPostId,
                ContractsId = ContractId,
                DocumentCode = $"GB-DRAFT-{Guid.NewGuid():N}",
                Status = (int)ESignDocumentStatus.Draft,
                CreatedAt = Now
            };
            Documents.Add(document);
            Context.Set<EsignDocumentContent>().Add(new EsignDocumentContent
            {
                EsignDocumentsId = document.EsignDocumentsId,
                RenderedHtmlContent = "<h1>Draft</h1>"
            });
            return document;
        }
    }
}
