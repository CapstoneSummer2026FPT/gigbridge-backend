using Application.Common.Exceptions;
using Application.Common.Interfaces.Time;
using Application.Features.ESign.Client.CreateDocumentFromJobPost.Commands;
using Application.Features.ESign.Client.GetDocumentByJobPost.Queries;
using Application.Features.ESign.Client.SubmitSignature.Commands;
using Application.Features.ESign.Client.SubmitSignature.DTOs;
using Application.Features.ESign.Common.GetMySignedDocuments.Queries;
using Domain.Entities;
using Domain.Enums.Contracts;
using Domain.Enums.ESign;
using Test_Gigbridge_Backend.TestSupport;

namespace Test_Gigbridge_Backend.Application.Features.ESign;

public class ESignJobPostWorkflowTests
{
    private const string SignatureDataUri = "data:image/png;base64,aGVsbG8=";

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
    public async Task SubmitSignature_SignsLegacyJobPostDocumentWithoutCreatingContract()
    {
        var fixture = new ESignJobPostFixture();
        var document = fixture.AddPendingDocument();
        var handler = new SubmitESignSignatureCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            fixture.MediaService);

        var result = await handler.Handle(
            new SubmitESignSignatureCommand(
                fixture.ClientUserId,
                new SubmitESignSignatureRequest(
                    document.EsignDocumentsId,
                    SignatureDataUri,
                    360,
                    120),
                "127.0.0.1",
                "unit-test"),
            CancellationToken.None);

        var signature = Assert.Single(fixture.Signatures.Entities);
        Assert.Equal(signature.EsignSignaturesId, result.SignatureId);
        Assert.Equal((int)ESignerRole.Client, signature.SignerRole);
        Assert.Equal((int)ESignSignatureStatus.Signed, signature.Status);
        Assert.Equal(fixture.CloudinarySignatureUrl, signature.SignatureImageUrl);
        var upload = Assert.Single(fixture.MediaService.Uploads);
        Assert.Equal("image/png", upload.ContentType);
        Assert.Equal("esign/signatures", upload.Folder);
        Assert.Equal(Convert.FromBase64String("aGVsbG8="), upload.Bytes);
        Assert.Equal(fixture.Now, signature.SignedAt);
        Assert.Equal("127.0.0.1", signature.IpAddress);
        Assert.Equal("unit-test", signature.UserAgent);
        Assert.Equal((int)ESignDocumentStatus.FullySigned, document.Status);
        Assert.Equal(fixture.Now, document.FinalizedAt);
        Assert.Equal(0, fixture.JobPost.Status);

        Assert.DoesNotContain(fixture.Context.Set<Contract>(), c => c.JobPostsId == fixture.JobPostId);
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
            new FixedDateTimeService(fixture.Now),
            fixture.MediaService);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new SubmitESignSignatureCommand(
                    fixture.ClientUserId,
                    new SubmitESignSignatureRequest(
                        document.EsignDocumentsId,
                        SignatureDataUri,
                        null,
                        null),
                    null,
                    null),
                CancellationToken.None));
    }

    [Fact]
    public async Task SubmitSignature_RejectsInvalidSignatureDataUri()
    {
        var fixture = new ESignJobPostFixture();
        var document = fixture.AddPendingDocument();
        var handler = new SubmitESignSignatureCommandHandler(
            fixture.Context,
            new FixedDateTimeService(fixture.Now),
            fixture.MediaService);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(
                new SubmitESignSignatureCommand(
                    fixture.ClientUserId,
                    new SubmitESignSignatureRequest(
                        document.EsignDocumentsId,
                        "https://sig/client.png",
                        null,
                        null),
                    null,
                    null),
                CancellationToken.None));

        Assert.Empty(fixture.MediaService.Uploads);
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

    [Fact]
    public async Task GetMySignedDocuments_ClientReceivesSignedJobAndPartialContractDocuments()
    {
        var fixture = new ESignSignedDocumentsFixture();
        var handler = new GetMySignedESignDocumentsQueryHandler(fixture.Context);

        var result = await handler.Handle(
            new GetMySignedESignDocumentsQuery(fixture.ClientUserId),
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(
            new[] { fixture.JobDocumentId, fixture.ClientPartialContractDocumentId },
            result.Items.Select(item => item.DocumentId).ToArray());
        Assert.Contains(result.Items, item =>
            item.DocumentId == fixture.ClientPartialContractDocumentId &&
            item.DocumentType == "Contract" &&
            item.DocumentStatus == (int)ESignDocumentStatus.PartiallySigned &&
            item.HasClientSigned &&
            !item.HasFreelancerSigned);
    }

    [Fact]
    public async Task GetMySignedDocuments_FreelancerReceivesOnlySignedContractDocuments()
    {
        var fixture = new ESignSignedDocumentsFixture();
        var handler = new GetMySignedESignDocumentsQueryHandler(fixture.Context);

        var result = await handler.Handle(
            new GetMySignedESignDocumentsQuery(fixture.FreelancerUserId),
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(fixture.FreelancerContractDocumentId, item.DocumentId);
        Assert.Equal("Contract", item.DocumentType);
        Assert.Equal((int)ESignerRole.Freelancer, item.CurrentUserSignerRole);
        Assert.DoesNotContain(result.Items, document => document.DocumentId == fixture.FreelancerJobDocumentId);
    }

    [Fact]
    public async Task GetMySignedDocuments_ExcludesDocumentsWithoutSignedCurrentUserSignature()
    {
        var fixture = new ESignSignedDocumentsFixture();
        var handler = new GetMySignedESignDocumentsQueryHandler(fixture.Context);

        var result = await handler.Handle(
            new GetMySignedESignDocumentsQuery(fixture.OtherClientUserId),
            CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetMySignedDocuments_AppliesFiltersSearchAndPagination()
    {
        var fixture = new ESignSignedDocumentsFixture();
        var handler = new GetMySignedESignDocumentsQueryHandler(fixture.Context);

        var statusResult = await handler.Handle(
            new GetMySignedESignDocumentsQuery(
                fixture.ClientUserId,
                Status: (int)ESignDocumentStatus.PartiallySigned),
            CancellationToken.None);
        var statusItem = Assert.Single(statusResult.Items);
        Assert.Equal(fixture.ClientPartialContractDocumentId, statusItem.DocumentId);

        var documentTypeResult = await handler.Handle(
            new GetMySignedESignDocumentsQuery(fixture.ClientUserId, DocumentType: "job"),
            CancellationToken.None);
        var documentTypeItem = Assert.Single(documentTypeResult.Items);
        Assert.Equal(fixture.JobDocumentId, documentTypeItem.DocumentId);

        var searchResult = await handler.Handle(
            new GetMySignedESignDocumentsQuery(fixture.ClientUserId, Q: "mobile"),
            CancellationToken.None);
        var searchItem = Assert.Single(searchResult.Items);
        Assert.Equal(fixture.ClientPartialContractDocumentId, searchItem.DocumentId);

        var pagedResult = await handler.Handle(
            new GetMySignedESignDocumentsQuery(fixture.ClientUserId, Page: 2, PageSize: 1),
            CancellationToken.None);
        var pagedItem = Assert.Single(pagedResult.Items);
        Assert.Equal(2, pagedResult.TotalCount);
        Assert.Equal(fixture.ClientPartialContractDocumentId, pagedItem.DocumentId);
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

        public string CloudinarySignatureUrl { get; } = "https://res.cloudinary.com/gigbridge/esign/signatures/client.png";

        public FakeMediaService MediaService { get; } = new("https://res.cloudinary.com/gigbridge/esign/signatures/client.png");

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

    private sealed class ESignSignedDocumentsFixture
    {
        public ESignSignedDocumentsFixture()
        {
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

            Context.AddSet(new FreelancerProfile
            {
                FreelancerProfilesId = FreelancerProfileId,
                UserId = FreelancerUserId
            });

            Context.AddSet(
                new JobPost
                {
                    JobPostsId = JobPostId,
                    ClientProfilesId = ClientProfileId,
                    Title = "Landing Page",
                    Description = "Build a landing page",
                    Status = 0,
                    CreatedAt = Now.AddDays(-5)
                },
                new JobPost
                {
                    JobPostsId = ClientPartialContractJobPostId,
                    ClientProfilesId = ClientProfileId,
                    Title = "Mobile App Job",
                    Description = "Build mobile app",
                    Status = 1,
                    CreatedAt = Now.AddDays(-4)
                },
                new JobPost
                {
                    JobPostsId = FreelancerContractJobPostId,
                    ClientProfilesId = ClientProfileId,
                    Title = "API Integration Job",
                    Description = "Build API integration",
                    Status = 1,
                    CreatedAt = Now.AddDays(-3)
                },
                new JobPost
                {
                    JobPostsId = UnsignedJobPostId,
                    ClientProfilesId = OtherClientProfileId,
                    Title = "Unsigned Job",
                    Description = "Pending signature",
                    Status = 0,
                    CreatedAt = Now.AddDays(-2)
                });

            Context.AddSet(
                new Contract
                {
                    ContractsId = ClientPartialContractId,
                    JobPostsId = ClientPartialContractJobPostId,
                    ClientProfilesId = ClientProfileId,
                    FreelancerProfilesId = FreelancerProfileId,
                    Title = "Mobile App Contract",
                    TotalBudget = 1200m,
                    Status = (int)ContractStatus.PendingSignature,
                    CreatedAt = Now.AddDays(-4)
                },
                new Contract
                {
                    ContractsId = FreelancerContractId,
                    JobPostsId = FreelancerContractJobPostId,
                    ClientProfilesId = ClientProfileId,
                    FreelancerProfilesId = FreelancerProfileId,
                    Title = "API Integration Contract",
                    TotalBudget = 900m,
                    Status = (int)ContractStatus.PendingSignature,
                    CreatedAt = Now.AddDays(-3)
                });

            Context.AddSet(
                new EsignDocument
                {
                    EsignDocumentsId = JobDocumentId,
                    EsignTemplatesId = TemplateId,
                    JobPostsId = JobPostId,
                    DocumentCode = "GB-JOB-SIGNED",
                    RenderedHtmlContent = "<h1>Landing Page</h1>",
                    Status = (int)ESignDocumentStatus.FullySigned,
                    FinalizedAt = Now.AddMinutes(-50),
                    CreatedAt = Now.AddDays(-5)
                },
                new EsignDocument
                {
                    EsignDocumentsId = ClientPartialContractDocumentId,
                    EsignTemplatesId = TemplateId,
                    JobPostsId = ClientPartialContractJobPostId,
                    ContractsId = ClientPartialContractId,
                    DocumentCode = "GB-CONTRACT-PARTIAL",
                    RenderedHtmlContent = "<h1>Mobile App Contract</h1>",
                    Status = (int)ESignDocumentStatus.PartiallySigned,
                    CreatedAt = Now.AddDays(-4)
                },
                new EsignDocument
                {
                    EsignDocumentsId = FreelancerContractDocumentId,
                    EsignTemplatesId = TemplateId,
                    JobPostsId = FreelancerContractJobPostId,
                    ContractsId = FreelancerContractId,
                    DocumentCode = "GB-CONTRACT-FREELANCER",
                    RenderedHtmlContent = "<h1>API Integration Contract</h1>",
                    Status = (int)ESignDocumentStatus.PartiallySigned,
                    CreatedAt = Now.AddDays(-3)
                },
                new EsignDocument
                {
                    EsignDocumentsId = UnsignedDocumentId,
                    EsignTemplatesId = TemplateId,
                    JobPostsId = UnsignedJobPostId,
                    DocumentCode = "GB-JOB-PENDING",
                    RenderedHtmlContent = "<h1>Unsigned Job</h1>",
                    Status = (int)ESignDocumentStatus.PendingSignatures,
                    CreatedAt = Now.AddDays(-2)
                },
                new EsignDocument
                {
                    EsignDocumentsId = FreelancerJobDocumentId,
                    EsignTemplatesId = TemplateId,
                    JobPostsId = JobPostId,
                    DocumentCode = "GB-JOB-FREELANCER-SHOULD-HIDE",
                    RenderedHtmlContent = "<h1>Wrong job signer</h1>",
                    Status = (int)ESignDocumentStatus.FullySigned,
                    CreatedAt = Now.AddDays(-1)
                });

            Context.AddSet(
                new EsignSignature
                {
                    EsignSignaturesId = Guid.NewGuid(),
                    EsignDocumentsId = JobDocumentId,
                    UserId = ClientUserId,
                    SignerRole = (int)ESignerRole.Client,
                    SignatureImageUrl = "https://cdn.test/job-client.png",
                    Status = (int)ESignSignatureStatus.Signed,
                    SignedAt = Now.AddMinutes(-10),
                    CreatedAt = Now.AddMinutes(-10)
                },
                new EsignSignature
                {
                    EsignSignaturesId = Guid.NewGuid(),
                    EsignDocumentsId = ClientPartialContractDocumentId,
                    UserId = ClientUserId,
                    SignerRole = (int)ESignerRole.Client,
                    SignatureImageUrl = "https://cdn.test/contract-client.png",
                    Status = (int)ESignSignatureStatus.Signed,
                    SignedAt = Now.AddMinutes(-20),
                    CreatedAt = Now.AddMinutes(-20)
                },
                new EsignSignature
                {
                    EsignSignaturesId = Guid.NewGuid(),
                    EsignDocumentsId = FreelancerContractDocumentId,
                    UserId = FreelancerUserId,
                    SignerRole = (int)ESignerRole.Freelancer,
                    SignatureImageUrl = "https://cdn.test/contract-freelancer.png",
                    Status = (int)ESignSignatureStatus.Signed,
                    SignedAt = Now.AddMinutes(-30),
                    CreatedAt = Now.AddMinutes(-30)
                },
                new EsignSignature
                {
                    EsignSignaturesId = Guid.NewGuid(),
                    EsignDocumentsId = UnsignedDocumentId,
                    UserId = OtherClientUserId,
                    SignerRole = (int)ESignerRole.Client,
                    SignatureImageUrl = "https://cdn.test/pending-client.png",
                    Status = (int)ESignSignatureStatus.Pending,
                    CreatedAt = Now.AddMinutes(-40)
                },
                new EsignSignature
                {
                    EsignSignaturesId = Guid.NewGuid(),
                    EsignDocumentsId = FreelancerJobDocumentId,
                    UserId = FreelancerUserId,
                    SignerRole = (int)ESignerRole.Freelancer,
                    SignatureImageUrl = "https://cdn.test/wrong-job-freelancer.png",
                    Status = (int)ESignSignatureStatus.Signed,
                    SignedAt = Now.AddMinutes(-5),
                    CreatedAt = Now.AddMinutes(-5)
                });
        }

        public InMemoryApplicationDbContext Context { get; } = new();

        public DateTime Now { get; } = new(2026, 6, 24, 9, 0, 0, DateTimeKind.Utc);

        public Guid ClientUserId { get; } = Guid.NewGuid();

        public Guid OtherClientUserId { get; } = Guid.NewGuid();

        public Guid FreelancerUserId { get; } = Guid.NewGuid();

        public Guid ClientProfileId { get; } = Guid.NewGuid();

        public Guid OtherClientProfileId { get; } = Guid.NewGuid();

        public Guid FreelancerProfileId { get; } = Guid.NewGuid();

        public Guid TemplateId { get; } = Guid.NewGuid();

        public Guid JobPostId { get; } = Guid.NewGuid();

        public Guid ClientPartialContractJobPostId { get; } = Guid.NewGuid();

        public Guid FreelancerContractJobPostId { get; } = Guid.NewGuid();

        public Guid UnsignedJobPostId { get; } = Guid.NewGuid();

        public Guid ClientPartialContractId { get; } = Guid.NewGuid();

        public Guid FreelancerContractId { get; } = Guid.NewGuid();

        public Guid JobDocumentId { get; } = Guid.NewGuid();

        public Guid ClientPartialContractDocumentId { get; } = Guid.NewGuid();

        public Guid FreelancerContractDocumentId { get; } = Guid.NewGuid();

        public Guid UnsignedDocumentId { get; } = Guid.NewGuid();

        public Guid FreelancerJobDocumentId { get; } = Guid.NewGuid();
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
