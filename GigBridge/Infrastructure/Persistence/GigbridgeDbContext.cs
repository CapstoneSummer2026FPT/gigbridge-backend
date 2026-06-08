using System;
using System.Collections.Generic;
using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public partial class GigbridgeDbContext : DbContext, IApplicationDbContext
{
    public GigbridgeDbContext(DbContextOptions<GigbridgeDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AdminAuditLog> AdminAuditLogs { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<ClientProfile> ClientProfiles { get; set; }

    public virtual DbSet<Contract> Contracts { get; set; }

    public virtual DbSet<Conversation> Conversations { get; set; }

    public virtual DbSet<Dispute> Disputes { get; set; }

    public virtual DbSet<DisputeEvidence> DisputeEvidences { get; set; }

    public virtual DbSet<DisputeMessage> DisputeMessages { get; set; }

    public virtual DbSet<EsignDocument> EsignDocuments { get; set; }

    public virtual DbSet<EsignSignature> EsignSignatures { get; set; }

    public virtual DbSet<EsignTemplate> EsignTemplates { get; set; }

    public virtual DbSet<Faq> Faqs { get; set; }

    public virtual DbSet<Faqcategory> Faqcategories { get; set; }

    public virtual DbSet<FreelancerProfile> FreelancerProfiles { get; set; }

    public virtual DbSet<FreelancerSkill> FreelancerSkills { get; set; }

    public virtual DbSet<JobPost> JobPosts { get; set; }

    public virtual DbSet<JobPostAttachment> JobPostAttachments { get; set; }

    public virtual DbSet<JobPostSkill> JobPostSkills { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<MessageAttachment> MessageAttachments { get; set; }

    public virtual DbSet<Milestone> Milestones { get; set; }

    public virtual DbSet<MilestoneAttachment> MilestoneAttachments { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<PaymentProof> PaymentProofs { get; set; }

    public virtual DbSet<PlatformSetting> PlatformSettings { get; set; }

    public virtual DbSet<PortfolioItem> PortfolioItems { get; set; }

    public virtual DbSet<Proposal> Proposals { get; set; }

    public virtual DbSet<ProposalAttachment> ProposalAttachments { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<Report> Reports { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<SavedFreelancer> SavedFreelancers { get; set; }

    public virtual DbSet<SavedJob> SavedJobs { get; set; }

    public virtual DbSet<Skill> Skills { get; set; }

    public virtual DbSet<Subscription> Subscriptions { get; set; }

    public virtual DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }

    public virtual DbSet<UserEloPointTransaction> UserEloPointTransactions { get; set; }

    public virtual DbSet<UserEloScore> UserEloScores { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<WorkExperience> WorkExperiences { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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


        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoriesId).HasName("Categories_pkey");

            entity.HasIndex(e => e.IsActive, "IX_Categories_IsActive");

            entity.HasIndex(e => e.ParentCategoryId, "IX_Categories_ParentCategoryId");

            entity.HasIndex(e => e.Slug, "IX_Categories_Slug").IsUnique();

            entity.Property(e => e.CategoriesId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("CategoriesId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.NameVi).HasMaxLength(200);
            entity.Property(e => e.Slug).HasMaxLength(200);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);

            entity.HasOne(d => d.ParentCategory).WithMany(p => p.InverseParentCategory)
                .HasForeignKey(d => d.ParentCategoryId)
                .HasConstraintName("Categories_ParentCategoryId_fkey");
        });

        modelBuilder.Entity<ClientProfile>(entity =>
        {
            entity.HasKey(e => e.ClientProfilesId).HasName("ClientProfiles_pkey");

            entity.HasIndex(e => e.UserId, "ClientProfiles_usr_UserId_key").IsUnique();

            entity.HasIndex(e => e.UserId, "IX_ClientProfiles_UserId").IsUnique();

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

            entity.HasIndex(e => e.JobPostsId, "IX_Contracts_JobPostsId");

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
            entity.Property(e => e.PaymentType).HasComment("Enum PaymentType: 0=Fixed, 1=Hourly");
            entity.Property(e => e.ProposalsId).HasColumnName("ProposalsId");
            entity.Property(e => e.Status).HasComment("Enum ContractStatus: 0=Active, 1=Completed, 2=Cancelled, 3=Disputed");
            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.TotalBudget).HasPrecision(18, 2);

            entity.HasOne(d => d.ClientProfiles).WithMany(p => p.Contracts)
                .HasForeignKey(d => d.ClientProfilesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Contracts_clPro_ClientProfilesId_fkey");

            entity.HasOne(d => d.FreelancerProfiles).WithMany(p => p.Contracts)
                .HasForeignKey(d => d.FreelancerProfilesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Contracts_flPro_FreelancerProfilesId_fkey");

            entity.HasOne(d => d.JobPosts).WithMany(p => p.Contracts)
                .HasForeignKey(d => d.JobPostsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Contracts_jp_JobPostsId_fkey");

            entity.HasOne(d => d.Proposals).WithOne(p => p.Contract)
                .HasForeignKey<Contract>(d => d.ProposalsId)
                .HasConstraintName("Contracts_propo_ProposalsId_fkey");
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.ConversationsId).HasName("Conversations_pkey");

            entity.HasIndex(e => new { e.User1Id, e.User2Id, e.ContractsId }, "Conversations_usr_User1Id_usr_User2Id_cont_ContractsId_key").IsUnique();

            entity.HasIndex(e => e.ContractsId, "IX_Conversations_ContractsId");

            entity.HasIndex(e => e.LastMessageAt, "IX_Conversations_LastMessageAt").IsDescending();

            entity.HasIndex(e => e.User1Id, "IX_Conversations_User1Id");

            entity.HasIndex(e => e.User2Id, "IX_Conversations_User2Id");

            entity.Property(e => e.ConversationsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ConversationsId");
            entity.Property(e => e.ContractsId).HasColumnName("ContractsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Type)
                .HasDefaultValue(0)
                .HasComment("Enum ConversationType: 0=DirectMessage, 1=ContractChat");
            entity.Property(e => e.User1Id).HasColumnName("User1Id");
            entity.Property(e => e.User2Id).HasColumnName("User2Id");

            entity.HasOne(d => d.Contracts).WithMany(p => p.Conversations)
                .HasForeignKey(d => d.ContractsId)
                .HasConstraintName("Conversations_cont_ContractsId_fkey");

            entity.HasOne(d => d.User1).WithMany(p => p.ConversationUser1s)
                .HasForeignKey(d => d.User1Id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Conversations_usr_User1Id_fkey");

            entity.HasOne(d => d.User2).WithMany(p => p.ConversationUser2s)
                .HasForeignKey(d => d.User2Id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Conversations_usr_User2Id_fkey");
        });

        modelBuilder.Entity<Dispute>(entity =>
        {
            entity.HasKey(e => e.DisputesId).HasName("Disputes_pkey");

            entity.HasIndex(e => e.ContractsId, "IX_Disputes_ContractsId");

            entity.HasIndex(e => e.InitiatorId, "IX_Disputes_InitiatorId");

            entity.HasIndex(e => e.ResolvedByAdminId, "IX_Disputes_ResolvedByAdminId");

            entity.HasIndex(e => e.Status, "IX_Disputes_Status");

            entity.Property(e => e.DisputesId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("DisputesId");
            entity.Property(e => e.ContractsId).HasColumnName("ContractsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.MilestonesId).HasColumnName("MilestonesId");
            entity.Property(e => e.Resolution).HasComment("Enum DisputeResolution: 0=ClientFavored, 1=FreelancerFavored, 2=Split, 3=Dismissed");
            entity.Property(e => e.Status).HasComment("Enum DisputeStatus: 0=Open, 1=UnderReview, 2=Resolved, 3=Closed");
            entity.Property(e => e.InitiatorId).HasColumnName("InitiatorId");

            entity.HasOne(d => d.Contracts).WithMany(p => p.Disputes)
                .HasForeignKey(d => d.ContractsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Disputes_cont_ContractsId_fkey");

            entity.HasOne(d => d.Milestones).WithMany(p => p.Disputes)
                .HasForeignKey(d => d.MilestonesId)
                .HasConstraintName("Disputes_mStone_MilestonesId_fkey");

            entity.HasOne(d => d.ResolvedByAdmin).WithMany(p => p.DisputeResolvedByAdmins)
                .HasForeignKey(d => d.ResolvedByAdminId)
                .HasConstraintName("Disputes_ResolvedByAdminId_fkey");

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

            entity.Property(e => e.DisputeEvidenceId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("DisputeEvidenceId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.DisputesId).HasColumnName("DisputesId");
            entity.Property(e => e.FileName).HasMaxLength(500);
            entity.Property(e => e.UploadedById).HasColumnName("UploadedById");

            entity.HasOne(d => d.Disputes).WithMany(p => p.DisputeEvidences)
                .HasForeignKey(d => d.DisputesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("DisputeEvidence_disp_DisputesId_fkey");

            entity.HasOne(d => d.UploadedBy).WithMany(p => p.DisputeEvidences)
                .HasForeignKey(d => d.UploadedById)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("DisputeEvidence_usr_UploadedById_fkey");
        });

        modelBuilder.Entity<DisputeMessage>(entity =>
        {
            entity.HasKey(e => e.DisputeMessagesId).HasName("DisputeMessages_pkey");

            entity.HasIndex(e => new { e.DisputesId, e.CreatedAt }, "IX_DisputeMessages_DisputesId_CreatedAt");

            entity.Property(e => e.DisputeMessagesId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("DisputeMessagesId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.DisputesId).HasColumnName("DisputesId");
            entity.Property(e => e.SenderId).HasColumnName("SenderId");

            entity.HasOne(d => d.Disputes).WithMany(p => p.DisputeMessages)
                .HasForeignKey(d => d.DisputesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("DisputeMessages_disp_DisputesId_fkey");

            entity.HasOne(d => d.Sender).WithMany(p => p.DisputeMessages)
                .HasForeignKey(d => d.SenderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("DisputeMessages_usr_SenderId_fkey");
        });



        modelBuilder.Entity<EsignDocument>(entity =>
        {
            entity.HasKey(e => e.EsignDocumentsId).HasName("ESignDocuments_pkey");

            entity.ToTable("ESignDocuments");

            entity.HasIndex(e => e.ContractsId, "ESignDocuments_cont_ContractsId_key").IsUnique();

            entity.HasIndex(e => e.DocumentCode, "IX_ESignDocuments_DocumentCode").IsUnique();

            entity.HasIndex(e => e.JobPostsId, "IX_ESignDocuments_JobPostsId");

            entity.HasIndex(e => e.Status, "IX_ESignDocuments_Status");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_ESignDocuments_Status_CreatedAt").IsDescending(false, true);

            entity.Property(e => e.EsignDocumentsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ESignDocumentsId");
            entity.Property(e => e.ContractsId).HasColumnName("ContractsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.DocumentCode).HasMaxLength(50);
            entity.Property(e => e.DocumentHash).HasMaxLength(128);
            entity.Property(e => e.EsignTemplatesId).HasColumnName("ESignTemplatesId");
            entity.Property(e => e.JobPostsId).HasColumnName("JobPostsId");
            entity.Property(e => e.Status)
                .HasDefaultValue(0)
                .HasComment("Enum ESignDocumentStatus: 0=Draft, 1=PendingSignatures, 2=PartiallySigned, 3=FullySigned, 4=Expired, 5=Voided");

            entity.HasOne(d => d.Contracts).WithOne(p => p.EsignDocument)
                .HasForeignKey<EsignDocument>(d => d.ContractsId)
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

            entity.Property(e => e.EsignTemplatesId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ESignTemplatesId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(300);
            entity.Property(e => e.NameVi).HasMaxLength(300);
            entity.Property(e => e.PlaceholderSchema).HasColumnType("jsonb");
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
            entity.Property(e => e.NameVi).HasMaxLength(200);
            entity.Property(e => e.Slug).HasMaxLength(200);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
        });

        modelBuilder.Entity<FreelancerProfile>(entity =>
        {
            entity.HasKey(e => e.FreelancerProfilesId).HasName("FreelancerProfiles_pkey");

            entity.HasIndex(e => e.UserId, "FreelancerProfiles_usr_UserId_key").IsUnique();

            entity.HasIndex(e => e.Availability, "IX_FreelancerProfiles_Availability");

            entity.HasIndex(e => e.ExperienceLevel, "IX_FreelancerProfiles_ExperienceLevel");

            entity.HasIndex(e => e.UserId, "IX_FreelancerProfiles_UserId").IsUnique();

            entity.Property(e => e.FreelancerProfilesId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("FreelancerProfilesId");
            entity.Property(e => e.Availability)
                .HasDefaultValue(0)
                .HasComment("Enum Availability: 0=FullTime, 1=PartTime, 2=NotAvailable");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.ExperienceLevel).HasComment("Enum ExperienceLevel: 0=Entry, 1=Intermediate, 2=Expert");
            entity.Property(e => e.HourlyRate).HasPrecision(18, 2);
            entity.Property(e => e.Location).HasMaxLength(300);
            entity.Property(e => e.Title).HasMaxLength(300);
            entity.Property(e => e.UserId).HasColumnName("UserId");

            entity.HasOne(d => d.User).WithOne(p => p.FreelancerProfile)
                .HasForeignKey<FreelancerProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FreelancerProfiles_usr_UserId_fkey");
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

        modelBuilder.Entity<JobPost>(entity =>
        {
            entity.HasKey(e => e.JobPostsId).HasName("JobPosts_pkey");

            entity.HasIndex(e => e.ApplicationDeadline, "IX_JobPosts_ApplicationDeadline");

            entity.HasIndex(e => e.CategoryId, "IX_JobPosts_CategoryId");

            entity.HasIndex(e => e.ClientProfilesId, "IX_JobPosts_ClientProfilesId");

            entity.HasIndex(e => e.CreatedAt, "IX_JobPosts_CreatedAt").IsDescending();

            entity.HasIndex(e => e.Status, "IX_JobPosts_Status");

            entity.HasIndex(e => new { e.Status, e.Visibility }, "IX_JobPosts_Status_Visibility");

            entity.HasIndex(e => new { e.Status, e.Visibility, e.CreatedAt }, "IX_JobPosts_Status_Visibility_CreatedAt").IsDescending(false, false, true);

            entity.Property(e => e.JobPostsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("JobPostsId");
            entity.Property(e => e.BudgetMax).HasPrecision(18, 2);
            entity.Property(e => e.BudgetMin).HasPrecision(18, 2);
            entity.Property(e => e.BudgetType).HasComment("Enum BudgetType: 0=Fixed, 1=Hourly");
            entity.Property(e => e.ClientProfilesId).HasColumnName("ClientProfilesId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Currency)
                .HasMaxLength(5)
                .HasDefaultValueSql("'VND'::character varying");
            entity.Property(e => e.EstimatedDuration).HasMaxLength(100);
            entity.Property(e => e.ExperienceLevelRequired).HasComment("Enum ExperienceLevel: 0=Entry, 1=Intermediate, 2=Expert");
            entity.Property(e => e.IsAigenerated)
                .HasDefaultValue(false)
                .HasColumnName("IsAIGenerated");
            entity.Property(e => e.Location).HasMaxLength(300);
            entity.Property(e => e.LocationType)
                .HasDefaultValue(0)
                .HasComment("Enum LocationType: 0=Remote, 1=OnSite, 2=Hybrid");
            entity.Property(e => e.Status).HasComment("Enum JobPostStatus: 0=Draft, 1=Open, 2=InProgress, 3=Closed, 4=Cancelled");
            entity.Property(e => e.Title).HasMaxLength(500);
            entity.Property(e => e.Visibility)
                .HasDefaultValue(0)
                .HasComment("Enum JobPostVisibility: 0=Public, 1=Private, 2=InviteOnly");

            entity.HasOne(d => d.Category).WithMany(p => p.JobPosts)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("JobPosts_CategoryId_fkey");

            entity.HasOne(d => d.ClientProfiles).WithMany(p => p.JobPosts)
                .HasForeignKey(d => d.ClientProfilesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("JobPosts_clPro_ClientProfilesId_fkey");
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

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.MessagesId).HasName("Messages_pkey");

            entity.HasIndex(e => new { e.ConversationsId, e.CreatedAt }, "IX_Messages_ConversationsId_CreatedAt").IsDescending(false, true);

            entity.HasIndex(e => e.IsRead, "IX_Messages_IsRead");

            entity.HasIndex(e => e.SenderId, "IX_Messages_SenderId");

            entity.Property(e => e.MessagesId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("MessagesId");
            entity.Property(e => e.ConversationsId).HasColumnName("ConversationsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.IsEdited).HasDefaultValue(false);
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.Type)
                .HasDefaultValue(0)
                .HasComment("Enum MessageType: 0=Text, 1=File, 2=System");
            entity.Property(e => e.SenderId).HasColumnName("SenderId");

            entity.HasOne(d => d.Conversations).WithMany(p => p.Messages)
                .HasForeignKey(d => d.ConversationsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Messages_conv_ConversationsId_fkey");

            entity.HasOne(d => d.Sender).WithMany(p => p.Messages)
                .HasForeignKey(d => d.SenderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Messages_usr_SenderId_fkey");
        });

        modelBuilder.Entity<MessageAttachment>(entity =>
        {
            entity.HasKey(e => e.MessageAttachmentsId).HasName("MessageAttachments_pkey");

            entity.HasIndex(e => e.MessagesId, "IX_MessageAttachments_MessagesId");

            entity.Property(e => e.MessageAttachmentsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("MessageAttachmentsId");
            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.FileName).HasMaxLength(500);
            entity.Property(e => e.MessagesId).HasColumnName("MessagesId");

            entity.HasOne(d => d.Messages).WithMany(p => p.MessageAttachments)
                .HasForeignKey(d => d.MessagesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("MessageAttachments_msg_MessagesId_fkey");
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
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.Status).HasComment("Enum MilestoneStatus: 0=Pending, 1=InProgress, 2=Submitted, 3=Approved, 4=PaymentProofUploaded, 5=PaymentConfirmed, 6=Disputed");
            entity.Property(e => e.Title).HasMaxLength(500);

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
            entity.Property(e => e.MilestonesId).HasColumnName("MilestonesId");

            entity.HasOne(d => d.Milestones).WithMany(p => p.MilestoneAttachments)
                .HasForeignKey(d => d.MilestonesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("MilestoneAttachments_mStone_MilestonesId_fkey");

            entity.HasOne(d => d.UploadedByUser).WithMany(p => p.MilestoneAttachments)
                .HasForeignKey(d => d.UploadedByUserId)
                .HasConstraintName("MilestoneAttachments_UploadedByUserId_fkey");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationsId).HasName("Notifications_pkey");

            entity.HasIndex(e => new { e.ReferenceId, e.ReferenceType }, "IX_Notifications_ReferenceId_ReferenceType");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_Notifications_UserId_CreatedAt").IsDescending(false, true);

            entity.HasIndex(e => new { e.UserId, e.IsRead }, "IX_Notifications_UserId_IsRead");

            entity.Property(e => e.NotificationsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("NotificationsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.ReferenceType).HasMaxLength(50);
            entity.Property(e => e.Title).HasMaxLength(300);
            entity.Property(e => e.Type).HasComment("Enum NotificationType: 0=NewJob, 1=ProposalReceived, 2=ProposalStatusChanged, 3=ContractStarted, 4=MilestoneUpdated, 5=PaymentProofUploaded, 6=PaymentConfirmed, 7=ChatMessage, 8=DisputeUpdate, 9=ReviewReceived, 10=SystemAlert, 11=AIInterviewInvite, 12=SubscriptionExpiring");
            entity.Property(e => e.UserId).HasColumnName("UserId");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Notifications_usr_UserId_fkey");
        });

        modelBuilder.Entity<PaymentProof>(entity =>
        {
            entity.HasKey(e => e.PaymentProofsId).HasName("PaymentProofs_pkey");

            entity.HasIndex(e => e.MilestonesId, "IX_PaymentProofs_MilestonesId");

            entity.HasIndex(e => e.Status, "IX_PaymentProofs_Status");

            entity.HasIndex(e => e.UploadedById, "IX_PaymentProofs_UploadedById");

            entity.Property(e => e.PaymentProofsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("PaymentProofsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.FileName).HasMaxLength(500);
            entity.Property(e => e.MilestonesId).HasColumnName("MilestonesId");
            entity.Property(e => e.Status)
                .HasDefaultValue(0)
                .HasComment("Enum PaymentProofStatus: 0=Pending, 1=Confirmed, 2=Disputed");
            entity.Property(e => e.UploadedById).HasColumnName("UploadedById");

            entity.HasOne(d => d.Milestones).WithMany(p => p.PaymentProofs)
                .HasForeignKey(d => d.MilestonesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("PaymentProofs_mStone_MilestonesId_fkey");

            entity.HasOne(d => d.UploadedBy).WithMany(p => p.PaymentProofs)
                .HasForeignKey(d => d.UploadedById)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("PaymentProofs_usr_UploadedById_fkey");
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
            entity.Property(e => e.ProposedRate).HasPrecision(18, 2);
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

        modelBuilder.Entity<ProposalAttachment>(entity =>
        {
            entity.HasKey(e => e.ProposalAttachmentsId).HasName("ProposalAttachments_pkey");

            entity.HasIndex(e => e.ProposalsId, "IX_ProposalAttachments_ProposalsId");

            entity.Property(e => e.ProposalAttachmentsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ProposalAttachmentsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.FileName).HasMaxLength(500);
            entity.Property(e => e.ProposalsId).HasColumnName("ProposalsId");

            entity.HasOne(d => d.Proposals).WithMany(p => p.ProposalAttachments)
                .HasForeignKey(d => d.ProposalsId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("ProposalAttachments_propo_ProposalsId_fkey");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("RefreshTokens_pkey");

            entity.HasIndex(e => e.ExpiresAt, "IX_RefreshTokens_ExpiresAt");

            entity.HasIndex(e => e.Token, "IX_RefreshTokens_Token").IsUnique();

            entity.HasIndex(e => e.UserId, "IX_RefreshTokens_UserId");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("Id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UserId).HasColumnName("UserId");

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("RefreshTokens_usr_UserId_fkey");
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
            entity.Property(e => e.AdminAttachmentFileName).HasMaxLength(500);
            entity.Property(e => e.AdminAttachmentUrl).HasComment("v1.2: Admin đính kèm bản hợp đồng lao động e-sign PDF cho tranh chấp thanh toán");
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

            entity.HasIndex(e => new { e.ContractsId, e.ReviewerId }, "Reviews_cont_ContractsId_usr_ReviewerId_key").IsUnique();

            entity.Property(e => e.ReviewsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ReviewsId");
            entity.Property(e => e.ContractsId).HasColumnName("ContractsId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsVisible).HasDefaultValue(true);
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

            entity.HasIndex(e => e.CategoriesId, "IX_Skills_CategoriesId");

            entity.HasIndex(e => e.IsActive, "IX_Skills_IsActive");

            entity.HasIndex(e => e.Name, "IX_Skills_Name");

            entity.Property(e => e.SkillsId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("SkillsId");
            entity.Property(e => e.CategoriesId).HasColumnName("CategoriesId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.NameVi).HasMaxLength(200);

            entity.HasOne(d => d.Categories).WithMany(p => p.Skills)
                .HasForeignKey(d => d.CategoriesId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Skills_cate_CategoriesId_fkey");
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
                .HasMaxLength(5)
                .HasDefaultValueSql("'VND'::character varying");
            entity.Property(e => e.Features).HasColumnType("jsonb");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.NameVi).HasMaxLength(200);
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
            entity.Property(e => e.Reason).HasComment("Enum UserEloPointReason: 0=InitialGrant, 1=InactivityPenalty, 2=ReturnBonus, 3=JobCompletion, 4=ReviewRating");
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
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.Password).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsEmailVerified).HasDefaultValue(false);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.PreferredLanguage)
                .HasMaxLength(5)
                .HasDefaultValueSql("'vi'::character varying");
            entity.Property(e => e.Role).HasComment("Enum UserRole: 0=Client, 1=Freelancer, 2=Admin");
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
            entity.Property(e => e.IsCurrentJob).HasDefaultValue(false);
            entity.Property(e => e.Title).HasMaxLength(300);

            entity.HasOne(d => d.Freelancer).WithMany(p => p.WorkExperiences)
                .HasForeignKey(d => d.FreelancerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("WorkExperiences_fl_FreelancerId_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
