using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public partial class GigbridgeDbContext : DbContext, IApplicationDbContext, IDataProtectionKeyContext
{
    public GigbridgeDbContext(DbContextOptions<GigbridgeDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AdminAuditLog> AdminAuditLogs { get; set; }

    public virtual DbSet<BankAccount> BankAccounts { get; set; }

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<CategorySkill> CategorySkills { get; set; }

    public virtual DbSet<BroadcastNotification> BroadcastNotifications { get; set; }

    public virtual DbSet<BroadcastNotificationRecipient> BroadcastNotificationRecipients { get; set; }

    public virtual DbSet<ClientProfile> ClientProfiles { get; set; }

    public virtual DbSet<Contract> Contracts { get; set; }

    public virtual DbSet<ContractWorkItem> ContractWorkItems { get; set; }

    public virtual DbSet<ContractChangeRequest> ContractChangeRequests { get; set; }

    public virtual DbSet<ContractAmendment> ContractAmendments { get; set; }

    public virtual DbSet<ContractAmendmentMilestone> ContractAmendmentMilestones { get; set; }

    public virtual DbSet<ContractAmendmentWorkItem> ContractAmendmentWorkItems { get; set; }

    public virtual DbSet<ContractAmendmentSignature> ContractAmendmentSignatures { get; set; }

    public virtual DbSet<ContractPlanRevision> ContractPlanRevisions { get; set; }

    public virtual DbSet<ContractEscrow> ContractEscrows { get; set; }

    public virtual DbSet<ContractProductHandoff> ContractProductHandoffs { get; set; }

    public virtual DbSet<Conversation> Conversations { get; set; }

    public virtual DbSet<ConversationParticipant> ConversationParticipants { get; set; }

    public virtual DbSet<Schedule> Schedules { get; set; }

    public virtual DbSet<DeliveryOutbox> DeliveryOutboxes { get; set; }

    public virtual DbSet<PayoutOutbox> PayoutOutboxes { get; set; }

    public virtual DbSet<GoogleMeetConnection> GoogleMeetConnections { get; set; }
    public virtual DbSet<GoogleMeetOAuthState> GoogleMeetOAuthStates { get; set; }
    public virtual DbSet<GoogleMeetProvisioningJob> GoogleMeetProvisioningJobs { get; set; }

    public virtual DbSet<Dispute> Disputes { get; set; }

    public virtual DbSet<DisputeEvidence> DisputeEvidences { get; set; }

    public virtual DbSet<DisputeMilestoneDecision> DisputeMilestoneDecisions { get; set; }

    public virtual DbSet<EsignDocument> EsignDocuments { get; set; }

    public virtual DbSet<EsignSignature> EsignSignatures { get; set; }

    public virtual DbSet<EsignTemplate> EsignTemplates { get; set; }

    public virtual DbSet<EscrowTransaction> EscrowTransactions { get; set; }

    public virtual DbSet<Faq> Faqs { get; set; }

    public virtual DbSet<Faqcategory> Faqcategories { get; set; }

    public virtual DbSet<FreelancerProfile> FreelancerProfiles { get; set; }

    public virtual DbSet<FreelancerProfileCategory> FreelancerProfileCategories { get; set; }

    public virtual DbSet<FreelancerSkill> FreelancerSkills { get; set; }

    public virtual DbSet<JobInvitation> JobInvitations { get; set; }

    public virtual DbSet<JobPost> JobPosts { get; set; }

    public virtual DbSet<JobPostMilestonePlan> JobPostMilestonePlans { get; set; }

    public virtual DbSet<JobPostWorkItem> JobPostWorkItems { get; set; }

    public virtual DbSet<JobPostAttachment> JobPostAttachments { get; set; }

    public virtual DbSet<JobPostQuestion> JobPostQuestions { get; set; }

    public virtual DbSet<JobPostSkill> JobPostSkills { get; set; }

    public virtual DbSet<TalentMatchRun> TalentMatchRuns { get; set; }

    public virtual DbSet<TalentMatchResult> TalentMatchResults { get; set; }

    public virtual DbSet<TalentMatchEvent> TalentMatchEvents { get; set; }

    public virtual DbSet<Major> Majors { get; set; }

    public virtual DbSet<MajorCategory> MajorCategories { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<MessageAttachment> MessageAttachments { get; set; }

    public virtual DbSet<NegotiationOffer> NegotiationOffers { get; set; }

    public virtual DbSet<NegotiationMilestoneDraft> NegotiationMilestoneDrafts { get; set; }

    public virtual DbSet<NegotiationMilestoneDraftWorkItem> NegotiationMilestoneDraftWorkItems { get; set; }

    public virtual DbSet<NegotiationOfferMilestone> NegotiationOfferMilestones { get; set; }

    public virtual DbSet<NegotiationOfferWorkItem> NegotiationOfferWorkItems { get; set; }

    public virtual DbSet<Milestone> Milestones { get; set; }

    public virtual DbSet<MilestoneEarlyStartRequest> MilestoneEarlyStartRequests { get; set; }

    public virtual DbSet<MilestoneAttachment> MilestoneAttachments { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<PlatformSetting> PlatformSettings { get; set; }
    public virtual DbSet<FreelancerRankProtection> FreelancerRankProtections { get; set; }
    public virtual DbSet<FreelancerProfilePromotion> FreelancerProfilePromotions { get; set; }

    public virtual DbSet<PortfolioItem> PortfolioItems { get; set; }

    public virtual DbSet<Proposal> Proposals { get; set; }

    public virtual DbSet<ProposalWorkBreakdownItem> ProposalWorkBreakdownItems { get; set; }

    public virtual DbSet<ProposalMilestonePlan> ProposalMilestonePlans { get; set; }

    public virtual DbSet<ProposalInterviewReviewSession> ProposalInterviewReviewSessions { get; set; }

    public virtual DbSet<ProposalQuestionTimer> ProposalQuestionTimers { get; set; }

    public virtual DbSet<ProposalAnswer> ProposalAnswers { get; set; }

    public virtual DbSet<ProposalAiJudging> ProposalAiJudgings { get; set; }

    public virtual DbSet<Report> Reports { get; set; }

    public virtual DbSet<ReportContract> ReportContracts { get; set; }

    public virtual DbSet<ReportContractAttachment> ReportContractAttachments { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<SavedFreelancer> SavedFreelancers { get; set; }

    public virtual DbSet<SavedJob> SavedJobs { get; set; }

    public virtual DbSet<Skill> Skills { get; set; }

    public virtual DbSet<Subscription> Subscriptions { get; set; }

    public virtual DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }

    public virtual DbSet<UserEloPointTransaction> UserEloPointTransactions { get; set; }

    public virtual DbSet<UserEloScore> UserEloScores { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserWallet> UserWallets { get; set; }

    public virtual DbSet<WalletTransaction> WalletTransactions { get; set; }

    public virtual DbSet<WalletWithdrawal> WalletWithdrawals { get; set; }

    public virtual DbSet<WorkExperience> WorkExperiences { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("citext");

        modelBuilder.Entity<AdminAuditLog>(entity =>
        {
            entity.HasKey(e => e.AdminAuditLogsId).HasName("AdminAuditLogs_pkey");

            entity.HasIndex(e => e.Action, "IX_AdminAuditLogs_Action");

            entity.HasIndex(e => e.AdminId, "IX_AdminAuditLogs_AdminId");

            entity.HasIndex(e => e.CreatedAt, "IX_AdminAuditLogs_CreatedAt");

            entity.HasIndex(e => new { e.EntityId, e.EntityType }, "IX_AdminAuditLogs_EntityId_EntityType");

            entity.Property(e => e.AdminAuditLogsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("AdminAuditLogsId");
            entity.Property(e => e.Action).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.EntityType).HasMaxLength(50);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.NewValues).HasColumnType("jsonb");
            entity.Property(e => e.OldValues).HasColumnType("jsonb");
            entity.Property(e => e.AdminId).HasColumnName("AdminId");

            entity.HasOne(d => d.Admin).WithMany(p => p.AdminAuditLogs)
                .HasForeignKey(d => d.AdminId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("AdminAuditLogs_usr_AdminId_fkey");
        });

        modelBuilder.Entity<BankAccount>(entity =>
        {
            entity.HasKey(e => e.BankAccountId).HasName("BankAccounts_pkey");

            entity.HasIndex(e => e.UserId, "IX_BankAccounts_UserId");
            entity.HasIndex(e => new { e.UserId, e.IsDefault }, "IX_BankAccounts_UserId_IsDefault");
            entity.HasIndex(e => new { e.UserId, e.Status }, "IX_BankAccounts_UserId_Status");

            entity.Property(e => e.BankAccountId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.BankCode).HasMaxLength(30);
            entity.Property(e => e.BankBin).HasMaxLength(6);
            entity.Property(e => e.BankName).HasMaxLength(120);
            entity.Property(e => e.AccountNumberEncrypted).HasColumnType("text");
            entity.Property(e => e.AccountNumberMasked).HasMaxLength(60);
            entity.Property(e => e.AccountName).HasMaxLength(120);
            entity.Property(e => e.Status)
                .HasComment("Enum BankAccountStatus: 0=Active, 1=Disabled");
            entity.Property(e => e.IsDefault).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("BankAccounts_usr_UserId_fkey");
        });


        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoriesId).HasName("Categories_pkey");

            entity.HasIndex(e => e.IsActive, "IX_Categories_IsActive");

            entity.HasIndex(e => e.Slug, "IX_Categories_Slug")
                .IsUnique();

            entity.Property(e => e.CategoriesId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("CategoriesId");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()");

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);

            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.Slug)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.Description);

            entity.Property(e => e.SortOrder)
                .HasDefaultValue(0);

        });
        modelBuilder.Entity<CategorySkill>(entity =>
        {
            entity.HasKey(e => e.CategorySkillsId).HasName("CategorySkills_pkey");

            entity.HasIndex(e => e.CategoryId, "IX_CategorySkills_CategoryId");

            entity.HasIndex(e => e.SkillId, "IX_CategorySkills_SkillId");

            entity.HasIndex(e => new { e.CategoryId, e.SkillId }, "CategorySkills_CategoryId_SkillId_key")
                .IsUnique();

            entity.Property(e => e.CategorySkillsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("CategorySkillsId");

            entity.Property(e => e.CategoryId)
                .HasColumnName("CategoryId");

            entity.Property(e => e.SkillId)
                .HasColumnName("SkillId");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()");

            entity.HasOne(e => e.Category)
                .WithMany(e => e.CategorySkills)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("CategorySkills_cat_CategoryId_fkey");

            entity.HasOne(e => e.Skill)
                .WithMany(e => e.CategorySkills)
                .HasForeignKey(e => e.SkillId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("CategorySkills_sk_SkillId_fkey");
        });

        modelBuilder.Entity<ClientProfile>(entity =>
        {
            entity.HasKey(e => e.ClientProfilesId).HasName("ClientProfiles_pkey");

            entity.HasIndex(e => e.UserId, "ClientProfiles_usr_UserId_key").IsUnique();

            entity.Property(e => e.ClientProfilesId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ClientProfilesId");
            entity.Property(e => e.CompanyName).HasMaxLength(300);
            entity.Property(e => e.CompanySize).HasComment("Enum CompanySize: 0=Solo, 1=Small, 2=Medium, 3=Large");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Industry).HasMaxLength(200);
            entity.Property(e => e.Location).HasMaxLength(300);
            entity.Property(e => e.UserId).HasColumnName("UserId");

            entity.HasOne(d => d.User).WithOne(p => p.ClientProfile)
                .HasForeignKey<ClientProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ClientProfiles_usr_UserId_fkey");
        });

        modelBuilder.Entity<Contract>(entity =>
        {
            entity.HasKey(e => e.ContractsId).HasName("Contracts_pkey");

            entity.HasIndex(e => e.ProposalsId, "Contracts_propo_ProposalsId_key").IsUnique();

            entity.HasIndex(e => e.ClientProfilesId, "IX_Contracts_ClientProfilesId");

            entity.HasIndex(e => new { e.ClientProfilesId, e.Status }, "IX_Contracts_ClientProfilesId_Status");

            entity.HasIndex(e => e.FreelancerProfilesId, "IX_Contracts_FreelancerProfilesId");

            entity.HasIndex(e => new { e.FreelancerProfilesId, e.Status }, "IX_Contracts_FreelancerProfilesId_Status");

            entity.HasIndex(e => e.JobPostsId, "IX_Contracts_JobPostsId").IsUnique();

            entity.HasIndex(e => e.Status, "IX_Contracts_Status");

            entity.Property(e => e.ContractsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ContractsId");
            entity.Property(e => e.ClientProfilesId).HasColumnName("ClientProfilesId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.EsignContractPdfUrl)
                .HasComment("v1.2: URL bản hợp đồng lao động e-sign PDF khi có tranh chấp thanh toán")
                .HasColumnName("ESignContractPdfUrl");
            entity.Property(e => e.FreelancerProfilesId).HasColumnName("FreelancerProfilesId");
            entity.Property(e => e.JobPostsId).HasColumnName("JobPostsId");
            entity.Property(e => e.ProposalsId).HasColumnName("ProposalsId");
            entity.Property(e => e.Status).HasComment("Enum ContractStatus: 0=Draft, 1=PendingFreelancerSelection, 2=InNegotiation, 3=PendingContractDetails, 4=PendingContractConfirmation, 5=PendingEscrow, 6=PendingSignature, 7=Active, 8=Completed, 9=Cancelled, 10=Disputed");
            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.TotalBudget).HasPrecision(18, 2);
            entity.Property(e => e.RevisionNumber).HasDefaultValue(1);

            entity.HasOne(d => d.ClientProfiles).WithMany(p => p.Contracts)
                .HasForeignKey(d => d.ClientProfilesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Contracts_clPro_ClientProfilesId_fkey");

            entity.HasOne(d => d.FreelancerProfiles).WithMany(p => p.Contracts)
                .HasForeignKey(d => d.FreelancerProfilesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Contracts_flPro_FreelancerProfilesId_fkey");

            entity.HasOne(d => d.JobPosts).WithOne(p => p.Contract)
                .HasForeignKey<Contract>(d => d.JobPostsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Contracts_jp_JobPostsId_fkey");

            entity.HasOne(d => d.Proposals).WithOne(p => p.Contract)
                .HasForeignKey<Contract>(d => d.ProposalsId)
                .HasConstraintName("Contracts_propo_ProposalsId_fkey");
        });

        modelBuilder.Entity<ContractWorkItem>(entity =>
        {
            entity.HasKey(e => e.ContractWorkItemId);
            entity.HasIndex(e => new { e.MilestonesId, e.OrderIndex }).IsUnique();
            entity.Property(e => e.ContractWorkItemId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.EstimatedDuration).HasMaxLength(100);
            entity.Property(e => e.ProgressNote).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(e => e.Milestone).WithMany(e => e.WorkItems)
                .HasForeignKey(e => e.MilestonesId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractChangeRequest>(entity =>
        {
            entity.HasKey(e => e.ContractChangeRequestId);
            entity.Property(e => e.ContractChangeRequestId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Reason).HasMaxLength(2000);
            entity.Property(e => e.RequestedChanges).HasColumnType("text");
            entity.Property(e => e.ResponseNote).HasMaxLength(2000);
            entity.Property(e => e.ClarificationRequestNote).HasMaxLength(2000);
            entity.Property(e => e.ClarificationResponseNote).HasMaxLength(2000);
            entity.Property(e => e.AffectedMilestoneIds).HasColumnType("uuid[]");
            entity.Property(e => e.AffectedWorkItemIds).HasColumnType("uuid[]");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(e => e.Contract).WithMany(e => e.ChangeRequests)
                .HasForeignKey(e => e.ContractsId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractAmendment>(entity =>
        {
            entity.HasKey(e => e.ContractAmendmentId);
            entity.HasIndex(e => e.ContractChangeRequestId).IsUnique();
            entity.HasIndex(e => new { e.ContractsId, e.RevisionNumber }).IsUnique();
            entity.Property(e => e.ContractAmendmentId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.OriginalTotalBudget).HasPrecision(18, 2);
            entity.Property(e => e.ProposedTotalBudget).HasPrecision(18, 2);
            entity.Property(e => e.BudgetDelta).HasPrecision(18, 2);
            entity.Property(e => e.Reason).HasMaxLength(2000);
            entity.Property(e => e.ReviewNote).HasMaxLength(2000);
            entity.Property(e => e.DocumentSnapshotJson).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(e => e.Contract).WithMany(e => e.Amendments)
                .HasForeignKey(e => e.ContractsId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ChangeRequest).WithOne(e => e.Amendment)
                .HasForeignKey<ContractAmendment>(e => e.ContractChangeRequestId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractAmendmentMilestone>(entity =>
        {
            entity.HasKey(e => e.ContractAmendmentMilestoneId);
            entity.HasIndex(e => new { e.ContractAmendmentId, e.OrderIndex }).IsUnique();
            entity.Property(e => e.ContractAmendmentMilestoneId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.EstimatedDuration).HasMaxLength(100);
            entity.HasOne(e => e.Amendment).WithMany(e => e.Milestones)
                .HasForeignKey(e => e.ContractAmendmentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractAmendmentWorkItem>(entity =>
        {
            entity.HasKey(e => e.ContractAmendmentWorkItemId);
            entity.HasIndex(e => new { e.ContractAmendmentMilestoneId, e.OrderIndex }).IsUnique();
            entity.Property(e => e.ContractAmendmentWorkItemId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.EstimatedDuration).HasMaxLength(100);
            entity.HasOne(e => e.Milestone).WithMany(e => e.WorkItems)
                .HasForeignKey(e => e.ContractAmendmentMilestoneId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractAmendmentSignature>(entity =>
        {
            entity.HasKey(e => e.ContractAmendmentSignatureId);
            entity.HasIndex(e => new { e.ContractAmendmentId, e.UserId }).IsUnique();
            entity.Property(e => e.ContractAmendmentSignatureId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.SignatureData).HasColumnType("text");
            entity.HasOne(e => e.Amendment).WithMany(e => e.Signatures)
                .HasForeignKey(e => e.ContractAmendmentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractPlanRevision>(entity =>
        {
            entity.HasKey(e => e.ContractPlanRevisionId);
            entity.HasIndex(e => new { e.ContractsId, e.RevisionNumber }).IsUnique();
            entity.Property(e => e.ContractPlanRevisionId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.SnapshotJson).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(e => e.Contract).WithMany(e => e.PlanRevisions)
                .HasForeignKey(e => e.ContractsId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractEscrow>(entity =>
        {
            entity.HasKey(e => e.ContractEscrowId).HasName("ContractEscrows_pkey");

            entity.HasIndex(e => e.ContractsId, "IX_ContractEscrows_ContractsId").IsUnique();

            entity.HasIndex(e => e.Status, "IX_ContractEscrows_Status");

            entity.Property(e => e.ContractEscrowId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ContractEscrowId");
            entity.Property(e => e.ContractsId).HasColumnName("ContractsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Currency)
                .HasMaxLength(20)
                .HasDefaultValueSql("'VND'::character varying");
            entity.Property(e => e.FundedAmount).HasPrecision(18, 2);
            entity.Property(e => e.ReleasedAmount)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m);
            entity.Property(e => e.RequiredAmount).HasPrecision(18, 2);
            entity.Property(e => e.RequiredPercentage)
                .HasPrecision(5, 4)
                .HasDefaultValue(1.0m);
            entity.Property(e => e.Status)
                .HasComment("Enum ContractEscrowStatus: 0=PendingFunding, 1=PartiallyFunded, 2=Funded, 3=PartiallyReleased, 4=Released, 5=Refunded, 6=Cancelled, 7=Disputed");

            entity.HasOne(d => d.Contract).WithOne(p => p.ContractEscrow)
                .HasForeignKey<ContractEscrow>(d => d.ContractsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ContractEscrows_cont_ContractsId_fkey");
        });

        modelBuilder.Entity<ContractProductHandoff>(entity =>
        {
            entity.HasKey(e => e.ContractProductHandoffId).HasName("ContractProductHandoffs_pkey");

            entity.HasIndex(e => e.ContractsId, "IX_ContractProductHandoffs_ContractsId");

            entity.HasIndex(e => new { e.ContractsId, e.IsCurrent }, "IX_ContractProductHandoffs_ContractsId_IsCurrent");

            entity.HasIndex(e => new { e.ContractsId, e.Version }, "IX_ContractProductHandoffs_ContractsId_Version");

            entity.Property(e => e.ContractProductHandoffId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ContractProductHandoffId");
            entity.Property(e => e.ContractsId).HasColumnName("ContractsId");
            entity.Property(e => e.SubmittedByUserId).HasColumnName("SubmittedByUserId");
            entity.Property(e => e.ReceivedByUserId).HasColumnName("ReceivedByUserId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.FileName).HasMaxLength(500);
            entity.Property(e => e.MimeType).HasMaxLength(200);
            entity.Property(e => e.Note).HasMaxLength(2000);
            entity.Property(e => e.SourceType)
                .HasComment("Enum ContractProductHandoffSourceType: 0=File, 1=Link");
            entity.Property(e => e.Version).HasDefaultValue(1);
            entity.Property(e => e.IsCurrent).HasDefaultValue(true);

            entity.HasOne(d => d.Contract).WithMany(p => p.ContractProductHandoffs)
                .HasForeignKey(d => d.ContractsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ContractProductHandoffs_cont_ContractsId_fkey");

            entity.HasOne(d => d.SubmittedByUser).WithMany(p => p.SubmittedContractProductHandoffs)
                .HasForeignKey(d => d.SubmittedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ContractProductHandoffs_usr_SubmittedByUserId_fkey");

            entity.HasOne(d => d.ReceivedByUser).WithMany(p => p.ReceivedContractProductHandoffs)
                .HasForeignKey(d => d.ReceivedByUserId)
                .HasConstraintName("ContractProductHandoffs_usr_ReceivedByUserId_fkey");
        });

        modelBuilder.Entity<EscrowTransaction>(entity =>
        {
            entity.HasKey(e => e.EscrowTransactionId).HasName("EscrowTransactions_pkey");

            entity.HasIndex(e => e.ContractEscrowId, "IX_EscrowTransactions_ContractEscrowId");

            entity.HasIndex(e => e.GatewayTransactionCode, "IX_EscrowTransactions_GatewayTransactionCode");

            entity.HasIndex(e => e.MilestonesId, "IX_EscrowTransactions_MilestonesId");

            entity.HasIndex(e => e.Status, "IX_EscrowTransactions_Status");

            entity.Property(e => e.EscrowTransactionId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("EscrowTransactionId");
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.ContractEscrowId).HasColumnName("ContractEscrowId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.GatewayTransactionCode).HasMaxLength(200);
            entity.Property(e => e.MilestonesId).HasColumnName("MilestonesId");
            entity.Property(e => e.PaymentGateway).HasMaxLength(100);
            entity.Property(e => e.Status).HasComment("Enum EscrowTransactionStatus: 0=Pending, 1=Succeeded, 2=Failed, 3=Cancelled");
            entity.Property(e => e.Type).HasComment("Enum EscrowTransactionType: 0=Deposit, 1=ReleaseToFreelancer, 2=RefundToClient, 3=PlatformFee, 4=Adjustment");

            entity.HasOne(d => d.ContractEscrow).WithMany(p => p.EscrowTransactions)
                .HasForeignKey(d => d.ContractEscrowId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("EscrowTransactions_cEsc_ContractEscrowId_fkey");

            entity.HasOne(d => d.Milestone).WithMany(p => p.EscrowTransactions)
                .HasForeignKey(d => d.MilestonesId)
                .HasConstraintName("EscrowTransactions_mStone_MilestonesId_fkey");
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.ConversationsId).HasName("Conversations_pkey");

            entity.HasIndex(e => e.ContractsId, "IX_Conversations_ContractsId");

            entity.HasIndex(e => e.DisputesId, "IX_Conversations_DisputesId");

            entity.HasIndex(e => e.JobPostsId, "IX_Conversations_JobPostsId");

            entity.HasIndex(e => e.LastMessageAt, "IX_Conversations_LastMessageAt").IsDescending();

            entity.HasIndex(e => e.LastMessageId, "IX_Conversations_LastMessageId");

            entity.HasIndex(e => e.ProposalsId, "IX_Conversations_ProposalsId");

            entity.HasIndex(e => e.CreatedByUserId, "IX_Conversations_CreatedByUserId");

            entity.Property(e => e.ConversationsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ConversationsId");
            entity.Property(e => e.ConversationType)
                .HasDefaultValue(0)
                .HasComment("Enum ConversationType: 0=JobNegotiation, 1=ContractWorkroom, 2=Dispute, 3=Support");
            entity.Property(e => e.ContractsId).HasColumnName("ContractsId");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.DisputesId).HasColumnName("DisputesId");
            entity.Property(e => e.JobPostsId).HasColumnName("JobPostsId");
            entity.Property(e => e.LastMessageId).HasColumnName("LastMessageId");
            entity.Property(e => e.ProposalsId).HasColumnName("ProposalsId");
            entity.Property(e => e.Status)
                .HasDefaultValue(0)
                .HasComment("Enum ConversationStatus: 0=Active, 1=Archived, 2=Closed");
            entity.Property(e => e.Title).HasMaxLength(300);

            entity.HasOne(d => d.Contracts).WithMany(p => p.Conversations)
                .HasForeignKey(d => d.ContractsId)
                .HasConstraintName("Conversations_cont_ContractsId_fkey");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.CreatedConversations)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Conversations_usr_CreatedByUserId_fkey");

            entity.HasOne(d => d.Disputes).WithMany(p => p.Conversations)
                .HasForeignKey(d => d.DisputesId)
                .HasConstraintName("Conversations_disp_DisputesId_fkey");

            entity.HasOne(d => d.JobPosts).WithMany(p => p.Conversations)
                .HasForeignKey(d => d.JobPostsId)
                .HasConstraintName("Conversations_jp_JobPostsId_fkey");

            entity.HasOne(d => d.LastMessage).WithMany(p => p.LastMessageForConversations)
                .HasForeignKey(d => d.LastMessageId)
                .HasConstraintName("Conversations_msg_LastMessageId_fkey");

            entity.HasOne(d => d.Proposals).WithMany(p => p.Conversations)
                .HasForeignKey(d => d.ProposalsId)
                .HasConstraintName("Conversations_propo_ProposalsId_fkey");
        });

        modelBuilder.Entity<ConversationParticipant>(entity =>
        {
            entity.HasKey(e => e.ConversationParticipantId).HasName("ConversationParticipants_pkey");

            entity.HasIndex(e => e.ConversationsId, "IX_ConversationParticipants_ConversationsId");

            entity.HasIndex(e => e.UserId, "IX_ConversationParticipants_UserId");

            entity.HasIndex(e => new { e.ConversationsId, e.UserId }, "ConversationParticipants_conv_User_key").IsUnique();

            entity.Property(e => e.ConversationParticipantId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ConversationParticipantId");
            entity.Property(e => e.ConversationsId).HasColumnName("ConversationsId");
            entity.Property(e => e.JoinedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.LastReadMessageId).HasColumnName("LastReadMessageId");
            entity.Property(e => e.ParticipantRole)
                .HasComment("Enum ParticipantRole: 0=Client, 1=Freelancer, 2=Admin, 3=Support");
            entity.Property(e => e.UnreadCount).HasDefaultValue(0);
            entity.Property(e => e.UserId).HasColumnName("UserId");

            entity.HasOne(d => d.Conversations).WithMany(p => p.Participants)
                .HasForeignKey(d => d.ConversationsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ConversationParticipants_conv_ConversationsId_fkey");

            entity.HasOne(d => d.LastReadMessage).WithMany(p => p.LastReadByParticipants)
                .HasForeignKey(d => d.LastReadMessageId)
                .HasConstraintName("ConversationParticipants_msg_LastReadMessageId_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.ConversationParticipants)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ConversationParticipants_usr_UserId_fkey");
        });

        modelBuilder.Entity<Dispute>(entity =>
        {
            entity.HasKey(e => e.DisputesId).HasName("Disputes_pkey");

            entity.HasIndex(e => e.ContractsId, "IX_Disputes_ContractsId");

            entity.HasIndex(e => e.InitiatorId, "IX_Disputes_InitiatorId");

            entity.HasIndex(e => e.RespondentId, "IX_Disputes_RespondentId");

            entity.HasIndex(e => e.ResolvedByAdminId, "IX_Disputes_ResolvedByAdminId");

            entity.HasIndex(e => e.AssignedAdminId, "IX_Disputes_AssignedAdminId");

            entity.HasIndex(e => e.Status, "IX_Disputes_Status");

            entity.HasIndex(e => new { e.Status, e.IsVipPriority, e.ResolutionTargetAt },
                "IX_Disputes_Status_Vip_ResolutionTarget");

            entity.Property(e => e.DisputesId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("DisputesId");
            entity.Property(e => e.ContractsId).HasColumnName("ContractsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.InitiatorId).HasColumnName("InitiatorId");
            entity.Property(e => e.MilestonesId).HasColumnName("MilestonesId");
            entity.Property(e => e.RespondentId).HasColumnName("RespondentId");
            entity.Property(e => e.RelatedReportId).HasColumnName("RelatedReportId");
            entity.Property(e => e.Title).HasMaxLength(300);
            entity.Property(e => e.Description).HasMaxLength(5000);
            entity.Property(e => e.ClaimedAmount).HasPrecision(18, 2);
            entity.Property(e => e.RequestedResolution).HasMaxLength(2000);
            entity.Property(e => e.Urgency)
                .HasDefaultValue((int)DisputeUrgency.Normal)
                .HasComment("Enum DisputeUrgency: 0=Normal, 1=High, 2=Critical");
            entity.Property(e => e.OpenedAt);
            entity.Property(e => e.Resolution).HasComment("Enum DisputeResolution: 0=ClientFavored, 1=FreelancerFavored, 2=Split, 3=Dismissed");
            entity.Property(e => e.Status).HasComment("Enum DisputeStatus: 0=Open, 1=WaitingAdmin, 2=UnderReview, 3=WaitingEvidence, 4=DecisionPending, 5=Resolved, 6=Closed");
            entity.Property(e => e.AssignedAdminId).HasColumnName("AssignedAdminId");
            entity.Property(e => e.AssignedAt);
            entity.Property(e => e.IsVipPriority).HasDefaultValue(false);
            entity.Property(e => e.AiAnalysisStatus).HasConversion<int>();

            entity.HasOne(d => d.Contracts).WithMany(p => p.Disputes)
                .HasForeignKey(d => d.ContractsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Disputes_cont_ContractsId_fkey");

            entity.HasOne(d => d.Milestones).WithMany(p => p.Disputes)
                .HasForeignKey(d => d.MilestonesId)
                .HasConstraintName("Disputes_mStone_MilestonesId_fkey");

            entity.HasOne(d => d.Respondent).WithMany(p => p.DisputeRespondents)
                .HasForeignKey(d => d.RespondentId)
                .HasConstraintName("Disputes_usr_RespondentId_fkey");

            entity.HasOne(d => d.RelatedReport).WithMany()
                .HasForeignKey(d => d.RelatedReportId)
                .HasConstraintName("Disputes_rc_RelatedReportId_fkey");

            entity.HasOne(d => d.ResolvedByAdmin).WithMany(p => p.DisputeResolvedByAdmins)
                .HasForeignKey(d => d.ResolvedByAdminId)
                .HasConstraintName("Disputes_ResolvedByAdminId_fkey");

            entity.HasOne(d => d.AssignedAdmin).WithMany(p => p.DisputeAssignedByAdmins)
                .HasForeignKey(d => d.AssignedAdminId)
                .HasConstraintName("Disputes_AssignedAdminId_fkey");

            entity.HasOne(d => d.Initiator).WithMany(p => p.DisputeInitiators)
                .HasForeignKey(d => d.InitiatorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Disputes_usr_InitiatorId_fkey");
        });

        modelBuilder.Entity<DisputeEvidence>(entity =>
        {
            entity.HasKey(e => e.DisputeEvidenceId).HasName("DisputeEvidence_pkey");

            entity.ToTable("DisputeEvidence");

            entity.HasIndex(e => e.DisputesId, "IX_DisputeEvidence_DisputesId");

            entity.HasIndex(e => new { e.DisputesId, e.RequestGroupId }, "IX_DisputeEvidence_DisputesId_RequestGroupId");

            entity.HasIndex(e => e.RequestedByAdminId, "IX_DisputeEvidence_RequestedByAdminId");

            entity.HasIndex(e => e.ReviewedByAdminId, "IX_DisputeEvidence_ReviewedByAdminId");

            entity.Property(e => e.DisputeEvidenceId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("DisputeEvidenceId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.DisputesId).HasColumnName("DisputesId");
            entity.Property(e => e.FileName).HasMaxLength(500);
            entity.Property(e => e.IsRequestedByAdmin).HasDefaultValue(false);
            entity.Property(e => e.IsRequestFulfilled).HasDefaultValue(false);
            entity.Property(e => e.RequestTarget)
                .HasComment("Enum EvidenceRequestTarget: 0=Reporter, 1=Respondent, 2=Both");
            entity.Property(e => e.ReviewNote).HasMaxLength(2000);
            entity.Property(e => e.UploadedById).HasColumnName("UploadedById");

            entity.HasOne(d => d.Disputes).WithMany(p => p.DisputeEvidences)
                .HasForeignKey(d => d.DisputesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("DisputeEvidence_disp_DisputesId_fkey");

            entity.HasOne(d => d.UploadedBy).WithMany(p => p.DisputeEvidences)
                .HasForeignKey(d => d.UploadedById)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("DisputeEvidence_usr_UploadedById_fkey");

            entity.HasOne(d => d.RequestedByAdmin).WithMany()
                .HasForeignKey(d => d.RequestedByAdminId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("DisputeEvidence_RequestedByAdminId_fkey");

            entity.HasOne(d => d.ReviewedByAdmin).WithMany()
                .HasForeignKey(d => d.ReviewedByAdminId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("DisputeEvidence_ReviewedByAdminId_fkey");
        });

        modelBuilder.Entity<DisputeMilestoneDecision>(entity =>
        {
            entity.HasKey(e => e.DisputeMilestoneDecisionId);
            entity.HasIndex(e => new { e.DisputesId, e.MilestonesId }).IsUnique();
            entity.HasIndex(e => e.DecidedByAdminId);
            entity.Property(e => e.Outcome)
                .HasComment("Enum DisputeMilestoneOutcome: 0=Accepted, 1=Rejected, 2=PartiallyAccepted, 3=Cancelled");
            entity.Property(e => e.MilestoneAmountSnapshot).HasPrecision(18, 2);
            entity.Property(e => e.ReleasedAmountSnapshot).HasPrecision(18, 2);
            entity.Property(e => e.AdditionalReleaseAmount).HasPrecision(18, 2);
            entity.Property(e => e.RefundAmount).HasPrecision(18, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(e => e.Dispute).WithMany(d => d.MilestoneDecisions)
                .HasForeignKey(e => e.DisputesId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Milestone).WithMany()
                .HasForeignKey(e => e.MilestonesId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.DecidedByAdmin).WithMany()
                .HasForeignKey(e => e.DecidedByAdminId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EsignDocument>(entity =>
        {
            entity.HasKey(e => e.EsignDocumentsId).HasName("ESignDocuments_pkey");

            entity.ToTable("ESignDocuments");

            entity.HasIndex(e => e.ContractsId, "IX_ESignDocuments_ContractsId");

            entity.HasIndex(e => e.DocumentCode, "IX_ESignDocuments_DocumentCode").IsUnique();

            entity.HasIndex(e => e.JobPostsId, "IX_ESignDocuments_JobPostsId");

            entity.HasIndex(e => e.Status, "IX_ESignDocuments_Status");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_ESignDocuments_Status_CreatedAt").IsDescending(false, true);

            entity.Property(e => e.EsignDocumentsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ESignDocumentsId");
            entity.Property(e => e.ContractsId).HasColumnName("ContractsId");
            entity.Property(e => e.ContractSnapshotJson).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.DocumentCode).HasMaxLength(50);
            entity.Property(e => e.DocumentHash).HasMaxLength(128);
            entity.Property(e => e.EsignTemplatesId).HasColumnName("ESignTemplatesId");
            entity.Property(e => e.FinalizedDocumentContent).HasColumnType("bytea");
            entity.Property(e => e.FinalizedDocumentFileName).HasMaxLength(255);
            entity.Property(e => e.FinalizedDocumentMimeType).HasMaxLength(150);
            entity.Property(e => e.JobPostsId).HasColumnName("JobPostsId");
            entity.Property(e => e.Status)
                .HasDefaultValue(0)
                .HasComment("Enum ESignDocumentStatus: 0=Draft, 1=PendingSignatures, 2=PartiallySigned, 3=FullySigned, 4=Expired, 5=Voided");

            entity.HasOne(d => d.Contracts).WithMany(p => p.EsignDocuments)
                .HasForeignKey(d => d.ContractsId)
                .HasConstraintName("ESignDocuments_cont_ContractsId_fkey");

            entity.HasOne(d => d.EsignTemplates).WithMany(p => p.EsignDocuments)
                .HasForeignKey(d => d.EsignTemplatesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ESignDocuments_eTpl_ESignTemplatesId_fkey");

            entity.HasOne(d => d.JobPosts).WithMany(p => p.EsignDocuments)
                .HasForeignKey(d => d.JobPostsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ESignDocuments_jp_JobPostsId_fkey");
        });

        modelBuilder.Entity<EsignSignature>(entity =>
        {
            entity.HasKey(e => e.EsignSignaturesId).HasName("ESignSignatures_pkey");

            entity.ToTable("ESignSignatures");

            entity.HasIndex(e => new { e.EsignDocumentsId, e.UserId }, "ESignSignatures_eDoc_ESignDocumentsId_usr_UserId_key").IsUnique();

            entity.HasIndex(e => new { e.EsignDocumentsId, e.Status }, "IX_ESignSignatures_DocId_Status");

            entity.HasIndex(e => e.EsignDocumentsId, "IX_ESignSignatures_ESignDocumentsId");

            entity.HasIndex(e => e.Status, "IX_ESignSignatures_Status");

            entity.HasIndex(e => e.UserId, "IX_ESignSignatures_UserId");

            entity.Property(e => e.EsignSignaturesId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ESignSignaturesId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.EsignDocumentsId).HasColumnName("ESignDocumentsId");
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.PolicyVersion).HasMaxLength(50);
            entity.Property(e => e.SignerRole).HasComment("Enum ESignerRole: 0=Client, 1=Freelancer");
            entity.Property(e => e.Status)
                .HasDefaultValue(0)
                .HasComment("Enum ESignSignatureStatus: 0=Pending, 1=Signed, 2=Declined");
            entity.Property(e => e.UserId).HasColumnName("UserId");

            entity.HasOne(d => d.EsignDocuments).WithMany(p => p.EsignSignatures)
                .HasForeignKey(d => d.EsignDocumentsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ESignSignatures_eDoc_ESignDocumentsId_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.EsignSignatures)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ESignSignatures_usr_UserId_fkey");
        });

        modelBuilder.Entity<EsignTemplate>(entity =>
        {
            entity.HasKey(e => e.EsignTemplatesId).HasName("ESignTemplates_pkey");

            entity.ToTable("ESignTemplates");

            entity.HasIndex(e => e.CreatedBy, "IX_ESignTemplates_CreatedBy");

            entity.HasIndex(e => e.IsActive, "IX_ESignTemplates_IsActive");

            entity.HasIndex(e => e.Name, "IX_ESignTemplates_Name");

            entity.HasIndex(e => new { e.TemplateCode, e.IsActive }, "IX_ESignTemplates_TemplateCode_IsActive");

            entity.Property(e => e.EsignTemplatesId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ESignTemplatesId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(300);
            entity.Property(e => e.PlaceholderSchema).HasColumnType("jsonb");
            entity.Property(e => e.TemplateCode)
                .HasMaxLength(100)
                .HasDefaultValue("CONTRACT_FIXED_PRICE");
            entity.Property(e => e.Version).HasDefaultValue(1);

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.EsignTemplates)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ESignTemplates_CreatedBy_fkey");
        });

        modelBuilder.Entity<Faq>(entity =>
        {
            entity.HasKey(e => e.FaqsId).HasName("FAQs_pkey");

            entity.ToTable("FAQs");

            entity.HasIndex(e => e.FaqcategoriesId, "IX_FAQs_FAQCategoriesId");

            entity.HasIndex(e => e.IsActive, "IX_FAQs_IsActive");

            entity.Property(e => e.FaqsId)
                .UseIdentityByDefaultColumn()
                .HasColumnName("FAQsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.FaqcategoriesId).HasColumnName("FAQCategoriesId");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);

            entity.HasOne(d => d.Faqcategories).WithMany(p => p.Faqs)
                .HasForeignKey(d => d.FaqcategoriesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FAQs_faqCat_FAQCategoriesId_fkey");
        });

        modelBuilder.Entity<Faqcategory>(entity =>
        {
            entity.HasKey(e => e.FaqcategoriesId).HasName("FAQCategories_pkey");

            entity.ToTable("FAQCategories");

            entity.HasIndex(e => e.Slug, "IX_FAQCategories_Slug").IsUnique();

            entity.Property(e => e.FaqcategoriesId)
                .UseIdentityByDefaultColumn()
                .HasColumnName("FAQCategoriesId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Slug).HasMaxLength(200);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
        });

        modelBuilder.Entity<FreelancerProfile>(entity =>
        {
            entity.HasKey(e => e.FreelancerProfilesId).HasName("FreelancerProfiles_pkey");

            entity.HasIndex(e => e.UserId, "FreelancerProfiles_usr_UserId_key").IsUnique();

            entity.HasIndex(e => e.Availability, "IX_FreelancerProfiles_Availability");

            entity.HasIndex(e => e.MajorId, "IX_FreelancerProfiles_MajorId");

            entity.Property(e => e.FreelancerProfilesId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("FreelancerProfilesId");
            entity.Property(e => e.Availability)
                .HasDefaultValue(0)
                .HasComment("Enum Availability: 0=FullTime, 1=PartTime, 2=NotAvailable");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Location).HasMaxLength(300);
            entity.Property(e => e.Title).HasMaxLength(300);
            entity.Property(e => e.UserId).HasColumnName("UserId");

            entity.Property(e => e.MajorId).HasColumnName("MajorId");

            entity.HasOne(d => d.User).WithOne(p => p.FreelancerProfile)
                .HasForeignKey<FreelancerProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FreelancerProfiles_usr_UserId_fkey");

            entity.HasOne(e => e.Major)
                .WithMany(e => e.FreelancerProfiles)
                .HasForeignKey(e => e.MajorId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FreelancerProfiles_major_MajorId_fkey");
        });

        modelBuilder.Entity<FreelancerProfileCategory>(entity =>
        {
            entity.HasKey(e => e.FreelancerProfileCategoriesId)
                .HasName("FreelancerProfileCategories_pkey");

            entity.Property(e => e.FreelancerProfileCategoriesId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("FreelancerProfileCategoriesId");
            entity.Property(e => e.FreelancerProfileId).HasColumnName("FreelancerProfileId");
            entity.Property(e => e.MajorCategoryId).HasColumnName("MajorCategoryId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasIndex(e => e.FreelancerProfileId, "IX_FreelancerProfileCategories_FreelancerProfileId");
            entity.HasIndex(e => e.MajorCategoryId, "IX_FreelancerProfileCategories_MajorCategoryId");
            entity.HasIndex(
                    e => new { e.FreelancerProfileId, e.MajorCategoryId },
                    "FreelancerProfileCategories_Profile_MajorCategory_key")
                .IsUnique();

            entity.HasOne(e => e.FreelancerProfile)
                .WithMany(e => e.FreelancerProfileCategories)
                .HasForeignKey(e => e.FreelancerProfileId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FreelancerProfileCategories_profile_FreelancerProfileId_fkey");

            entity.HasOne(e => e.MajorCategory)
                .WithMany(e => e.FreelancerProfileCategories)
                .HasForeignKey(e => e.MajorCategoryId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FreelancerProfileCategories_majorCategory_MajorCategoryId_fkey");
        });

        modelBuilder.Entity<FreelancerSkill>(entity =>
        {
            entity.HasKey(e => e.FreelancerSkillsId).HasName("FreelancerSkills_pkey");

            entity.HasIndex(e => new { e.FreelancerId, e.SkillsId }, "FreelancerSkills_fl_FreelancerId_sk_SkillsId_key").IsUnique();

            entity.HasIndex(e => e.FreelancerId, "IX_FreelancerSkills_FreelancerId");

            entity.HasIndex(e => e.SkillsId, "IX_FreelancerSkills_SkillsId");

            entity.Property(e => e.FreelancerSkillsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("FreelancerSkillsId");
            entity.Property(e => e.FreelancerId).HasColumnName("FreelancerId");
            entity.Property(e => e.ProficiencyLevel).HasComment("Enum ProficiencyLevel: 0=Beginner, 1=Intermediate, 2=Advanced, 3=Expert");
            entity.Property(e => e.SkillsId).HasColumnName("SkillsId");

            entity.HasOne(d => d.Freelancer).WithMany(p => p.FreelancerSkills)
                .HasForeignKey(d => d.FreelancerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FreelancerSkills_fl_FreelancerId_fkey");

            entity.HasOne(d => d.Skills).WithMany(p => p.FreelancerSkills)
                .HasForeignKey(d => d.SkillsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FreelancerSkills_sk_SkillsId_fkey");
        });

        modelBuilder.Entity<JobInvitation>(entity =>
        {
            entity.HasKey(e => e.JobInvitationsId).HasName("JobInvitations_pkey");

            entity.HasIndex(e => e.JobPostsId, "IX_JobInvitations_JobPostsId");

            entity.HasIndex(e => e.ClientProfilesId, "IX_JobInvitations_ClientProfilesId");

            entity.HasIndex(e => e.FreelancerProfilesId, "IX_JobInvitations_FreelancerProfilesId");

            entity.HasIndex(e => e.ProposalsId, "IX_JobInvitations_ProposalsId")
                .IsUnique();

            entity.HasIndex(e => e.Status, "IX_JobInvitations_Status");

            entity.HasIndex(e => new { e.JobPostsId, e.FreelancerProfilesId }, "JobInvitations_jp_JobPostsId_flPro_FreelancerProfilesId_key")
                .IsUnique();

            entity.HasIndex(e => new { e.ClientProfilesId, e.JobPostsId }, "IX_JobInvitations_ClientProfilesId_JobPostsId");

            entity.HasIndex(e => new { e.FreelancerProfilesId, e.Status }, "IX_JobInvitations_FreelancerProfilesId_Status");

            entity.Property(e => e.JobInvitationsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("JobInvitationsId");

            entity.Property(e => e.JobPostsId)
                .HasColumnName("JobPostsId");

            entity.Property(e => e.ClientProfilesId)
                .HasColumnName("ClientProfilesId");

            entity.Property(e => e.FreelancerProfilesId)
                .HasColumnName("FreelancerProfilesId");

            entity.Property(e => e.ProposalsId)
                .HasColumnName("ProposalsId");

            entity.Property(e => e.Status)
                .HasDefaultValue(0)
                .HasComment("Enum JobInvitationStatus: 0=Pending, 1=Viewed, 2=Applied, 3=Declined, 4=Expired, 5=Cancelled");

            entity.Property(e => e.Message)
                .HasMaxLength(1000);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()");

            entity.Property(e => e.DeclineReason)
                .HasMaxLength(500);

            entity.HasOne(d => d.JobPosts)
                .WithMany(p => p.JobInvitations)
                .HasForeignKey(d => d.JobPostsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("JobInvitations_jp_JobPostsId_fkey");

            entity.HasOne(d => d.ClientProfiles)
                .WithMany()
                .HasForeignKey(d => d.ClientProfilesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("JobInvitations_clPro_ClientProfilesId_fkey");

            entity.HasOne(d => d.FreelancerProfiles)
                .WithMany()
                .HasForeignKey(d => d.FreelancerProfilesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("JobInvitations_flPro_FreelancerProfilesId_fkey");

            entity.HasOne(d => d.Proposals)
                .WithOne()
                .HasForeignKey<JobInvitation>(d => d.ProposalsId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("JobInvitations_propo_ProposalsId_fkey");
        });

        modelBuilder.Entity<JobPost>(entity =>
        {
            entity.HasKey(e => e.JobPostsId).HasName("JobPosts_pkey");

            entity.HasIndex(e => e.EndDate, "IX_JobPosts_EndDate");

            entity.HasIndex(e => e.MajorCategoryId, "IX_JobPosts_MajorCategoryId");

            entity.HasIndex(e => e.ClientProfilesId, "IX_JobPosts_ClientProfilesId");

            entity.HasIndex(e => e.CreatedAt, "IX_JobPosts_CreatedAt")
                .IsDescending();

            entity.HasIndex(e => e.Status, "IX_JobPosts_Status");

            entity.HasIndex(e => new { e.Status, e.Visibility }, "IX_JobPosts_Status_Visibility");

            entity.HasIndex(e => new { e.Status, e.Visibility, e.CreatedAt }, "IX_JobPosts_Status_Visibility_CreatedAt")
                .IsDescending(false, false, true);

            entity.HasIndex(e => new { e.IsFeatured, e.FeaturedUntil },
                "IX_JobPosts_IsFeatured_FeaturedUntil");

            entity.Property(e => e.JobPostsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("JobPostsId");

            entity.Property(e => e.MajorCategoryId)
                .HasColumnName("MajorCategoryId");

            entity.Property(e => e.BudgetMax)
                .HasPrecision(18, 2);

            entity.Property(e => e.BudgetMin)
                .HasPrecision(18, 2);

            entity.Property(e => e.ClientProfilesId)
                .HasColumnName("ClientProfilesId");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()");

            entity.Property(e => e.Currency)
                .HasMaxLength(5)
                .HasDefaultValueSql("'VND'::character varying");

            entity.Property(e => e.EstimatedDuration)
                .HasMaxLength(100);

            entity.Property(e => e.IsAigenerated)
                .HasDefaultValue(false)
                .HasColumnName("IsAIGenerated");

            entity.Property(e => e.IsFeatured).HasDefaultValue(false);

            entity.Property(e => e.Status)
                .HasComment("Enum JobPostStatus: 0=Draft, 1=Open, 2=Closed, 3=Cancelled");

            entity.Property(e => e.Title)
                .HasMaxLength(500);

            entity.Property(e => e.Visibility)
                .HasDefaultValue(0)
                .HasComment("Enum JobPostVisibility: 0=Public, 1=Private, 2=InviteOnly");

            entity.Property(e => e.CustomSkillNames)
                .HasColumnType("text[]")
                .HasDefaultValueSql("ARRAY[]::text[]");

            entity.HasOne(e => e.MajorCategory)
                .WithMany(e => e.JobPosts)
                .HasForeignKey(e => e.MajorCategoryId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("JobPosts_MajorCategoryId_fkey");

            entity.HasOne(d => d.ClientProfiles)
                .WithMany(p => p.JobPosts)
                .HasForeignKey(d => d.ClientProfilesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("JobPosts_clPro_ClientProfilesId_fkey");
        });

        modelBuilder.Entity<JobPostMilestonePlan>(entity =>
        {
            entity.HasKey(e => e.JobPostMilestonePlanId);
            entity.HasIndex(e => new { e.JobPostsId, e.OrderIndex }).IsUnique();
            entity.Property(e => e.JobPostMilestonePlanId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.EstimatedDuration).HasMaxLength(100);
            entity.Property(e => e.DueDate).HasColumnType("date");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(e => e.JobPost).WithMany(e => e.JobPostMilestonePlans)
                .HasForeignKey(e => e.JobPostsId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobPostWorkItem>(entity =>
        {
            entity.HasKey(e => e.JobPostWorkItemId);
            entity.HasIndex(e => new { e.JobPostMilestonePlanId, e.OrderIndex }).IsUnique();
            entity.Property(e => e.JobPostWorkItemId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.EstimatedDuration).HasMaxLength(100);
            entity.HasOne(e => e.MilestonePlan).WithMany(e => e.WorkItems)
                .HasForeignKey(e => e.JobPostMilestonePlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobPostPromotion>(entity =>
        {
            entity.ToTable("JobPostPromotions");
            entity.HasKey(e => e.JobPostPromotionsId);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(200);
            entity.Property(e => e.ImageUrl).HasMaxLength(2048);
            entity.Property(e => e.PromotionTitle).HasMaxLength(140);
            entity.Property(e => e.PromotionDescription).HasMaxLength(1000);
            entity.Property(e => e.TokenCost).HasPrecision(18, 4);
            entity.HasIndex(e => new { e.ClientUserId, e.IdempotencyKey }).IsUnique();
            entity.HasIndex(e => new { e.JobPostId, e.FeaturedUntil });
            entity.HasOne(e => e.JobPost).WithMany(e => e.JobPostPromotions)
                .HasForeignKey(e => e.JobPostId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ClientUser).WithMany()
                .HasForeignKey(e => e.ClientUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.WalletTransaction).WithMany()
                .HasForeignKey(e => e.WalletTransactionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AiInterviewDefinition>(entity =>
        {
            entity.ToTable("AiInterviewDefinitions");
            entity.HasKey(e => e.AiInterviewDefinitionsId);
            entity.Property(e => e.Language).HasMaxLength(20);
            entity.Property(e => e.Mode).HasMaxLength(20);
            entity.Property(e => e.ExternalReference).HasMaxLength(200);
            entity.HasIndex(e => new { e.ClientUserId, e.JobPostId, e.Status });
            entity.HasOne(e => e.JobPost).WithMany().HasForeignKey(e => e.JobPostId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ClientUser).WithMany().HasForeignKey(e => e.ClientUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AiInterviewAttempt>(entity =>
        {
            entity.ToTable("AiInterviewAttempts");
            entity.HasKey(e => e.AiInterviewAttemptsId);
            entity.Property(e => e.ExternalSessionId).HasMaxLength(128);
            entity.HasIndex(e => e.ExternalSessionId).IsUnique();
            entity.HasIndex(e => new { e.AiInterviewDefinitionId, e.Status });
            entity.HasOne(e => e.Definition).WithMany(e => e.Attempts)
                .HasForeignKey(e => e.AiInterviewDefinitionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.FreelancerUser).WithMany().HasForeignKey(e => e.FreelancerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AiInterviewAnswerResult>(entity =>
        {
            entity.ToTable("AiInterviewAnswerResults");
            entity.HasKey(e => e.AiInterviewAnswerResultsId);
            entity.HasIndex(e => new { e.AiInterviewAttemptId, e.QuestionIndex }).IsUnique();
            entity.HasOne(e => e.Attempt).WithMany(e => e.Answers)
                .HasForeignKey(e => e.AiInterviewAttemptId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobPostAttachment>(entity =>
        {
            entity.HasKey(e => e.JobPostAttachmentsId).HasName("JobPostAttachments_pkey");

            entity.HasIndex(e => e.JobPostsId, "IX_JobPostAttachments_JobPostsId");

            entity.Property(e => e.JobPostAttachmentsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("JobPostAttachmentsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.FileName).HasMaxLength(500);
            entity.Property(e => e.JobPostsId).HasColumnName("JobPostsId");

            entity.HasOne(d => d.JobPosts).WithMany(p => p.JobPostAttachments)
                .HasForeignKey(d => d.JobPostsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("JobPostAttachments_jp_JobPostsId_fkey");
        });
        modelBuilder.Entity<JobPostQuestion>(entity =>
        {
            entity.HasKey(e => e.JobPostQuestionsId).HasName("JobPostQuestions_pkey");

            entity.HasIndex(e => e.JobPostsId, "IX_JobPostQuestions_JobPostsId");

            entity.HasIndex(e => new { e.JobPostsId, e.OrderIndex }, "IX_JobPostQuestions_JobPostsId_OrderIndex")
                .IsUnique();

            entity.Property(e => e.JobPostQuestionsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("JobPostQuestionsId");

            entity.Property(e => e.JobPostsId)
                .HasColumnName("JobPostsId");

            entity.Property(e => e.QuestionText)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(e => e.OrderIndex)
                .HasDefaultValue(0);

            entity.Property(e => e.IsRequired)
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()");

            entity.HasOne(d => d.JobPosts)
                .WithMany(p => p.JobPostQuestions)
                .HasForeignKey(d => d.JobPostsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("JobPostQuestions_jp_JobPostsId_fkey");
        });

        modelBuilder.Entity<JobPostSkill>(entity =>
        {
            entity.HasKey(e => e.JobPostSkillsId).HasName("JobPostSkills_pkey");

            entity.HasIndex(e => e.JobPostsId, "IX_JobPostSkills_JobPostsId");

            entity.HasIndex(e => e.SkillsId, "IX_JobPostSkills_SkillsId");

            entity.HasIndex(e => new { e.JobPostsId, e.SkillsId }, "JobPostSkills_jp_JobPostsId_sk_SkillsId_key").IsUnique();

            entity.Property(e => e.JobPostSkillsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("JobPostSkillsId");
            entity.Property(e => e.IsRequired).HasDefaultValue(true);
            entity.Property(e => e.JobPostsId).HasColumnName("JobPostsId");
            entity.Property(e => e.SkillsId).HasColumnName("SkillsId");

            entity.HasOne(d => d.JobPosts).WithMany(p => p.JobPostSkills)
                .HasForeignKey(d => d.JobPostsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("JobPostSkills_jp_JobPostsId_fkey");

            entity.HasOne(d => d.Skills).WithMany(p => p.JobPostSkills)
                .HasForeignKey(d => d.SkillsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("JobPostSkills_sk_SkillsId_fkey");
        });

        modelBuilder.Entity<Major>(entity =>
        {
            entity.HasKey(e => e.MajorsId).HasName("Majors_pkey");

            entity.HasIndex(e => e.IsActive, "IX_Majors_IsActive");

            entity.HasIndex(e => e.Slug, "IX_Majors_Slug")
                .IsUnique();

            entity.Property(e => e.MajorsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("MajorsId");

            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.Slug)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.Description);

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);

            entity.Property(e => e.SortOrder)
                .HasDefaultValue(0);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<MajorCategory>(entity =>
        {
            entity.HasKey(e => e.MajorCategoriesId).HasName("MajorCategories_pkey");

            entity.HasIndex(e => e.MajorId, "IX_MajorCategories_MajorId");

            entity.HasIndex(e => e.CategoryId, "IX_MajorCategories_CategoryId");

            entity.HasIndex(e => new { e.MajorId, e.CategoryId }, "MajorCategories_MajorId_CategoryId_key")
                .IsUnique();

            entity.Property(e => e.MajorCategoriesId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("MajorCategoriesId");

            entity.Property(e => e.MajorId)
                .HasColumnName("MajorId");

            entity.Property(e => e.CategoryId)
                .HasColumnName("CategoryId");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()");

            entity.HasOne(e => e.Major)
                .WithMany(e => e.MajorCategories)
                .HasForeignKey(e => e.MajorId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("MajorCategories_major_MajorId_fkey");

            entity.HasOne(e => e.Category)
                .WithMany(e => e.MajorCategories)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("MajorCategories_cat_CategoryId_fkey");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.MessagesId).HasName("Messages_pkey");

            entity.HasIndex(e => new { e.ConversationsId, e.SentAt }, "IX_Messages_ConversationsId_SentAt").IsDescending(false, true);

            entity.HasIndex(e => e.SenderUserId, "IX_Messages_SenderUserId");

            entity.HasIndex(e => new { e.ScheduleId, e.ScheduleEventSequence }, "IX_Messages_ScheduleId_EventSequence");

            entity.HasIndex(e => new { e.ConversationsId, e.SenderUserId, e.ClientMessageId }, "Messages_conv_sender_client_key").IsUnique();

            entity.Property(e => e.MessagesId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("MessagesId");
            entity.Property(e => e.ClientMessageId).HasMaxLength(100);
            entity.Property(e => e.ConversationsId).HasColumnName("ConversationsId");
            entity.Property(e => e.MessageType)
                .HasDefaultValue(0)
                .HasComment("Enum MessageType: 0=Text, 1=Image, 2=File, 3=System, 4=FinalOffer, 5=ContractEvent, 6=MilestoneEvent, 7=PaymentEvent, 8=DisputeEvent");
            entity.Property(e => e.Metadata).HasColumnType("jsonb");
            entity.Property(e => e.ReplyToMessageId).HasColumnName("ReplyToMessageId");
            entity.Property(e => e.SenderUserId).HasColumnName("SenderUserId");
            entity.Property(e => e.SentAt).HasDefaultValueSql("now()");

            entity.Property(e => e.ScheduleId).HasColumnName("ScheduleId");

            entity.HasOne(d => d.Conversations).WithMany(p => p.Messages)
                .HasForeignKey(d => d.ConversationsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Messages_conv_ConversationsId_fkey");

            entity.HasOne(d => d.ReplyToMessage).WithMany(p => p.Replies)
                .HasForeignKey(d => d.ReplyToMessageId)
                .HasConstraintName("Messages_msg_ReplyToMessageId_fkey");

            entity.HasOne(d => d.SenderUser).WithMany(p => p.Messages)
                .HasForeignKey(d => d.SenderUserId)
                .HasConstraintName("Messages_usr_SenderUserId_fkey");

            entity.HasOne(d => d.Schedule).WithMany(p => p.Messages)
                .HasForeignKey(d => d.ScheduleId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("Messages_sch_ScheduleId_fkey");
        });

        modelBuilder.Entity<MessageAttachment>(entity =>
        {
            entity.HasKey(e => e.MessageAttachmentsId).HasName("MessageAttachments_pkey");

            entity.HasIndex(e => e.MessagesId, "IX_MessageAttachments_MessagesId");

            entity.Property(e => e.MessageAttachmentsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("MessageAttachmentsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.FileName).HasMaxLength(500);
            entity.Property(e => e.FileExtension).HasMaxLength(20);
            entity.Property(e => e.FileSizeBytes).HasColumnName("FileSizeBytes");
            entity.Property(e => e.MimeType).HasMaxLength(100);
            entity.Property(e => e.MessagesId).HasColumnName("MessagesId");
            entity.Property(e => e.StorageObjectKey).HasMaxLength(500);
            entity.Property(e => e.StorageProvider).HasMaxLength(100);

            entity.HasOne(d => d.Messages).WithMany(p => p.MessageAttachments)
                .HasForeignKey(d => d.MessagesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("MessageAttachments_msg_MessagesId_fkey");
        });

        modelBuilder.Entity<NegotiationOffer>(entity =>
        {
            entity.HasKey(e => e.NegotiationOfferId).HasName("NegotiationOffers_pkey");

            entity.HasIndex(e => e.ContractsId, "IX_NegotiationOffers_ContractsId");

            entity.HasIndex(e => new { e.ConversationsId, e.Status }, "IX_NegotiationOffers_ConversationsId_Status");

            entity.HasIndex(e => new { e.JobPostsId, e.Status }, "IX_NegotiationOffers_JobPostsId_Status");

            entity.HasIndex(e => new { e.ConversationsId, e.Status }, "UX_NegotiationOffers_PendingPerConversation")
                .IsUnique()
                .HasFilter("\"Status\" = 0");

            entity.HasIndex(e => new { e.JobPostsId, e.Status }, "UX_NegotiationOffers_AcceptedPerJobPost")
                .IsUnique()
                .HasFilter("\"Status\" = 1");

            entity.Property(e => e.NegotiationOfferId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("NegotiationOfferId");
            entity.Property(e => e.ClientProfilesId).HasColumnName("ClientProfilesId");
            entity.Property(e => e.ContractsId).HasColumnName("ContractsId");
            entity.Property(e => e.ConversationsId).HasColumnName("ConversationsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.FinalPrice).HasPrecision(18, 2);
            entity.Property(e => e.FreelancerProfilesId).HasColumnName("FreelancerProfilesId");
            entity.Property(e => e.JobPostsId).HasColumnName("JobPostsId");
            entity.Property(e => e.ProposalsId).HasColumnName("ProposalsId");
            entity.Property(e => e.Status)
                .HasComment("Enum NegotiationOfferStatus: 0=PendingFreelancerConfirmation, 1=Accepted, 2=Rejected, 3=ChangeRequested, 4=Expired, 5=Cancelled");

            entity.HasOne(d => d.ClientProfiles).WithMany()
                .HasForeignKey(d => d.ClientProfilesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("NegotiationOffers_clPro_ClientProfilesId_fkey");

            entity.HasOne(d => d.Contracts).WithMany(p => p.NegotiationOffers)
                .HasForeignKey(d => d.ContractsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("NegotiationOffers_cont_ContractsId_fkey");

            entity.HasOne(d => d.Conversations).WithMany(p => p.NegotiationOffers)
                .HasForeignKey(d => d.ConversationsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("NegotiationOffers_conv_ConversationsId_fkey");

            entity.HasOne(d => d.FreelancerProfiles).WithMany()
                .HasForeignKey(d => d.FreelancerProfilesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("NegotiationOffers_flPro_FreelancerProfilesId_fkey");

            entity.HasOne(d => d.JobPosts).WithMany(p => p.NegotiationOffers)
                .HasForeignKey(d => d.JobPostsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("NegotiationOffers_jp_JobPostsId_fkey");

            entity.HasOne(d => d.Proposals).WithMany(p => p.NegotiationOffers)
                .HasForeignKey(d => d.ProposalsId)
                .HasConstraintName("NegotiationOffers_propo_ProposalsId_fkey");
        });

        modelBuilder.Entity<Milestone>(entity =>
        {
            entity.HasKey(e => e.MilestonesId).HasName("Milestones_pkey");

            entity.HasIndex(e => e.ContractsId, "IX_Milestones_ContractsId");

            entity.HasIndex(e => new { e.ContractsId, e.SortOrder }, "IX_Milestones_ContractsId_SortOrder");

            entity.HasIndex(e => e.Status, "IX_Milestones_Status");

            entity.Property(e => e.MilestonesId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("MilestonesId");
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.ContractsId).HasColumnName("ContractsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.ReleasedAmount)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.Status).HasComment("Enum MilestoneStatus: 0=Pending, 1=InProgress, 2=Submitted, 3=Approved, 4=PaymentProofUploaded, 5=PaymentConfirmed, 6=Disputed");
            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.SubmissionDescription).HasMaxLength(5000);

            entity.HasOne(d => d.Contracts).WithMany(p => p.Milestones)
                .HasForeignKey(d => d.ContractsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Milestones_cont_ContractsId_fkey");
        });

        modelBuilder.Entity<MilestoneAttachment>(entity =>
        {
            entity.HasKey(e => e.MilestoneAttachmentsId).HasName("MilestoneAttachments_pkey");

            entity.HasIndex(e => e.MilestonesId, "IX_MilestoneAttachments_MilestonesId");

            entity.Property(e => e.MilestoneAttachmentsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("MilestoneAttachmentsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.FileName).HasMaxLength(500);
            entity.Property(e => e.MimeType).HasMaxLength(200);
            entity.Property(e => e.MilestonesId).HasColumnName("MilestonesId");
            entity.Property(e => e.SourceType)
                .HasDefaultValue(0)
                .HasComment("Enum MilestoneSubmissionSourceType: 0=File, 1=Link");

            entity.HasOne(d => d.Milestones).WithMany(p => p.MilestoneAttachments)
                .HasForeignKey(d => d.MilestonesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("MilestoneAttachments_mStone_MilestonesId_fkey");

            entity.HasOne(d => d.UploadedByUser).WithMany(p => p.MilestoneAttachments)
                .HasForeignKey(d => d.UploadedByUserId)
                .HasConstraintName("MilestoneAttachments_UploadedByUserId_fkey");
        });

        modelBuilder.Entity<BroadcastNotification>(entity =>
        {
            entity.HasKey(e => e.BroadcastNotificationId).HasName("BroadcastNotifications_pkey");

            entity.HasIndex(e => e.CreatedAt, "IX_BroadcastNotifications_CreatedAt").IsDescending(true);

            entity.HasIndex(e => e.CreatedByAdminId, "IX_BroadcastNotifications_CreatedByAdminId");

            entity.Property(e => e.BroadcastNotificationId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("BroadcastNotificationId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.ReferenceType).HasMaxLength(50);
            entity.Property(e => e.Title).HasMaxLength(300);
            entity.Property(e => e.Type).HasComment("Enum NotificationType");
            entity.Property(e => e.TargetScope).HasComment("Enum NotificationTarget");
            entity.Property(e => e.TargetRole).HasComment("Enum UserRole");

            entity.HasOne(d => d.CreatedByAdmin).WithMany()
                .HasForeignKey(d => d.CreatedByAdminId)
                .HasConstraintName("BroadcastNotifications_CreatedByAdminId_fkey");
        });

        modelBuilder.Entity<BroadcastNotificationRecipient>(entity =>
        {
            entity.HasKey(e => e.BroadcastNotificationRecipientId).HasName("BroadcastNotificationRecipients_pkey");

            entity.HasIndex(e => new { e.BroadcastNotificationId, e.UserId }, "IX_BroadcastRecipients_BroadcastNotificationId_UserId")
                .IsUnique();

            entity.HasIndex(e => new { e.UserId, e.IsRead }, "IX_BroadcastRecipients_UserId_IsRead");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_BroadcastRecipients_UserId_CreatedAt").IsDescending(false, true);

            entity.Property(e => e.BroadcastNotificationRecipientId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("BroadcastNotificationRecipientId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsRead).HasDefaultValue(false);

            entity.HasOne(d => d.BroadcastNotification).WithMany(p => p.Recipients)
                .HasForeignKey(d => d.BroadcastNotificationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("BroadcastRecipients_BroadcastNotificationId_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.BroadcastNotificationRecipients)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("BroadcastRecipients_UserId_fkey");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationsId).HasName("Notifications_pkey");

            entity.HasIndex(e => new { e.ReferenceId, e.ReferenceType }, "IX_Notifications_ReferenceId_ReferenceType");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_Notifications_UserId_CreatedAt").IsDescending(false, true);

            entity.HasIndex(e => new { e.UserId, e.IsRead }, "IX_Notifications_UserId_IsRead");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_Notifications_Unread_UserId_CreatedAt")
                .IsDescending(false, true)
                .HasFilter("\"IsRead\" IS NOT TRUE");

            entity.HasIndex(e => new { e.UserId, e.ReferenceId }, "UX_Notifications_UnreadSchedule_User_Reference")
                .IsUnique()
                .HasFilter("\"Type\" = 13 AND \"ReferenceId\" IS NOT NULL AND \"IsRead\" IS NOT TRUE");

            entity.Property(e => e.NotificationsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("NotificationsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsRead).HasDefaultValue(false);

            entity.Property(e => e.Metadata).HasColumnType("jsonb");
            entity.Property(e => e.ReferenceType).HasMaxLength(50);
            entity.Property(e => e.Title).HasMaxLength(300);
            entity.Property(e => e.Type).HasComment("Enum NotificationType: 0=NewJob, 1=ProposalReceived, 2=ProposalStatusChanged, 3=ContractStarted, 4=MilestoneUpdated, 5=PaymentProofUploaded, 6=PaymentConfirmed, 7=ChatMessage, 8=DisputeUpdate, 9=ReviewReceived, 10=SystemAlert, 11=AIInterviewInvite, 12=SubscriptionExpiring, 13=Schedule, 14=SubscriptionActivated, 15=SubscriptionCancelled, 16=PromotionActivated, 17=PromotionExpired, 18=RankProtectionActivated, 19=RankProtectionExpired, 20=ReportUpdate");
            entity.Property(e => e.UserId).HasColumnName("UserId");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Notifications_usr_UserId_fkey");
        });

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.HasKey(e => e.ScheduleId);
            entity.HasIndex(e => new { e.ConversationId, e.ScheduledAtUtc });
            entity.HasIndex(e => e.ConversationId, "UX_Schedules_ConversationId_Scheduled")
                .IsUnique()
                .HasFilter("\"Status\" = 0");
            entity.HasIndex(e => new { e.Status, e.ScheduledAtUtc });
            entity.Property(e => e.ScheduleId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Details).HasMaxLength(4000);
            entity.Property(e => e.TimeZoneId).HasMaxLength(64).HasDefaultValue("Asia/Ho_Chi_Minh");
            entity.Property(e => e.CancellationReason).HasMaxLength(1000);
            entity.Property(e => e.Status).HasDefaultValue(ScheduleStatus.Scheduled);
            entity.Property(e => e.AgreementStatus).HasDefaultValue(ScheduleAgreementStatus.Accepted);
            entity.Property(e => e.EditCount).HasDefaultValue(0);
            entity.Property(e => e.RescheduleRequestCount).HasDefaultValue(0);
            entity.Property(e => e.RescheduleRejectionCount).HasDefaultValue(0);
            entity.Property(e => e.ProposedTimeZoneId).HasMaxLength(64);
            entity.Property(e => e.Version).HasDefaultValue(1).IsConcurrencyToken();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            // Meeting fields
            entity.Property(e => e.MeetingProvider).HasDefaultValue(ScheduleMeetingProvider.None);
            entity.Property(e => e.MeetingStatus).HasDefaultValue(MeetingProvisioningStatus.None);
            entity.Property(e => e.MeetingAttempt).HasDefaultValue(0);
            entity.Property(e => e.MeetingSpaceName).HasMaxLength(255);
            entity.Property(e => e.MeetingJoinUri).HasMaxLength(500);
            entity.Property(e => e.MeetingFailureCode).HasMaxLength(100);

            entity.HasOne(e => e.Conversation).WithMany(e => e.Schedules)
                .HasForeignKey(e => e.ConversationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.CreatedByUser).WithMany(e => e.CreatedSchedules)
                .HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CancelledByUser).WithMany(e => e.CancelledSchedules)
                .HasForeignKey(e => e.CancelledByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DeliveryOutbox>(entity =>
        {
            entity.HasKey(e => e.DeliveryOutboxId);
            entity.HasIndex(e => e.DeliveryKey).IsUnique();
            entity.HasIndex(e => new { e.Status, e.NextAttemptAt });
            entity.Property(e => e.DeliveryOutboxId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.DeliveryKey).HasMaxLength(250).IsRequired();
            entity.Property(e => e.Payload).HasColumnType("jsonb");
            entity.Property(e => e.LastError).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne<Schedule>().WithMany()
                .HasForeignKey(e => e.ScheduleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany()
                .HasForeignKey(e => e.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PayoutOutbox>(entity =>
        {
            entity.HasKey(e => e.PayoutOutboxId).HasName("PayoutOutboxes_pkey");

            entity.HasIndex(e => e.PayoutKey, "IX_PayoutOutboxes_PayoutKey").IsUnique();
            entity.HasIndex(e => new { e.Status, e.NextAttemptAt }, "IX_PayoutOutboxes_Status_NextAttemptAt");
            entity.HasIndex(e => e.WalletWithdrawalId, "IX_PayoutOutboxes_WalletWithdrawalId");

            entity.Property(e => e.PayoutOutboxId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.PayoutKey).HasMaxLength(250);
            entity.Property(e => e.Status)
                .HasComment("Enum PayoutOutboxStatus: 0=Pending, 1=Processing, 2=Delivered, 3=DeadLettered, 4=Cancelled");
            entity.Property(e => e.LastError).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(e => e.WalletWithdrawal).WithMany()
                .HasForeignKey(e => e.WalletWithdrawalId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("PayoutOutboxes_wwd_WalletWithdrawalId_fkey");
        });

        modelBuilder.Entity<GoogleMeetConnection>(entity =>
        {
            entity.HasKey(e => e.GoogleMeetConnectionId);
            entity.HasIndex(e => new { e.UserId, e.ConnectedAt }).IsDescending(false, true);
            entity.HasIndex(e => new { e.UserId, e.DisconnectedAt })
                .HasFilter("\"DisconnectedAt\" IS NULL")
                .IsUnique();

            entity.Property(e => e.GoogleMeetConnectionId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.GoogleSubject).HasMaxLength(255).IsRequired();
            entity.Property(e => e.GoogleEmail).HasMaxLength(320).IsRequired();
            entity.Property(e => e.GrantedScopes).HasColumnType("text");
            entity.Property(e => e.EncryptedRefreshToken).HasColumnType("text");
            entity.Property(e => e.Status).HasDefaultValue(GoogleMeetConnectionStatus.Active);
            entity.Property(e => e.LastFailureCode).HasMaxLength(100);
            entity.Property(e => e.Version).HasDefaultValue(1).IsConcurrencyToken();
            entity.Property(e => e.ConnectedAt).HasDefaultValueSql("now()");

            entity.HasOne(e => e.User).WithMany(e => e.GoogleMeetConnections)
                .HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GoogleMeetOAuthState>(entity =>
        {
            entity.HasKey(e => e.GoogleMeetOAuthStateId);
            entity.HasIndex(e => e.StateHash).IsUnique();
            entity.HasIndex(e => e.FlowId).IsUnique();

            entity.Property(e => e.GoogleMeetOAuthStateId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.StateHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.NonceHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ProtectedCodeVerifier).HasColumnType("text");
            entity.Property(e => e.FrontendReturnPath).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(e => e.User).WithMany(e => e.GoogleMeetOAuthStates)
                .HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GoogleMeetProvisioningJob>(entity =>
        {
            entity.HasKey(e => e.GoogleMeetProvisioningJobId);
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
            entity.HasIndex(e => new { e.ScheduleId, e.Attempt }).IsUnique();
            entity.HasIndex(e => new { e.ScheduleId, e.Status })
                .HasFilter("\"Status\" IN (0, 1)")
                .IsUnique();

            entity.Property(e => e.GoogleMeetProvisioningJobId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.FailureCode).HasMaxLength(100);
            entity.Property(e => e.ReturnedSpaceName).HasMaxLength(255);
            entity.Property(e => e.ReturnedJoinUri).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(e => e.Schedule).WithMany(e => e.MeetProvisioningJobs)
                .HasForeignKey(e => e.ScheduleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.OrganizerUser).WithMany()
                .HasForeignKey(e => e.OrganizerUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlatformSetting>(entity =>
        {
            entity.HasKey(e => e.PlatformSettingsId).HasName("PlatformSettings_pkey");

            entity.HasIndex(e => e.Key, "IX_PlatformSettings_Key").IsUnique();

            entity.Property(e => e.PlatformSettingsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("PlatformSettingsId");
            entity.Property(e => e.DataType)
                .HasMaxLength(50)
                .HasDefaultValueSql("'string'::character varying");
            entity.Property(e => e.Key).HasMaxLength(200);

            entity.HasOne(d => d.UpdatedByAdmin).WithMany(p => p.PlatformSettings)
                .HasForeignKey(d => d.UpdatedByAdminId)
                .HasConstraintName("PlatformSettings_UpdatedByAdminId_fkey");
        });

        modelBuilder.Entity<FreelancerRankProtection>(entity =>
        {
            entity.HasKey(e => e.FreelancerRankProtectionsId)
                .HasName("FreelancerRankProtections_pkey");
            entity.HasIndex(e => new { e.FreelancerProfileId, e.IsVacationModeEnabled },
                "IX_FreelancerRankProtections_Profile_Active");
            entity.HasIndex(e => e.RankProtectionEndsAt,
                "IX_FreelancerRankProtections_End");
            entity.Property(e => e.FreelancerRankProtectionsId)
                .HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.RankProtectionReason).HasMaxLength(500);
            entity.HasOne(e => e.FreelancerProfile)
                .WithMany(e => e.RankProtections)
                .HasForeignKey(e => e.FreelancerProfileId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FreelancerRankProtections_FreelancerProfileId_fkey");
        });

        modelBuilder.Entity<FreelancerProfilePromotion>(entity =>
        {
            entity.HasKey(e => e.FreelancerProfilePromotionsId)
                .HasName("FreelancerProfilePromotions_pkey");
            entity.Property(e => e.FreelancerProfilePromotionsId)
                .HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.PackageId).HasMaxLength(100);
            entity.Property(e => e.PackageName).HasMaxLength(200);
            entity.Property(e => e.PurchaseIdempotencyKey).HasMaxLength(200);
            entity.HasIndex(e => new { e.FreelancerProfileId, e.PurchaseIdempotencyKey }).IsUnique();
            entity.Property(e => e.PhotoUrl).HasMaxLength(2048);
            entity.Property(e => e.DisplayName).HasMaxLength(120);
            entity.Property(e => e.Quote).HasMaxLength(240);
            entity.Property(e => e.JobTitle).HasMaxLength(160);
            entity.Property(e => e.TokenCost).HasPrecision(18, 4);
            entity.Property(e => e.BoostWeight).HasPrecision(10, 4);
            entity.Property(e => e.QueuePosition).HasDefaultValue(0);
            entity.HasIndex(e => new { e.FreelancerProfileId, e.Status, e.StartTime },
                "IX_FreelancerProfilePromotions_Queue");
            entity.HasIndex(e => new { e.Status, e.QueuePosition },
                "IX_FreelancerProfilePromotions_Position")
                .IsUnique()
                .HasFilter("\"QueuePosition\" > 0");
            entity.HasIndex(e => e.EndTime, "IX_FreelancerProfilePromotions_End");
            entity.HasIndex(e => e.WalletTransactionId).IsUnique();
            entity.HasIndex(e => e.FreelancerProfileId,
                    "UX_FreelancerProfilePromotions_OneActive")
                .IsUnique()
                .HasFilter("\"Status\" = 1");
            entity.HasOne(e => e.FreelancerProfile)
                .WithMany(e => e.Promotions)
                .HasForeignKey(e => e.FreelancerProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.WalletTransaction)
                .WithOne()
                .HasForeignKey<FreelancerProfilePromotion>(e => e.WalletTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PortfolioItem>(entity =>
        {
            entity.HasKey(e => e.PortfolioItemsId).HasName("PortfolioItems_pkey");

            entity.HasIndex(e => e.FreelancerId, "IX_PortfolioItems_FreelancerId");

            entity.Property(e => e.PortfolioItemsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("PortfolioItemsId");
            entity.Property(e => e.FreelancerId).HasColumnName("FreelancerId");

            entity.HasOne(d => d.Freelancer).WithMany(p => p.PortfolioItems)
                .HasForeignKey(d => d.FreelancerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("PortfolioItems_fl_FreelancerId_fkey");
        });

        modelBuilder.Entity<Proposal>(entity =>
        {
            entity.HasKey(e => e.ProposalsId).HasName("Proposals_pkey");

            entity.HasIndex(e => e.FreelancerProfilesId, "IX_Proposals_FreelancerProfilesId");

            entity.HasIndex(e => new { e.FreelancerProfilesId, e.Status }, "IX_Proposals_FreelancerProfilesId_Status");

            entity.HasIndex(e => e.JobPostsId, "IX_Proposals_JobPostsId");

            entity.HasIndex(e => new { e.JobPostsId, e.Status }, "IX_Proposals_JobPostsId_Status");

            entity.HasIndex(e => e.Status, "IX_Proposals_Status");

            entity.HasIndex(e => new { e.JobPostsId, e.FreelancerProfilesId }, "Proposals_jp_JobPostsId_flPro_FreelancerProfilesId_key").IsUnique();

            entity.Property(e => e.ProposalsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ProposalsId");
            entity.Property(e => e.FreelancerProfilesId).HasColumnName("FreelancerProfilesId");
            entity.Property(e => e.IsAigenerated)
                .HasDefaultValue(false)
                .HasColumnName("IsAIGenerated");
            entity.Property(e => e.JobPostsId).HasColumnName("JobPostsId");
            entity.Property(e => e.ProposedDuration).HasMaxLength(100);
            entity.Property(e => e.ProposedBudget).HasPrecision(18, 2);
            entity.Property(e => e.AnalysisSummary).HasColumnType("text");
            entity.Property(e => e.SolutionApproach).HasColumnType("text");
            entity.Property(e => e.Deliverables).HasColumnType("text");
            entity.Property(e => e.Assumptions).HasColumnType("text");
            entity.Property(e => e.OutOfScope).HasColumnType("text");
            entity.Property(e => e.Status).HasComment("Enum ProposalStatus: 0=Pending, 1=Shortlisted, 2=Accepted, 3=Rejected, 4=Withdrawn");

            entity.HasOne(d => d.FreelancerProfiles).WithMany(p => p.Proposals)
                .HasForeignKey(d => d.FreelancerProfilesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Proposals_flPro_FreelancerProfilesId_fkey");

            entity.HasOne(d => d.JobPosts).WithMany(p => p.Proposals)
                .HasForeignKey(d => d.JobPostsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Proposals_jp_JobPostsId_fkey");
        });

        modelBuilder.Entity<ProposalWorkBreakdownItem>(entity =>
        {
            entity.HasKey(e => e.ProposalWorkBreakdownItemsId).HasName("ProposalWorkBreakdownItems_pkey");
            entity.HasIndex(e => new { e.ProposalsId, e.OrderIndex }, "IX_ProposalWorkBreakdownItems_Proposal_Order");
            entity.Property(e => e.ProposalWorkBreakdownItemsId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.EstimatedDuration).HasMaxLength(100);
            entity.HasOne(e => e.Proposals).WithMany(e => e.ProposalWorkBreakdownItems)
                .HasForeignKey(e => e.ProposalsId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("ProposalWorkBreakdownItems_ProposalsId_fkey");
            entity.HasOne(e => e.ProposalMilestonePlan).WithMany(e => e.WorkItems)
                .HasForeignKey(e => e.ProposalMilestonePlansId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProposalMilestonePlan>(entity =>
        {
            entity.HasKey(e => e.ProposalMilestonePlansId).HasName("ProposalMilestonePlans_pkey");
            entity.HasIndex(e => new { e.ProposalsId, e.OrderIndex }, "IX_ProposalMilestonePlans_Proposal_Order");
            entity.Property(e => e.ProposalMilestonePlansId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.EstimatedDuration).HasMaxLength(100);
            entity.Property(e => e.EstimatedDuration).HasMaxLength(100);
            entity.Property(e => e.DueDate).HasColumnType("date");
            entity.HasOne(e => e.Proposals).WithMany(e => e.ProposalMilestonePlans)
                .HasForeignKey(e => e.ProposalsId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("ProposalMilestonePlans_ProposalsId_fkey");
        });

        modelBuilder.Entity<ProposalAiJudging>(entity =>
        {
            entity.HasKey(e => e.ProposalAiJudgingsId).HasName("ProposalAiJudgings_pkey");
            entity.HasIndex(e => e.ProposalId, "IX_ProposalAiJudgings_ProposalId").IsUnique();
            entity.Property(e => e.ProposalAiJudgingsId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.EvaluatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.TechnicalSkillsJson).HasColumnType("text");
            entity.Property(e => e.SoftSkillsJson).HasColumnType("text");
            entity.Property(e => e.GradedQuestionsJson).HasColumnType("text");
            entity.HasOne(e => e.Proposal).WithOne(e => e.ProposalAiJudging)
                .HasForeignKey<ProposalAiJudging>(e => e.ProposalId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("ProposalAiJudgings_ProposalId_fkey");
        });

        modelBuilder.Entity<NegotiationMilestoneDraft>(entity =>
        {
            entity.HasKey(e => e.NegotiationMilestoneDraftId).HasName("NegotiationMilestoneDrafts_pkey");
            entity.HasIndex(e => new { e.ConversationsId, e.OrderIndex }, "IX_NegotiationMilestoneDrafts_Conversation_Order").IsUnique();
            entity.Property(e => e.NegotiationMilestoneDraftId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.EstimatedDuration).HasMaxLength(100);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(e => e.Conversations).WithMany(e => e.NegotiationMilestoneDrafts)
                .HasForeignKey(e => e.ConversationsId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("NegotiationMilestoneDrafts_ConversationsId_fkey");
        });

        modelBuilder.Entity<MilestoneEarlyStartRequest>(entity =>
        {
            entity.HasKey(e => e.MilestoneEarlyStartRequestId);
            entity.HasIndex(e => new { e.MilestonesId, e.Status })
                .IsUnique()
                .HasFilter("\"Status\" = 0");
            entity.Property(e => e.MilestoneEarlyStartRequestId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Reason).HasMaxLength(2000);
            entity.Property(e => e.ResponseNote).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(e => e.Contract).WithMany()
                .HasForeignKey(e => e.ContractsId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Milestone).WithMany()
                .HasForeignKey(e => e.MilestonesId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NegotiationMilestoneDraftWorkItem>(entity =>
        {
            entity.HasKey(e => e.NegotiationMilestoneDraftWorkItemId);
            entity.HasIndex(e => new { e.NegotiationMilestoneDraftId, e.OrderIndex }).IsUnique();
            entity.Property(e => e.NegotiationMilestoneDraftWorkItemId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.EstimatedDuration).HasMaxLength(100);
            entity.HasOne(e => e.MilestoneDraft).WithMany(e => e.WorkItems)
                .HasForeignKey(e => e.NegotiationMilestoneDraftId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NegotiationOfferMilestone>(entity =>
        {
            entity.HasKey(e => e.NegotiationOfferMilestoneId).HasName("NegotiationOfferMilestones_pkey");
            entity.HasIndex(e => new { e.NegotiationOfferId, e.OrderIndex }, "IX_NegotiationOfferMilestones_Offer_Order").IsUnique();
            entity.Property(e => e.NegotiationOfferMilestoneId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.EstimatedDuration).HasMaxLength(100);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.HasOne(e => e.NegotiationOffer).WithMany(e => e.NegotiationOfferMilestones)
                .HasForeignKey(e => e.NegotiationOfferId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("NegotiationOfferMilestones_NegotiationOfferId_fkey");
        });

        modelBuilder.Entity<NegotiationOfferWorkItem>(entity =>
        {
            entity.HasKey(e => e.NegotiationOfferWorkItemId);
            entity.HasIndex(e => new { e.NegotiationOfferMilestoneId, e.OrderIndex }).IsUnique();
            entity.Property(e => e.NegotiationOfferWorkItemId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.EstimatedDuration).HasMaxLength(100);
            entity.HasOne(e => e.Milestone).WithMany(e => e.WorkItems)
                .HasForeignKey(e => e.NegotiationOfferMilestoneId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProposalInterviewReviewSession>(entity =>
        {
            entity.HasKey(e => e.ProposalInterviewReviewSessionsId).HasName("ProposalInterviewReviewSessions_pkey");

            entity.HasIndex(e => e.ProposalsId, "IX_ProposalInterviewReviewSessions_ProposalsId")
                .IsUnique();

            entity.HasIndex(e => new { e.FreelancerUserId, e.CreatedAt }, "IX_ProposalInterviewReviewSessions_FreelancerUserId_CreatedAt")
                .IsDescending(false, true);

            entity.Property(e => e.ProposalInterviewReviewSessionsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ProposalInterviewReviewSessionsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsLocked).HasDefaultValue(false);
            entity.Property(e => e.ReviewableQuestionCount).HasDefaultValue(0);

            entity.HasOne(d => d.Proposals).WithOne(p => p.ProposalInterviewReviewSession)
                .HasForeignKey<ProposalInterviewReviewSession>(d => d.ProposalsId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("ProposalInterviewReviewSessions_propo_ProposalsId_fkey");

            entity.HasOne(d => d.FreelancerUser).WithMany(p => p.ProposalInterviewReviewSessions)
                .HasForeignKey(d => d.FreelancerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ProposalInterviewReviewSessions_usr_FreelancerUserId_fkey");
        });

        modelBuilder.Entity<ProposalQuestionTimer>(entity =>
        {
            entity.HasKey(e => e.ProposalQuestionTimersId).HasName("ProposalQuestionTimers_pkey");

            entity.HasIndex(e => new { e.ProposalsId, e.JobPostQuestionsId }, "IX_ProposalQuestionTimers_ProposalsId_JobPostQuestionsId")
                .IsUnique();

            entity.HasIndex(e => new { e.FreelancerUserId, e.CreatedAt }, "IX_ProposalQuestionTimers_FreelancerUserId_CreatedAt")
                .IsDescending(false, true);

            entity.Property(e => e.ProposalQuestionTimersId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ProposalQuestionTimersId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsLocked).HasDefaultValue(false);
            entity.Property(e => e.LockedReason).HasComment("Enum QuestionTimerLockedReason: 0=Completed, 1=Timeout");

            entity.HasOne(d => d.Proposals).WithMany(p => p.ProposalQuestionTimers)
                .HasForeignKey(d => d.ProposalsId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("ProposalQuestionTimers_propo_ProposalsId_fkey");

            entity.HasOne(d => d.JobPostQuestions).WithMany(p => p.ProposalQuestionTimers)
                .HasForeignKey(d => d.JobPostQuestionsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ProposalQuestionTimers_jpq_JobPostQuestionsId_fkey");

            entity.HasOne(d => d.FreelancerUser).WithMany(p => p.ProposalQuestionTimers)
                .HasForeignKey(d => d.FreelancerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ProposalQuestionTimers_usr_FreelancerUserId_fkey");
        });

        modelBuilder.Entity<ProposalAnswer>(entity =>
        {
            entity.HasKey(e => e.ProposalAnswersId).HasName("ProposalAnswers_pkey");

            entity.HasIndex(e => e.ProposalsId, "IX_ProposalAnswers_ProposalsId");

            entity.HasIndex(e => e.JobPostQuestionsId, "IX_ProposalAnswers_JobPostQuestionsId");

            entity.HasIndex(e => new { e.ProposalsId, e.JobPostQuestionsId }, "ProposalAnswers_propo_ProposalsId_jpq_JobPostQuestionsId_key")
                .IsUnique();

            entity.Property(e => e.ProposalAnswersId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ProposalAnswersId");

            entity.Property(e => e.ProposalsId)
                .HasColumnName("ProposalsId");

            entity.Property(e => e.JobPostQuestionsId)
                .HasColumnName("JobPostQuestionsId");

            entity.Property(e => e.AnswerText)
                .IsRequired()
                .HasMaxLength(4000);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()");

            entity.HasOne(d => d.Proposals)
                .WithMany(p => p.ProposalAnswers)
                .HasForeignKey(d => d.ProposalsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ProposalAnswers_propo_ProposalsId_fkey");

            entity.HasOne(d => d.JobPostQuestions)
                .WithMany(p => p.ProposalAnswers)
                .HasForeignKey(d => d.JobPostQuestionsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ProposalAnswers_jpq_JobPostQuestionsId_fkey");
        });

        modelBuilder.Entity<Report>(entity =>
        {
            entity.HasKey(e => e.ReportsId).HasName("Reports_pkey");

            entity.HasIndex(e => new { e.ReportedEntityId, e.ReportedEntityType }, "IX_Reports_ReportedEntityId_ReportedEntityType");

            entity.HasIndex(e => e.ReporterId, "IX_Reports_ReporterId");

            entity.HasIndex(e => e.ResolvedByAdminId, "IX_Reports_ResolvedByAdminId");

            entity.HasIndex(e => e.Status, "IX_Reports_Status");

            entity.Property(e => e.ReportsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ReportsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.ReportedEntityType).HasMaxLength(50);
            entity.Property(e => e.Status)
                .HasDefaultValue(0)
                .HasComment("Enum ReportStatus: 0=Pending, 1=Reviewing, 2=Resolved, 3=Dismissed");
            entity.Property(e => e.Type).HasComment("Enum ReportType: 0=Spam, 1=Fraud, 2=InappropriateContent, 3=HarassmentOrAbuse, 4=Other, 5=PaymentDispute");
            entity.Property(e => e.ReporterId).HasColumnName("ReporterId");

            entity.HasOne(d => d.ResolvedByAdmin).WithMany(p => p.ReportResolvedByAdmins)
                .HasForeignKey(d => d.ResolvedByAdminId)
                .HasConstraintName("Reports_ResolvedByAdminId_fkey");

            entity.HasOne(d => d.Reporter).WithMany(p => p.ReportReporters)
                .HasForeignKey(d => d.ReporterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Reports_usr_ReporterId_fkey");
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.ReviewsId).HasName("Reviews_pkey");

            entity.HasIndex(e => e.ContractsId, "IX_Reviews_ContractsId");

            entity.HasIndex(e => e.RevieweeId, "IX_Reviews_RevieweeId");

            entity.HasIndex(e => new { e.RevieweeId, e.IsVisible }, "IX_Reviews_RevieweeId_IsVisible");

            entity.HasIndex(e => e.ReviewerId, "IX_Reviews_ReviewerId");

            entity.HasIndex(e => e.ModerationStatus, "IX_Reviews_ModerationStatus");

            entity.HasIndex(e => new { e.ContractsId, e.ReviewerId }, "Reviews_cont_ContractsId_usr_ReviewerId_key").IsUnique();

            entity.Property(e => e.ReviewsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ReviewsId");
            entity.Property(e => e.ContractsId).HasColumnName("ContractsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsVisible).HasDefaultValue(true);
            entity.Property(e => e.ModerationStatus).HasDefaultValue(0);
            entity.Property(e => e.ModerationNote).HasMaxLength(1000);
            entity.Property(e => e.RevieweeId).HasColumnName("RevieweeId");
            entity.Property(e => e.ReviewerId).HasColumnName("ReviewerId");

            entity.HasOne(d => d.Contracts).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.ContractsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Reviews_cont_ContractsId_fkey");

            entity.HasOne(d => d.Reviewee).WithMany(p => p.ReviewReviewees)
                .HasForeignKey(d => d.RevieweeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Reviews_usr_RevieweeId_fkey");

            entity.HasOne(d => d.Reviewer).WithMany(p => p.ReviewReviewers)
                .HasForeignKey(d => d.ReviewerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Reviews_usr_ReviewerId_fkey");

            entity.HasOne<User>().WithMany()
                .HasForeignKey(d => d.ModeratedByAdminId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("Reviews_usr_ModeratedByAdminId_fkey");
        });

        modelBuilder.Entity<SavedFreelancer>(entity =>
        {
            entity.HasKey(e => e.SavedFreelancersId).HasName("SavedFreelancers_pkey");

            entity.HasIndex(e => e.FreelancerProfilesId, "IX_SavedFreelancers_FreelancerProfilesId");

            entity.HasIndex(e => e.UserId, "IX_SavedFreelancers_UserId");

            entity.HasIndex(e => new { e.UserId, e.FreelancerProfilesId }, "SavedFreelancers_usr_UserId_flPro_FreelancerProfilesId_key").IsUnique();

            entity.Property(e => e.SavedFreelancersId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("SavedFreelancersId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.FreelancerProfilesId).HasColumnName("FreelancerProfilesId");
            entity.Property(e => e.UserId).HasColumnName("UserId");

            entity.HasOne(d => d.FreelancerProfiles).WithMany(p => p.SavedFreelancers)
                .HasForeignKey(d => d.FreelancerProfilesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("SavedFreelancers_flPro_FreelancerProfilesId_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.SavedFreelancers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("SavedFreelancers_usr_UserId_fkey");
        });

        modelBuilder.Entity<SavedJob>(entity =>
        {
            entity.HasKey(e => e.SavedJobsId).HasName("SavedJobs_pkey");

            entity.HasIndex(e => e.JobPostsId, "IX_SavedJobs_JobPostsId");

            entity.HasIndex(e => e.UserId, "IX_SavedJobs_UserId");

            entity.HasIndex(e => new { e.UserId, e.JobPostsId }, "SavedJobs_usr_UserId_jp_JobPostsId_key").IsUnique();

            entity.Property(e => e.SavedJobsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("SavedJobsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.JobPostsId).HasColumnName("JobPostsId");
            entity.Property(e => e.UserId).HasColumnName("UserId");

            entity.HasOne(d => d.JobPosts).WithMany(p => p.SavedJobs)
                .HasForeignKey(d => d.JobPostsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("SavedJobs_jp_JobPostsId_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.SavedJobs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("SavedJobs_usr_UserId_fkey");
        });

        modelBuilder.Entity<Skill>(entity =>
        {
            entity.HasKey(e => e.SkillsId).HasName("Skills_pkey");

            entity.HasIndex(e => e.IsActive, "IX_Skills_IsActive");

            entity.HasIndex(e => e.Name, "IX_Skills_Name");

            entity.Property(e => e.SkillsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("SkillsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.SubscriptionsId).HasName("Subscriptions_pkey");

            entity.HasIndex(e => e.EndDate, "IX_Subscriptions_EndDate");

            entity.HasIndex(e => e.SubscriptionPlansId, "IX_Subscriptions_SubscriptionPlansId");

            entity.HasIndex(e => new { e.UserId, e.Status }, "IX_Subscriptions_UserId_Status");

            entity.Property(e => e.SubscriptionsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("SubscriptionsId");
            entity.Property(e => e.AutoRenew).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.PaymentReference).HasMaxLength(200);
            entity.Property(e => e.Status).HasComment("Enum SubscriptionStatus: 0=Active, 1=Expired, 2=Cancelled");
            entity.Property(e => e.SubscriptionPlansId).HasColumnName("SubscriptionPlansId");
            entity.Property(e => e.UserId).HasColumnName("UserId");

            entity.HasOne(d => d.SubscriptionPlans).WithMany(p => p.Subscriptions)
                .HasForeignKey(d => d.SubscriptionPlansId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Subscriptions_subPlan_SubscriptionPlansId_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Subscriptions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Subscriptions_usr_UserId_fkey");
        });

        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.HasKey(e => e.SubscriptionPlansId).HasName("SubscriptionPlans_pkey");

            entity.HasIndex(e => e.IsActive, "IX_SubscriptionPlans_IsActive");

            entity.HasIndex(e => e.TargetRole, "IX_SubscriptionPlans_TargetRole");

            entity.Property(e => e.SubscriptionPlansId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("SubscriptionPlansId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Currency)
                .HasDefaultValueSql("'VND'::character varying");
            entity.Property(e => e.Features).HasColumnType("jsonb");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.TargetRole).HasComment("Enum UserRole: 0=Client, 1=Freelancer, NULL=Both");
        });

        modelBuilder.Entity<UserEloPointTransaction>(entity =>
        {
            entity.HasKey(e => e.UserEloPointTransactionsId).HasName("UserEloPointTransactions_pkey");

            entity.HasIndex(e => e.IdempotencyKey, "IX_UserEloPointTransactions_IdempotencyKey").IsUnique();

            entity.HasIndex(e => new { e.SourceEntityType, e.SourceEntityId }, "IX_UserEloPointTransactions_SourceEntity");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_UserEloPointTransactions_UserId_CreatedAt").IsDescending(false, true);

            entity.Property(e => e.UserEloPointTransactionsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("UserEloPointTransactionsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IdempotencyKey).HasMaxLength(200);
            entity.Property(e => e.Metadata).HasColumnType("jsonb");
            entity.Property(e => e.PointsAfter).HasDefaultValue(0);
            entity.Property(e => e.Reason).HasComment("Enum UserEloPointReason: 0=InitialGrant, 1=InactivityPenalty, 2=ReturnBonus, 3=JobCompletion, 4=ReviewRating, 5=LegacyIntegrityPenalty, 6=ReviewModeration");
            entity.Property(e => e.SourceEntityType).HasMaxLength(50);
            entity.Property(e => e.UserId).HasColumnName("UserId");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_UserEloPointTransactions_PointsAfter_NonNegative", "\"PointsAfter\" >= 0");
            });

            entity.HasOne(d => d.User).WithMany(p => p.UserEloPointTransactions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("UserEloPointTransactions_usr_UserId_fkey");
        });

        modelBuilder.Entity<UserEloScore>(entity =>
        {
            entity.HasKey(e => e.UserEloScoresId).HasName("UserEloScores_pkey");

            entity.HasIndex(e => e.CurrentPoints, "IX_UserEloScores_CurrentPoints").IsDescending();

            entity.HasIndex(e => e.UserId, "IX_UserEloScores_UserId").IsUnique();

            entity.Property(e => e.UserEloScoresId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("UserEloScoresId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CurrentPoints).HasDefaultValue(100);
            entity.Property(e => e.LastActivityAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UserId).HasColumnName("UserId");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_UserEloScores_CurrentPoints_NonNegative", "\"CurrentPoints\" >= 0");
            });

            entity.HasOne(d => d.User).WithOne(p => p.UserEloScore)
                .HasForeignKey<UserEloScore>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("UserEloScores_usr_UserId_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("Users_pkey");

            entity.HasIndex(e => e.IsActive, "IX_Users_IsActive");

            entity.HasIndex(e => e.Role, "IX_Users_Role");

            entity.HasIndex(e => e.Email, "IX_Users_Email").IsUnique();

            entity.Property(e => e.UserId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("UserId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.FullName).HasMaxLength(200);
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnType("citext");
            entity.Property(e => e.Password).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsEmailVerified).HasDefaultValue(false);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.SuspensionReason).HasMaxLength(500);
            entity.Property(e => e.PreferredLanguage)
                .HasMaxLength(5)
                .HasDefaultValueSql("'vi'::character varying");
            entity.Property(e => e.Role).HasComment("Enum UserRole: 0=Client, 1=Freelancer, 2=Admin");
        });

        modelBuilder.Entity<UserWallet>(entity =>
        {
            entity.HasKey(e => e.UserWalletsId).HasName("UserWallets_pkey");

            entity.HasIndex(e => e.UserId, "IX_UserWallets_UserId").IsUnique();

            entity.Property(e => e.UserWalletsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("UserWalletsId");
            entity.Property(e => e.Version)
                .HasDefaultValue(1)
                .IsConcurrencyToken();
            entity.Property(e => e.AvailableTokens)
                .HasPrecision(18, 4)
                .HasDefaultValue(0m);
            entity.Property(e => e.WithdrawableTokens)
                .HasPrecision(18, 4)
                .HasDefaultValue(0m);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.HeldTokens)
                .HasPrecision(18, 4)
                .HasDefaultValue(0m);
            entity.Property(e => e.PendingWithdrawalTokens)
                .HasPrecision(18, 4)
                .HasDefaultValue(0m);
            entity.Property(e => e.UserId).HasColumnName("UserId");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_UserWallets_AvailableTokens_NonNegative", "\"AvailableTokens\" >= 0");
                t.HasCheckConstraint("CK_UserWallets_WithdrawableTokens_NonNegative", "\"WithdrawableTokens\" >= 0");
                t.HasCheckConstraint("CK_UserWallets_WithdrawableTokens_MaxAvailable", "\"WithdrawableTokens\" <= \"AvailableTokens\"");
                t.HasCheckConstraint("CK_UserWallets_HeldTokens_NonNegative", "\"HeldTokens\" >= 0");
                t.HasCheckConstraint("CK_UserWallets_PendingWithdrawalTokens_NonNegative", "\"PendingWithdrawalTokens\" >= 0");
            });

            entity.HasOne(d => d.User).WithOne(p => p.UserWallet)
                .HasForeignKey<UserWallet>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("UserWallets_usr_UserId_fkey");
        });

        modelBuilder.Entity<WalletWithdrawal>(entity =>
        {
            entity.HasKey(e => e.WalletWithdrawalId).HasName("WalletWithdrawals_pkey");

            entity.HasIndex(e => e.BankAccountId, "IX_WalletWithdrawals_BankAccountId");
            entity.HasIndex(e => e.ProviderOrderCode, "IX_WalletWithdrawals_ProviderOrderCode").IsUnique();
            entity.HasIndex(e => new { e.Provider, e.ProviderPayoutId }, "IX_WalletWithdrawals_Provider_ProviderPayoutId").IsUnique();
            entity.HasIndex(e => e.Status, "IX_WalletWithdrawals_Status");
            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_WalletWithdrawals_UserId_CreatedAt").IsDescending(false, true);
            entity.HasIndex(e => new { e.UserId, e.IdempotencyKey }, "IX_WalletWithdrawals_UserId_IdempotencyKey").IsUnique();
            entity.HasIndex(e => e.UserWalletsId, "IX_WalletWithdrawals_UserWalletsId");

            entity.Property(e => e.WalletWithdrawalId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.BankCode).HasMaxLength(30);
            entity.Property(e => e.BankBin).HasMaxLength(6);
            entity.Property(e => e.BankName).HasMaxLength(120);
            entity.Property(e => e.BankAccountNumberEncrypted).HasColumnType("text");
            entity.Property(e => e.BankAccountNumberMasked).HasMaxLength(60);
            entity.Property(e => e.BankAccountName).HasMaxLength(120);
            entity.Property(e => e.TokenAmount).HasPrecision(18, 4);
            entity.Property(e => e.VndAmount).HasPrecision(18, 2);
            entity.Property(e => e.FeeVnd).HasPrecision(18, 2).HasDefaultValue(0m);
            entity.Property(e => e.NetVndAmount).HasPrecision(18, 2);
            entity.Property(e => e.Status)
                .HasComment("Enum WithdrawalStatus: 0=Pending, 1=Processing, 2=SyncRequired, 3=Success, 4=Failed, 5=Cancelled");
            entity.Property(e => e.Provider).HasMaxLength(100);
            entity.Property(e => e.ProviderOrderCode).HasMaxLength(100);
            entity.Property(e => e.ProviderPayoutId).HasMaxLength(200);
            entity.Property(e => e.ProviderTransactionCode).HasMaxLength(200);
            entity.Property(e => e.ProviderRawStatus).HasMaxLength(100);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(200);
            entity.Property(e => e.FailureReason).HasMaxLength(1000);
            entity.Property(e => e.LastSyncError).HasMaxLength(1000);
            entity.Property(e => e.Metadata).HasColumnType("text");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_WalletWithdrawals_TokenAmount_Positive", "\"TokenAmount\" > 0");
                t.HasCheckConstraint("CK_WalletWithdrawals_VndAmount_Positive", "\"VndAmount\" > 0");
                t.HasCheckConstraint("CK_WalletWithdrawals_NetVndAmount_Positive", "\"NetVndAmount\" > 0");
            });

            entity.HasOne(e => e.BankAccount).WithMany(e => e.WalletWithdrawals)
                .HasForeignKey(e => e.BankAccountId)
                .HasConstraintName("WalletWithdrawals_bnk_BankAccountId_fkey");

            entity.HasOne(e => e.User).WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("WalletWithdrawals_usr_UserId_fkey");

            entity.HasOne(e => e.UserWallet).WithMany(e => e.WalletWithdrawals)
                .HasForeignKey(e => e.UserWalletsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("WalletWithdrawals_uWal_UserWalletsId_fkey");
        });

        modelBuilder.Entity<WalletTransaction>(entity =>
        {
            entity.HasKey(e => e.WalletTransactionsId).HasName("WalletTransactions_pkey");

            entity.HasIndex(e => e.ContractEscrowId, "IX_WalletTransactions_ContractEscrowId");

            entity.HasIndex(e => e.ContractsId, "IX_WalletTransactions_ContractsId");

            entity.HasIndex(e => e.GatewayOrderCode, "IX_WalletTransactions_GatewayOrderCode");

            entity.HasIndex(e => e.GatewayTransactionCode, "IX_WalletTransactions_GatewayTransactionCode");

            entity.HasIndex(e => e.MilestonesId, "IX_WalletTransactions_MilestonesId");

            entity.HasIndex(e => e.Status, "IX_WalletTransactions_Status");

            entity.HasIndex(e => e.Type, "IX_WalletTransactions_Type");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_WalletTransactions_UserId_CreatedAt").IsDescending(false, true);

            entity.HasIndex(e => new { e.UserId, e.IdempotencyKey }, "IX_WalletTransactions_UserId_IdempotencyKey").IsUnique();

            entity.HasIndex(e => e.UserWalletsId, "IX_WalletTransactions_UserWalletsId");

            entity.Property(e => e.WalletTransactionsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("WalletTransactionsId");
            entity.Property(e => e.ContractEscrowId).HasColumnName("ContractEscrowId");
            entity.Property(e => e.ContractsId).HasColumnName("ContractsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.GatewayOrderCode).HasMaxLength(100);
            entity.Property(e => e.GatewayProvider).HasMaxLength(100);
            entity.Property(e => e.GatewayTransactionCode).HasMaxLength(200);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(200);
            entity.Property(e => e.Metadata).HasColumnType("text");
            entity.Property(e => e.MilestonesId).HasColumnName("MilestonesId");
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.Status)
                .HasComment("Enum WalletTransactionStatus: 0=Pending, 1=Succeeded, 2=Failed, 3=Cancelled");
            entity.Property(e => e.TokenAmount).HasPrecision(18, 4);
            entity.Property(e => e.Type)
                .HasComment("Enum WalletTransactionType: 0=AdminCredit, 1=TopUp, 2=EscrowHold, 3=EscrowRelease, 4=EscrowRefund, 5=Adjustment, 6=WithdrawalLock, 7=WithdrawalSuccess, 8=WithdrawalRefund, 9=WithdrawalFee, 10=SubscriptionPurchase, 11=PromotionPurchase");
            entity.Property(e => e.UserId).HasColumnName("UserId");
            entity.Property(e => e.UserWalletsId).HasColumnName("UserWalletsId");
            entity.Property(e => e.VndAmount).HasPrecision(18, 2);

            entity.HasOne(d => d.Contract).WithMany(p => p.WalletTransactions)
                .HasForeignKey(d => d.ContractsId)
                .HasConstraintName("WalletTransactions_cont_ContractsId_fkey");

            entity.HasOne(d => d.ContractEscrow).WithMany(p => p.WalletTransactions)
                .HasForeignKey(d => d.ContractEscrowId)
                .HasConstraintName("WalletTransactions_cEsc_ContractEscrowId_fkey");

            entity.HasOne(d => d.Milestone).WithMany()
                .HasForeignKey(d => d.MilestonesId)
                .HasConstraintName("WalletTransactions_mStone_MilestonesId_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.WalletTransactions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("WalletTransactions_usr_UserId_fkey");

            entity.HasOne(d => d.UserWallet).WithMany(p => p.WalletTransactions)
                .HasForeignKey(d => d.UserWalletsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("WalletTransactions_uWal_UserWalletsId_fkey");
        });

        modelBuilder.Entity<WorkExperience>(entity =>
        {
            entity.HasKey(e => e.WorkExperiencesId).HasName("WorkExperiences_pkey");

            entity.HasIndex(e => e.FreelancerId, "IX_WorkExperiences_FreelancerId");

            entity.Property(e => e.WorkExperiencesId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("WorkExperiencesId");
            entity.Property(e => e.CompanyName).HasMaxLength(300);
            entity.Property(e => e.FreelancerId).HasColumnName("FreelancerId");
            entity.Property(e => e.Title).HasMaxLength(300);

            entity.HasOne(d => d.Freelancer).WithMany(p => p.WorkExperiences)
                .HasForeignKey(d => d.FreelancerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("WorkExperiences_fl_FreelancerId_fkey");
        });

        modelBuilder.Entity<ReportContract>(entity =>
        {
            entity.HasKey(e => e.ReportContractId).HasName("ReportContracts_pkey");

            entity.HasIndex(e => e.ContractId, "IX_ReportContracts_ContractId");

            entity.HasIndex(e => e.ReporterId, "IX_ReportContracts_ReporterId");

            entity.HasIndex(e => e.RespondentId, "IX_ReportContracts_RespondentId");

            entity.HasIndex(e => e.Status, "IX_ReportContracts_Status");

            entity.Property(e => e.ReportContractId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ReportContractId");
            entity.Property(e => e.ContractId).HasColumnName("ContractId");
            entity.Property(e => e.ReporterId).HasColumnName("ReporterId");
            entity.Property(e => e.RespondentId).HasColumnName("RespondentId");
            entity.Property(e => e.MilestoneId).HasColumnName("MilestoneId");
            entity.Property(e => e.Description).HasMaxLength(5000);
            entity.Property(e => e.DesiredResolution).HasMaxLength(5000);
            entity.Property(e => e.IssueType)
                .HasComment("Enum ContractReportIssueType: 0=PaymentIssue, 1=MilestoneIssue, 2=Delay, 3=PoorQuality, 4=CommunicationProblem, 5=ScopeChange, 6=Other");
            entity.Property(e => e.Status)
                .HasComment("Enum ContractReportStatus: 0=Pending, 1=WaitingReporterConfirmation, 2=Resolved, 3=Escalated");
            entity.Property(e => e.ResolutionAction)
                .HasComment("Enum ContractReportResolutionAction: 0=AcceptIssue, 1=ProvideExplanation, 2=ProposeResolution, 3=RejectIssue");
            entity.Property(e => e.Explanation).HasMaxLength(5000);
            entity.Property(e => e.ProposedResolution).HasMaxLength(5000);
            entity.Property(e => e.RejectReason).HasMaxLength(5000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsEscalatedToDispute).HasDefaultValue(false);

            entity.HasOne(d => d.Contract).WithMany(p => p.ReportContracts)
                .HasForeignKey(d => d.ContractId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ReportContracts_cont_ContractId_fkey");

            entity.HasOne(d => d.Reporter).WithMany(p => p.ReportContractReporters)
                .HasForeignKey(d => d.ReporterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ReportContracts_usr_ReporterId_fkey");

            entity.HasOne(d => d.Respondent).WithMany(p => p.ReportContractRespondents)
                .HasForeignKey(d => d.RespondentId)
                .HasConstraintName("ReportContracts_usr_RespondentId_fkey");

            entity.HasOne(d => d.Milestone).WithMany(p => p.ReportContracts)
                .HasForeignKey(d => d.MilestoneId)
                .HasConstraintName("ReportContracts_mStone_MilestoneId_fkey");

            entity.HasOne(d => d.ResolvedByUser).WithMany(p => p.ReportContractResolvedBy)
                .HasForeignKey(d => d.ResolvedBy)
                .HasConstraintName("ReportContracts_usr_ResolvedBy_fkey");
        });

        modelBuilder.Entity<ReportContractAttachment>(entity =>
        {
            entity.HasKey(e => e.ReportContractAttachmentId).HasName("ReportContractAttachments_pkey");

            entity.HasIndex(e => e.ReportContractId, "IX_ReportContractAttachments_ReportContractId");

            entity.Property(e => e.ReportContractAttachmentId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ReportContractAttachmentId");
            entity.Property(e => e.ReportContractId).HasColumnName("ReportContractId");
            entity.Property(e => e.FileUrl).HasColumnName("FileUrl");
            entity.Property(e => e.FileName).HasMaxLength(500);
            entity.Property(e => e.ContentType).HasMaxLength(200);
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UploadedByUserId).HasColumnName("UploadedByUserId");

            entity.HasOne(d => d.ReportContract).WithMany(p => p.ReportContractAttachments)
                .HasForeignKey(d => d.ReportContractId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("ReportContractAttachments_rc_ReportContractId_fkey");
        });

        modelBuilder.Entity<TalentMatchRun>(entity =>
        {
            entity.ToTable("TalentMatchRuns");
            entity.HasKey(e => e.TalentMatchRunId);
            entity.HasIndex(e => new { e.ClientUserId, e.JobPostId, e.CreatedAt },
                "IX_TalentMatchRuns_ClientUserId_JobPostId_CreatedAt");
            entity.Property(e => e.TalentMatchRunId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.AlgorithmVersion).HasMaxLength(64);
            entity.Property(e => e.EmbeddingModel).HasMaxLength(200);
            entity.Property(e => e.ScoringVersion).HasMaxLength(64);
            entity.Property(e => e.FailureCode).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Status)
                .HasComment("Enum TalentMatchRunStatus: 0=Running, 1=Succeeded, 2=NoCandidates, 3=Failed");

            entity.HasOne(e => e.ClientUser).WithMany()
                .HasForeignKey(e => e.ClientUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.JobPost).WithMany()
                .HasForeignKey(e => e.JobPostId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TalentMatchResult>(entity =>
        {
            entity.ToTable("TalentMatchResults");
            entity.HasKey(e => e.TalentMatchResultId);
            entity.Property(e => e.TalentMatchResultId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.EmbeddingScore).HasPrecision(5, 2);
            entity.Property(e => e.AlgorithmScore).HasPrecision(5, 2);
            entity.Property(e => e.EvidenceScore).HasPrecision(5, 2);
            entity.Property(e => e.FinalScore).HasPrecision(5, 2);
            entity.Property(e => e.Confidence).HasMaxLength(16);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(e => e.TalentMatchRun).WithMany(e => e.Results)
                .HasForeignKey(e => e.TalentMatchRunId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.FreelancerProfile).WithMany()
                .HasForeignKey(e => e.FreelancerProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TalentMatchEvent>(entity =>
        {
            entity.ToTable("TalentMatchEvents");
            entity.HasKey(e => e.TalentMatchEventId);
            entity.HasIndex(e => e.IdempotencyKey, "UX_TalentMatchEvents_IdempotencyKey").IsUnique();
            entity.HasIndex(e => new { e.TalentMatchRunId, e.EventType, e.CreatedAt },
                "IX_TalentMatchEvents_Run_Type_CreatedAt");
            entity.Property(e => e.TalentMatchEventId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.IdempotencyKey).HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.EventType)
                .HasComment("Enum TalentMatchEventType: 0=Impression, 1=ProfileOpened, 2=Saved, 3=Invited, 4=ProposalSubmitted, 5=Shortlisted, 6=InterviewStarted, 7=InterviewCompleted, 8=Hired, 9=ContractCompleted");

            entity.HasOne(e => e.TalentMatchRun).WithMany(e => e.Events)
                .HasForeignKey(e => e.TalentMatchRunId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.TalentMatchResult).WithMany(e => e.Events)
                .HasForeignKey(e => new { e.TalentMatchRunId, e.FreelancerProfileId })
                .HasPrincipalKey(e => new { e.TalentMatchRunId, e.FreelancerProfileId })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.FreelancerProfile).WithMany()
                .HasForeignKey(e => e.FreelancerProfileId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
