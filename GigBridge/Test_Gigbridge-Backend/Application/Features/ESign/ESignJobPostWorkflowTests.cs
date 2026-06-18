using Application.Common.Exceptions;
using Application.Common.Interfaces.IService;
using Application.Features.ESign.Client.CreateDocumentFromJobPost.Commands;
using Application.Features.ESign.Client.GetDocumentByJobPost.Queries;
using Application.Features.ESign.Client.SubmitSignature.Commands;
using Application.Features.ESign.Client.SubmitSignature.DTOs;
using Domain.Entities;
using Domain.Enums;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.ESign;

public class ESignJobPostWorkflowTests
{
    [Fact]
    public async Task CreateFromJob_CreatesPendingDocumentForOwningClient()
    {
        var fixture = new ESignJobPostFixture();
        var handler = new CreateESignDocumentFromJobPostCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now));

        var result = await handler.Handle(
            new CreateESignDocumentFromJobPostCommand(fixture.JobPostId, fixture.ClientUserId),
            CancellationToken.None);

        var document = Assert.Single(fixture.Documents.Entities);
        Assert.Equal(document.EsignDocumentsId, result.DocumentId);
        Assert.Equal(fixture.JobPostId, result.JobPostId);
        Assert.Equal(fixture.TemplateId, result.TemplateId);
        Assert.Equal((int)ESignDocumentStatus.PendingSignatures, document.Status);
        Assert.Contains("Build landing page", document.RenderedHtmlContent);
        Assert.Contains("Create a responsive landing page", document.RenderedHtmlContent);
        Assert.False(string.IsNullOrWhiteSpace(document.DocumentHash));
        Assert.Equal(fixture.Now, document.CreatedAt);
    }

    [Fact]
    public async Task CreateFromJob_ReusesExistingDocumentForJob()
    {
        var fixture = new ESignJobPostFixture();
        var existing = fixture.AddPendingDocument();
        var handler = new CreateESignDocumentFromJobPostCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now));

        var result = await handler.Handle(
            new CreateESignDocumentFromJobPostCommand(fixture.JobPostId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Single(fixture.Documents.Entities);
        Assert.Equal(existing.EsignDocumentsId, result.DocumentId);
        Assert.Equal(existing.DocumentCode, result.DocumentCode);
    }

    [Fact]
    public async Task CreateFromJob_RejectsNonOwnerClient()
    {
        var fixture = new ESignJobPostFixture();
        var handler = new CreateESignDocumentFromJobPostCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now));

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            handler.Handle(
                new CreateESignDocumentFromJobPostCommand(fixture.JobPostId, fixture.OtherClientUserId),
                CancellationToken.None));
    }

    [Fact]
    public async Task SubmitSignature_SignsDocumentAndOpensJobPost()
    {
        var fixture = new ESignJobPostFixture();
        var document = fixture.AddPendingDocument();
        var handler = new SubmitESignSignatureCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now));

        var result = await handler.Handle(
            new SubmitESignSignatureCommand(
                fixture.ClientUserId,
                new SubmitESignSignatureRequest(
                    document.EsignDocumentsId,
                    "data:image/png;base64,aGVsbG8=",
                    360,
                    120),
                "127.0.0.1",
                "unit-test"),
            CancellationToken.None);

        var signature = Assert.Single(fixture.Signatures.Entities);
        Assert.Equal(signature.EsignSignaturesId, result.SignatureId);
        Assert.Equal((int)ESignerRole.Client, signature.SignerRole);
        Assert.Equal((int)ESignSignatureStatus.Signed, signature.Status);
        Assert.Equal("data:image/png;base64,aGVsbG8=", signature.SignatureImageUrl);
        Assert.Equal(fixture.Now, signature.SignedAt);
        Assert.Equal("127.0.0.1", signature.IpAddress);
        Assert.Equal("unit-test", signature.UserAgent);
        Assert.Equal((int)ESignDocumentStatus.FullySigned, document.Status);
        Assert.Equal(fixture.Now, document.FinalizedAt);
        Assert.Equal(1, fixture.JobPost.Status);
        Assert.Equal(fixture.Now, fixture.JobPost.UpdatedAt);
    }

    [Fact]
    public async Task SubmitSignature_RejectsDuplicateSignedSignature()
    {
        var fixture = new ESignJobPostFixture();
        var document = fixture.AddPendingDocument();
        fixture.Signatures.Add(new EsignSignature
        {
            EsignSignaturesId = Guid.NewGuid(),
            EsignDocumentsId = document.EsignDocumentsId,
            UserId = fixture.ClientUserId,
            SignerRole = (int)ESignerRole.Client,
            SignatureImageUrl = "data:image/png;base64,old",
            Status = (int)ESignSignatureStatus.Signed,
            SignedAt = fixture.Now.AddMinutes(-5),
            CreatedAt = fixture.Now.AddMinutes(-5)
        });

        var handler = new SubmitESignSignatureCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now));

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new SubmitESignSignatureCommand(
                    fixture.ClientUserId,
                    new SubmitESignSignatureRequest(
                        document.EsignDocumentsId,
                        "data:image/png;base64,new",
                        null,
                        null),
                    null,
                    null),
                CancellationToken.None));
    }

    [Fact]
    public async Task GetByJob_ReturnsDocumentWithSignaturesForOwningClient()
    {
        var fixture = new ESignJobPostFixture();
        var document = fixture.AddPendingDocument();
        fixture.Signatures.Add(new EsignSignature
        {
            EsignSignaturesId = Guid.NewGuid(),
            EsignDocumentsId = document.EsignDocumentsId,
            UserId = fixture.ClientUserId,
            SignerRole = (int)ESignerRole.Client,
            SignatureImageUrl = "data:image/png;base64,aGVsbG8=",
            Status = (int)ESignSignatureStatus.Signed,
            SignedAt = fixture.Now,
            CreatedAt = fixture.Now
        });

        var handler = new GetESignDocumentByJobPostQueryHandler(fixture.Context);

        var result = await handler.Handle(
            new GetESignDocumentByJobPostQuery(fixture.JobPostId, fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal(document.EsignDocumentsId, result.DocumentId);
        Assert.Single(result.Signatures);
        Assert.Equal(fixture.ClientUserId, result.Signatures[0].UserId);
    }

    private sealed class ESignJobPostFixture
    {
        public ESignJobPostFixture()
        {
            JobPost = new JobPost
            {
                JobPostsId = JobPostId,
                ClientProfilesId = ClientProfileId,
                Title = "Build landing page",
                Description = "Create a responsive landing page",
                BudgetMin = 500m,
                BudgetMax = 800m,
                Currency = "USD",
                Status = 0,
                Visibility = 0,
                CreatedAt = new DateTime(2026, 6, 18, 8, 0, 0, DateTimeKind.Utc)
            };

            Context.AddSet(
                new ClientProfile
                {
                    ClientProfilesId = ClientProfileId,
                    UserId = ClientUserId
                },
                new ClientProfile
                {
                    ClientProfilesId = OtherClientProfileId,
                    UserId = OtherClientUserId
                });

            JobPosts = Context.AddSet(JobPost);

            Templates = Context.AddSet(new EsignTemplate
            {
                EsignTemplatesId = TemplateId,
                Name = "Job post commitment",
                TemplateCode = "JOB_POST_CLIENT_COMMITMENT",
                HtmlContent = "<h1>{{Job.Title}}</h1><p>{{Job.Description}}</p><p>{{Job.Budget}}</p>",
                Version = 1,
                IsActive = true,
                CreatedAt = Now
            });

            Documents = Context.AddSet<EsignDocument>();
            Signatures = Context.AddSet<EsignSignature>();
        }

        public InMemoryApplicationDbContext Context { get; } = new();

        public DateTime Now { get; } = new(2026, 6, 18, 8, 30, 0, DateTimeKind.Utc);

        public Guid ClientUserId { get; } = Guid.NewGuid();

        public Guid OtherClientUserId { get; } = Guid.NewGuid();

        public Guid ClientProfileId { get; } = Guid.NewGuid();

        public Guid OtherClientProfileId { get; } = Guid.NewGuid();

        public Guid JobPostId { get; } = Guid.NewGuid();

        public Guid TemplateId { get; } = Guid.NewGuid();

        public JobPost JobPost { get; }

        public TestDbSet<JobPost> JobPosts { get; }

        public TestDbSet<EsignTemplate> Templates { get; }

        public TestDbSet<EsignDocument> Documents { get; }

        public TestDbSet<EsignSignature> Signatures { get; }

        public EsignDocument AddPendingDocument()
        {
            var document = new EsignDocument
            {
                EsignDocumentsId = Guid.NewGuid(),
                EsignTemplatesId = TemplateId,
                JobPostsId = JobPostId,
                DocumentCode = "GB-JOB-EXISTING",
                RenderedHtmlContent = "<h1>Existing</h1>",
                Status = (int)ESignDocumentStatus.PendingSignatures,
                DocumentHash = "existing-hash",
                CreatedAt = Now
            };
            Documents.Add(document);
            return document;
        }
    }

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}
