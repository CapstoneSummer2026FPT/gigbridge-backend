using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    cate_CategoriesId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameVi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ParentCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Categories_pkey", x => x.cate_CategoriesId);
                    table.ForeignKey(
                        name: "Categories_ParentCategoryId_fkey",
                        column: x => x.ParentCategoryId,
                        principalTable: "Categories",
                        principalColumn: "cate_CategoriesId");
                });

            migrationBuilder.CreateTable(
                name: "FAQCategories",
                columns: table => new
                {
                    faqCat_FAQCategoriesId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameVi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("FAQCategories_pkey", x => x.faqCat_FAQCategoriesId);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    subPlan_SubscriptionPlansId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameVi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true, defaultValueSql: "'VND'::character varying"),
                    DurationInDays = table.Column<int>(type: "integer", nullable: false),
                    Features = table.Column<string>(type: "jsonb", nullable: true),
                    TargetRole = table.Column<int>(type: "integer", nullable: true, comment: "Enum UserRole: 0=Client, 1=Freelancer, NULL=Both"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("SubscriptionPlans_pkey", x => x.subPlan_SubscriptionPlansId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    usr_UserId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Avatar = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: false, comment: "Enum UserRole: 0=Client, 1=Freelancer, 2=Admin"),
                    IsEmailVerified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    PreferredLanguage = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true, defaultValueSql: "'vi'::character varying"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Users_pkey", x => x.usr_UserId);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    sk_SkillsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    cate_CategoriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NameVi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Skills_pkey", x => x.sk_SkillsId);
                    table.ForeignKey(
                        name: "Skills_cate_CategoriesId_fkey",
                        column: x => x.cate_CategoriesId,
                        principalTable: "Categories",
                        principalColumn: "cate_CategoriesId");
                });

            migrationBuilder.CreateTable(
                name: "FAQs",
                columns: table => new
                {
                    faq_FAQsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    faqCat_FAQCategoriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    QuestionVi = table.Column<string>(type: "text", nullable: true),
                    Answer = table.Column<string>(type: "text", nullable: false),
                    AnswerVi = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("FAQs_pkey", x => x.faq_FAQsId);
                    table.ForeignKey(
                        name: "FAQs_faqCat_FAQCategoriesId_fkey",
                        column: x => x.faqCat_FAQCategoriesId,
                        principalTable: "FAQCategories",
                        principalColumn: "faqCat_FAQCategoriesId");
                });

            migrationBuilder.CreateTable(
                name: "AdminAuditLogs",
                columns: table => new
                {
                    aal_AdminAuditLogsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    usr_AdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OldValues = table.Column<string>(type: "jsonb", nullable: true),
                    NewValues = table.Column<string>(type: "jsonb", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("AdminAuditLogs_pkey", x => x.aal_AdminAuditLogsId);
                    table.ForeignKey(
                        name: "AdminAuditLogs_usr_AdminId_fkey",
                        column: x => x.usr_AdminId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateTable(
                name: "ClientProfiles",
                columns: table => new
                {
                    clPro_ClientProfilesId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    usr_UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CompanyWebsite = table.Column<string>(type: "text", nullable: true),
                    CompanySize = table.Column<int>(type: "integer", nullable: true, comment: "Enum CompanySize: 0=Solo, 1=Small, 2=Medium, 3=Large"),
                    Industry = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CompanyDescription = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ClientProfiles_pkey", x => x.clPro_ClientProfilesId);
                    table.ForeignKey(
                        name: "ClientProfiles_usr_UserId_fkey",
                        column: x => x.usr_UserId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateTable(
                name: "ESignTemplates",
                columns: table => new
                {
                    eTpl_ESignTemplatesId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    NameVi = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    HtmlContent = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    PlaceholderSchema = table.Column<string>(type: "jsonb", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ESignTemplates_pkey", x => x.eTpl_ESignTemplatesId);
                    table.ForeignKey(
                        name: "ESignTemplates_CreatedBy_fkey",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateTable(
                name: "FreelancerProfiles",
                columns: table => new
                {
                    flPro_FreelancerProfilesId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    usr_UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Bio = table.Column<string>(type: "text", nullable: true),
                    HourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExperienceLevel = table.Column<int>(type: "integer", nullable: true, comment: "Enum ExperienceLevel: 0=Entry, 1=Intermediate, 2=Expert"),
                    Availability = table.Column<int>(type: "integer", nullable: true, defaultValue: 0, comment: "Enum Availability: 0=FullTime, 1=PartTime, 2=NotAvailable"),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ProfileCompletionScore = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("FreelancerProfiles_pkey", x => x.flPro_FreelancerProfilesId);
                    table.ForeignKey(
                        name: "FreelancerProfiles_usr_UserId_fkey",
                        column: x => x.usr_UserId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    noti_NotificationsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    usr_UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false, comment: "Enum NotificationType: 0=NewJob, 1=ProposalReceived, 2=ProposalStatusChanged, 3=ContractStarted, 4=MilestoneUpdated, 5=PaymentProofUploaded, 6=PaymentConfirmed, 7=ChatMessage, 8=DisputeUpdate, 9=ReviewReceived, 10=SystemAlert, 11=AIInterviewInvite, 12=SubscriptionExpiring"),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReferenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("Notifications_pkey", x => x.noti_NotificationsId);
                    table.ForeignKey(
                        name: "Notifications_usr_UserId_fkey",
                        column: x => x.usr_UserId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateTable(
                name: "PlatformSettings",
                columns: table => new
                {
                    ps_PlatformSettingsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DataType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, defaultValueSql: "'string'::character varying"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByAdminId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PlatformSettings_pkey", x => x.ps_PlatformSettingsId);
                    table.ForeignKey(
                        name: "PlatformSettings_UpdatedByAdminId_fkey",
                        column: x => x.UpdatedByAdminId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    rt_Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    usr_UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("RefreshTokens_pkey", x => x.rt_Id);
                    table.ForeignKey(
                        name: "RefreshTokens_usr_UserId_fkey",
                        column: x => x.usr_UserId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    rpt_ReportsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    usr_ReporterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportedEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportedEntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false, comment: "Enum ReportType: 0=Spam, 1=Fraud, 2=InappropriateContent, 3=HarassmentOrAbuse, 4=Other, 5=PaymentDispute"),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0, comment: "Enum ReportStatus: 0=Pending, 1=Reviewing, 2=Resolved, 3=Dismissed"),
                    AdminNote = table.Column<string>(type: "text", nullable: true),
                    ResolvedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AdminAttachmentUrl = table.Column<string>(type: "text", nullable: true, comment: "v1.2: Admin đính kèm bản hợp đồng lao động e-sign PDF cho tranh chấp thanh toán"),
                    AdminAttachmentFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Reports_pkey", x => x.rpt_ReportsId);
                    table.ForeignKey(
                        name: "Reports_ResolvedByAdminId_fkey",
                        column: x => x.ResolvedByAdminId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                    table.ForeignKey(
                        name: "Reports_usr_ReporterId_fkey",
                        column: x => x.usr_ReporterId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    sub_SubscriptionsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    usr_UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    subPlan_SubscriptionPlansId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Enum SubscriptionStatus: 0=Active, 1=Expired, 2=Cancelled"),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AutoRenew = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    PaymentReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Subscriptions_pkey", x => x.sub_SubscriptionsId);
                    table.ForeignKey(
                        name: "Subscriptions_subPlan_SubscriptionPlansId_fkey",
                        column: x => x.subPlan_SubscriptionPlansId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "subPlan_SubscriptionPlansId");
                    table.ForeignKey(
                        name: "Subscriptions_usr_UserId_fkey",
                        column: x => x.usr_UserId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateTable(
                name: "JobPosts",
                columns: table => new
                {
                    jp_JobPostsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    clPro_ClientProfilesId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    BudgetType = table.Column<int>(type: "integer", nullable: false, comment: "Enum BudgetType: 0=Fixed, 1=Hourly"),
                    BudgetMin = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    BudgetMax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true, defaultValueSql: "'VND'::character varying"),
                    EstimatedDuration = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MaxHires = table.Column<int>(type: "integer", nullable: true),
                    ExperienceLevelRequired = table.Column<int>(type: "integer", nullable: true, comment: "Enum ExperienceLevel: 0=Entry, 1=Intermediate, 2=Expert"),
                    LocationType = table.Column<int>(type: "integer", nullable: true, defaultValue: 0, comment: "Enum LocationType: 0=Remote, 1=OnSite, 2=Hybrid"),
                    Location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Enum JobPostStatus: 0=Draft, 1=Open, 2=InProgress, 3=Closed, 4=Cancelled"),
                    Visibility = table.Column<int>(type: "integer", nullable: true, defaultValue: 0, comment: "Enum JobPostVisibility: 0=Public, 1=Private, 2=InviteOnly"),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsAIGenerated = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("JobPosts_pkey", x => x.jp_JobPostsId);
                    table.ForeignKey(
                        name: "JobPosts_CategoryId_fkey",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "cate_CategoriesId");
                    table.ForeignKey(
                        name: "JobPosts_clPro_ClientProfilesId_fkey",
                        column: x => x.clPro_ClientProfilesId,
                        principalTable: "ClientProfiles",
                        principalColumn: "clPro_ClientProfilesId");
                });

            migrationBuilder.CreateTable(
                name: "Certifications",
                columns: table => new
                {
                    cer_CertificationsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    fl_FreelancerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IssuingOrganization = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CredentialUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Certifications_pkey", x => x.cer_CertificationsId);
                    table.ForeignKey(
                        name: "Certifications_fl_FreelancerId_fkey",
                        column: x => x.fl_FreelancerId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "flPro_FreelancerProfilesId");
                });

            migrationBuilder.CreateTable(
                name: "Educations",
                columns: table => new
                {
                    e_EducationsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    fl_FreelancerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Institution = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Degree = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FieldOfStudy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Educations_pkey", x => x.e_EducationsId);
                    table.ForeignKey(
                        name: "Educations_fl_FreelancerId_fkey",
                        column: x => x.fl_FreelancerId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "flPro_FreelancerProfilesId");
                });

            migrationBuilder.CreateTable(
                name: "FreelancerSkills",
                columns: table => new
                {
                    fSkill_FreelancerSkillsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    fl_FreelancerId = table.Column<Guid>(type: "uuid", nullable: false),
                    sk_SkillsId = table.Column<Guid>(type: "uuid", nullable: false),
                    YearsOfExperience = table.Column<int>(type: "integer", nullable: true),
                    ProficiencyLevel = table.Column<int>(type: "integer", nullable: true, comment: "Enum ProficiencyLevel: 0=Beginner, 1=Intermediate, 2=Advanced, 3=Expert")
                },
                constraints: table =>
                {
                    table.PrimaryKey("FreelancerSkills_pkey", x => x.fSkill_FreelancerSkillsId);
                    table.ForeignKey(
                        name: "FreelancerSkills_fl_FreelancerId_fkey",
                        column: x => x.fl_FreelancerId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "flPro_FreelancerProfilesId");
                    table.ForeignKey(
                        name: "FreelancerSkills_sk_SkillsId_fkey",
                        column: x => x.sk_SkillsId,
                        principalTable: "Skills",
                        principalColumn: "sk_SkillsId");
                });

            migrationBuilder.CreateTable(
                name: "PortfolioItems",
                columns: table => new
                {
                    pi_PortfolioItemsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    fl_FreelancerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ProjectUrl = table.Column<string>(type: "text", nullable: true),
                    ImageUrls = table.Column<string>(type: "text", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PortfolioItems_pkey", x => x.pi_PortfolioItemsId);
                    table.ForeignKey(
                        name: "PortfolioItems_CategoryId_fkey",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "cate_CategoriesId");
                    table.ForeignKey(
                        name: "PortfolioItems_fl_FreelancerId_fkey",
                        column: x => x.fl_FreelancerId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "flPro_FreelancerProfilesId");
                });

            migrationBuilder.CreateTable(
                name: "SavedFreelancers",
                columns: table => new
                {
                    sf_SavedFreelancersId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    usr_UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    flPro_FreelancerProfilesId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("SavedFreelancers_pkey", x => x.sf_SavedFreelancersId);
                    table.ForeignKey(
                        name: "SavedFreelancers_flPro_FreelancerProfilesId_fkey",
                        column: x => x.flPro_FreelancerProfilesId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "flPro_FreelancerProfilesId");
                    table.ForeignKey(
                        name: "SavedFreelancers_usr_UserId_fkey",
                        column: x => x.usr_UserId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateTable(
                name: "WorkExperiences",
                columns: table => new
                {
                    we_WorkExperiencesId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    fl_FreelancerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsCurrentJob = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("WorkExperiences_pkey", x => x.we_WorkExperiencesId);
                    table.ForeignKey(
                        name: "WorkExperiences_fl_FreelancerId_fkey",
                        column: x => x.fl_FreelancerId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "flPro_FreelancerProfilesId");
                });

            migrationBuilder.CreateTable(
                name: "JobPostAttachments",
                columns: table => new
                {
                    jpAttach_JobPostAttachmentsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    jp_JobPostsId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("JobPostAttachments_pkey", x => x.jpAttach_JobPostAttachmentsId);
                    table.ForeignKey(
                        name: "JobPostAttachments_jp_JobPostsId_fkey",
                        column: x => x.jp_JobPostsId,
                        principalTable: "JobPosts",
                        principalColumn: "jp_JobPostsId");
                });

            migrationBuilder.CreateTable(
                name: "JobPostSkills",
                columns: table => new
                {
                    jpSkill_JobPostSkillsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    jp_JobPostsId = table.Column<Guid>(type: "uuid", nullable: false),
                    sk_SkillsId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("JobPostSkills_pkey", x => x.jpSkill_JobPostSkillsId);
                    table.ForeignKey(
                        name: "JobPostSkills_jp_JobPostsId_fkey",
                        column: x => x.jp_JobPostsId,
                        principalTable: "JobPosts",
                        principalColumn: "jp_JobPostsId");
                    table.ForeignKey(
                        name: "JobPostSkills_sk_SkillsId_fkey",
                        column: x => x.sk_SkillsId,
                        principalTable: "Skills",
                        principalColumn: "sk_SkillsId");
                });

            migrationBuilder.CreateTable(
                name: "Proposals",
                columns: table => new
                {
                    propo_ProposalsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    jp_JobPostsId = table.Column<Guid>(type: "uuid", nullable: false),
                    flPro_FreelancerProfilesId = table.Column<Guid>(type: "uuid", nullable: false),
                    CoverLetter = table.Column<string>(type: "text", nullable: true),
                    ProposedRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ProposedDuration = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Enum ProposalStatus: 0=Pending, 1=Shortlisted, 2=Accepted, 3=Rejected, 4=Withdrawn"),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsAIGenerated = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Proposals_pkey", x => x.propo_ProposalsId);
                    table.ForeignKey(
                        name: "Proposals_flPro_FreelancerProfilesId_fkey",
                        column: x => x.flPro_FreelancerProfilesId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "flPro_FreelancerProfilesId");
                    table.ForeignKey(
                        name: "Proposals_jp_JobPostsId_fkey",
                        column: x => x.jp_JobPostsId,
                        principalTable: "JobPosts",
                        principalColumn: "jp_JobPostsId");
                });

            migrationBuilder.CreateTable(
                name: "SavedJobs",
                columns: table => new
                {
                    sj_SavedJobsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    usr_UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    jp_JobPostsId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("SavedJobs_pkey", x => x.sj_SavedJobsId);
                    table.ForeignKey(
                        name: "SavedJobs_jp_JobPostsId_fkey",
                        column: x => x.jp_JobPostsId,
                        principalTable: "JobPosts",
                        principalColumn: "jp_JobPostsId");
                    table.ForeignKey(
                        name: "SavedJobs_usr_UserId_fkey",
                        column: x => x.usr_UserId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateTable(
                name: "AIInterviewSessions",
                columns: table => new
                {
                    aiIntv_AIInterviewSessionsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    jp_JobPostsId = table.Column<Guid>(type: "uuid", nullable: false),
                    clPro_ClientProfilesId = table.Column<Guid>(type: "uuid", nullable: false),
                    flPro_FreelancerProfilesId = table.Column<Guid>(type: "uuid", nullable: false),
                    propo_ProposalsId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Enum InterviewStatus: 0=Pending, 1=InProgress, 2=Completed, 3=Cancelled"),
                    OverallScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    AIFeedback = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("AIInterviewSessions_pkey", x => x.aiIntv_AIInterviewSessionsId);
                    table.ForeignKey(
                        name: "AIInterviewSessions_clPro_ClientProfilesId_fkey",
                        column: x => x.clPro_ClientProfilesId,
                        principalTable: "ClientProfiles",
                        principalColumn: "clPro_ClientProfilesId");
                    table.ForeignKey(
                        name: "AIInterviewSessions_flPro_FreelancerProfilesId_fkey",
                        column: x => x.flPro_FreelancerProfilesId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "flPro_FreelancerProfilesId");
                    table.ForeignKey(
                        name: "AIInterviewSessions_jp_JobPostsId_fkey",
                        column: x => x.jp_JobPostsId,
                        principalTable: "JobPosts",
                        principalColumn: "jp_JobPostsId");
                    table.ForeignKey(
                        name: "AIInterviewSessions_propo_ProposalsId_fkey",
                        column: x => x.propo_ProposalsId,
                        principalTable: "Proposals",
                        principalColumn: "propo_ProposalsId");
                });

            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    cont_ContractsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    jp_JobPostsId = table.Column<Guid>(type: "uuid", nullable: false),
                    clPro_ClientProfilesId = table.Column<Guid>(type: "uuid", nullable: false),
                    flPro_FreelancerProfilesId = table.Column<Guid>(type: "uuid", nullable: false),
                    propo_ProposalsId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    TotalBudget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentType = table.Column<int>(type: "integer", nullable: false, comment: "Enum PaymentType: 0=Fixed, 1=Hourly"),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Enum ContractStatus: 0=Active, 1=Completed, 2=Cancelled, 3=Disputed"),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ESignContractPdfUrl = table.Column<string>(type: "text", nullable: true, comment: "v1.2: URL bản hợp đồng lao động e-sign PDF khi có tranh chấp thanh toán"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Contracts_pkey", x => x.cont_ContractsId);
                    table.ForeignKey(
                        name: "Contracts_clPro_ClientProfilesId_fkey",
                        column: x => x.clPro_ClientProfilesId,
                        principalTable: "ClientProfiles",
                        principalColumn: "clPro_ClientProfilesId");
                    table.ForeignKey(
                        name: "Contracts_flPro_FreelancerProfilesId_fkey",
                        column: x => x.flPro_FreelancerProfilesId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "flPro_FreelancerProfilesId");
                    table.ForeignKey(
                        name: "Contracts_jp_JobPostsId_fkey",
                        column: x => x.jp_JobPostsId,
                        principalTable: "JobPosts",
                        principalColumn: "jp_JobPostsId");
                    table.ForeignKey(
                        name: "Contracts_propo_ProposalsId_fkey",
                        column: x => x.propo_ProposalsId,
                        principalTable: "Proposals",
                        principalColumn: "propo_ProposalsId");
                });

            migrationBuilder.CreateTable(
                name: "ProposalAttachments",
                columns: table => new
                {
                    propoAttach_ProposalAttachmentsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    propo_ProposalsId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("ProposalAttachments_pkey", x => x.propoAttach_ProposalAttachmentsId);
                    table.ForeignKey(
                        name: "ProposalAttachments_propo_ProposalsId_fkey",
                        column: x => x.propo_ProposalsId,
                        principalTable: "Proposals",
                        principalColumn: "propo_ProposalsId");
                });

            migrationBuilder.CreateTable(
                name: "AIInterviewQuestions",
                columns: table => new
                {
                    aiQ_AIInterviewQuestionsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    aiIntv_AIInterviewSessionsId = table.Column<Guid>(type: "uuid", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: true),
                    Score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    AIAnalysis = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    AnsweredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("AIInterviewQuestions_pkey", x => x.aiQ_AIInterviewQuestionsId);
                    table.ForeignKey(
                        name: "AIInterviewQuestions_aiIntv_AIInterviewSessionsId_fkey",
                        column: x => x.aiIntv_AIInterviewSessionsId,
                        principalTable: "AIInterviewSessions",
                        principalColumn: "aiIntv_AIInterviewSessionsId");
                });

            migrationBuilder.CreateTable(
                name: "AIConversationSessions",
                columns: table => new
                {
                    aiSess_AIConversationSessionsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    usr_UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false, comment: "Enum AISessionType: 0=WorkAssistant, 1=ProfileOptimizer, 2=JobPostGenerator, 3=ProposalGenerator"),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cont_ContractsId = table.Column<Guid>(type: "uuid", nullable: true),
                    jp_JobPostsId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModelUsed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TotalTokensUsed = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("AIConversationSessions_pkey", x => x.aiSess_AIConversationSessionsId);
                    table.ForeignKey(
                        name: "AIConversationSessions_cont_ContractsId_fkey",
                        column: x => x.cont_ContractsId,
                        principalTable: "Contracts",
                        principalColumn: "cont_ContractsId");
                    table.ForeignKey(
                        name: "AIConversationSessions_jp_JobPostsId_fkey",
                        column: x => x.jp_JobPostsId,
                        principalTable: "JobPosts",
                        principalColumn: "jp_JobPostsId");
                    table.ForeignKey(
                        name: "AIConversationSessions_usr_UserId_fkey",
                        column: x => x.usr_UserId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    conv_ConversationsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    usr_User1Id = table.Column<Guid>(type: "uuid", nullable: false),
                    usr_User2Id = table.Column<Guid>(type: "uuid", nullable: false),
                    cont_ContractsId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: true, defaultValue: 0, comment: "Enum ConversationType: 0=DirectMessage, 1=ContractChat"),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Conversations_pkey", x => x.conv_ConversationsId);
                    table.ForeignKey(
                        name: "Conversations_cont_ContractsId_fkey",
                        column: x => x.cont_ContractsId,
                        principalTable: "Contracts",
                        principalColumn: "cont_ContractsId");
                    table.ForeignKey(
                        name: "Conversations_usr_User1Id_fkey",
                        column: x => x.usr_User1Id,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                    table.ForeignKey(
                        name: "Conversations_usr_User2Id_fkey",
                        column: x => x.usr_User2Id,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateTable(
                name: "ESignDocuments",
                columns: table => new
                {
                    eDoc_ESignDocumentsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    eTpl_ESignTemplatesId = table.Column<Guid>(type: "uuid", nullable: false),
                    jp_JobPostsId = table.Column<Guid>(type: "uuid", nullable: false),
                    cont_ContractsId = table.Column<Guid>(type: "uuid", nullable: true),
                    DocumentCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RenderedHtmlContent = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0, comment: "Enum ESignDocumentStatus: 0=Draft, 1=PendingSignatures, 2=PartiallySigned, 3=FullySigned, 4=Expired, 5=Voided"),
                    DocumentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExportedPdfUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ESignDocuments_pkey", x => x.eDoc_ESignDocumentsId);
                    table.ForeignKey(
                        name: "ESignDocuments_cont_ContractsId_fkey",
                        column: x => x.cont_ContractsId,
                        principalTable: "Contracts",
                        principalColumn: "cont_ContractsId");
                    table.ForeignKey(
                        name: "ESignDocuments_eTpl_ESignTemplatesId_fkey",
                        column: x => x.eTpl_ESignTemplatesId,
                        principalTable: "ESignTemplates",
                        principalColumn: "eTpl_ESignTemplatesId");
                    table.ForeignKey(
                        name: "ESignDocuments_jp_JobPostsId_fkey",
                        column: x => x.jp_JobPostsId,
                        principalTable: "JobPosts",
                        principalColumn: "jp_JobPostsId");
                });

            migrationBuilder.CreateTable(
                name: "Milestones",
                columns: table => new
                {
                    mStone_MilestonesId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    cont_ContractsId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Enum MilestoneStatus: 0=Pending, 1=InProgress, 2=Submitted, 3=Approved, 4=PaymentProofUploaded, 5=PaymentConfirmed, 6=Disputed"),
                    SortOrder = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Milestones_pkey", x => x.mStone_MilestonesId);
                    table.ForeignKey(
                        name: "Milestones_cont_ContractsId_fkey",
                        column: x => x.cont_ContractsId,
                        principalTable: "Contracts",
                        principalColumn: "cont_ContractsId");
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    rev_ReviewsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    cont_ContractsId = table.Column<Guid>(type: "uuid", nullable: false),
                    usr_ReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    usr_RevieweeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    CommunicationRating = table.Column<int>(type: "integer", nullable: true),
                    QualityRating = table.Column<int>(type: "integer", nullable: true),
                    TimelinessRating = table.Column<int>(type: "integer", nullable: true),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: true, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Reviews_pkey", x => x.rev_ReviewsId);
                    table.ForeignKey(
                        name: "Reviews_cont_ContractsId_fkey",
                        column: x => x.cont_ContractsId,
                        principalTable: "Contracts",
                        principalColumn: "cont_ContractsId");
                    table.ForeignKey(
                        name: "Reviews_usr_RevieweeId_fkey",
                        column: x => x.usr_RevieweeId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                    table.ForeignKey(
                        name: "Reviews_usr_ReviewerId_fkey",
                        column: x => x.usr_ReviewerId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateTable(
                name: "AIMessages",
                columns: table => new
                {
                    aiMsg_AIMessagesId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    aiSess_AIConversationSessionsId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    TokensUsed = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    SortOrder = table.Column<int>(type: "integer", nullable: true, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("AIMessages_pkey", x => x.aiMsg_AIMessagesId);
                    table.ForeignKey(
                        name: "AIMessages_aiSess_AIConversationSessionsId_fkey",
                        column: x => x.aiSess_AIConversationSessionsId,
                        principalTable: "AIConversationSessions",
                        principalColumn: "aiSess_AIConversationSessionsId");
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    msg_MessagesId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    conv_ConversationsId = table.Column<Guid>(type: "uuid", nullable: false),
                    usr_SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: true, defaultValue: 0, comment: "Enum MessageType: 0=Text, 1=File, 2=System"),
                    IsRead = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    IsEdited = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Messages_pkey", x => x.msg_MessagesId);
                    table.ForeignKey(
                        name: "Messages_conv_ConversationsId_fkey",
                        column: x => x.conv_ConversationsId,
                        principalTable: "Conversations",
                        principalColumn: "conv_ConversationsId");
                    table.ForeignKey(
                        name: "Messages_usr_SenderId_fkey",
                        column: x => x.usr_SenderId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateTable(
                name: "ESignAuditTrails",
                columns: table => new
                {
                    eAudit_ESignAuditTrailsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    eDoc_ESignDocumentsId = table.Column<Guid>(type: "uuid", nullable: false),
                    usr_UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false, comment: "Enum ESignAuditAction: 0=DocumentCreated, 1=DocumentViewed, 2=SignatureAdded, 3=SignatureDeclined, 4=DocumentFinalized, 5=DocumentExported, 6=DocumentVoided"),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("ESignAuditTrails_pkey", x => x.eAudit_ESignAuditTrailsId);
                    table.ForeignKey(
                        name: "ESignAuditTrails_eDoc_ESignDocumentsId_fkey",
                        column: x => x.eDoc_ESignDocumentsId,
                        principalTable: "ESignDocuments",
                        principalColumn: "eDoc_ESignDocumentsId");
                    table.ForeignKey(
                        name: "ESignAuditTrails_usr_UserId_fkey",
                        column: x => x.usr_UserId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateTable(
                name: "ESignSignatures",
                columns: table => new
                {
                    eSig_ESignSignaturesId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    eDoc_ESignDocumentsId = table.Column<Guid>(type: "uuid", nullable: false),
                    usr_UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignerRole = table.Column<int>(type: "integer", nullable: false, comment: "Enum ESignerRole: 0=Client, 1=Freelancer"),
                    SignatureImageUrl = table.Column<string>(type: "text", nullable: false),
                    SignatureWidth = table.Column<int>(type: "integer", nullable: true),
                    SignatureHeight = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0, comment: "Enum ESignSignatureStatus: 0=Pending, 1=Signed, 2=Declined"),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeclinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeclineReason = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("ESignSignatures_pkey", x => x.eSig_ESignSignaturesId);
                    table.ForeignKey(
                        name: "ESignSignatures_eDoc_ESignDocumentsId_fkey",
                        column: x => x.eDoc_ESignDocumentsId,
                        principalTable: "ESignDocuments",
                        principalColumn: "eDoc_ESignDocumentsId");
                    table.ForeignKey(
                        name: "ESignSignatures_usr_UserId_fkey",
                        column: x => x.usr_UserId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateTable(
                name: "Disputes",
                columns: table => new
                {
                    disp_DisputesId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    cont_ContractsId = table.Column<Guid>(type: "uuid", nullable: false),
                    usr_InitiatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    mStone_MilestonesId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Enum DisputeStatus: 0=Open, 1=UnderReview, 2=Resolved, 3=Closed"),
                    Resolution = table.Column<int>(type: "integer", nullable: true, comment: "Enum DisputeResolution: 0=ClientFavored, 1=FreelancerFavored, 2=Split, 3=Dismissed"),
                    ResolutionNote = table.Column<string>(type: "text", nullable: true),
                    ResolvedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("Disputes_pkey", x => x.disp_DisputesId);
                    table.ForeignKey(
                        name: "Disputes_ResolvedByAdminId_fkey",
                        column: x => x.ResolvedByAdminId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                    table.ForeignKey(
                        name: "Disputes_cont_ContractsId_fkey",
                        column: x => x.cont_ContractsId,
                        principalTable: "Contracts",
                        principalColumn: "cont_ContractsId");
                    table.ForeignKey(
                        name: "Disputes_mStone_MilestonesId_fkey",
                        column: x => x.mStone_MilestonesId,
                        principalTable: "Milestones",
                        principalColumn: "mStone_MilestonesId");
                    table.ForeignKey(
                        name: "Disputes_usr_InitiatorId_fkey",
                        column: x => x.usr_InitiatorId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateTable(
                name: "MilestoneAttachments",
                columns: table => new
                {
                    mStoneAttach_MilestoneAttachmentsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    mStone_MilestonesId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("MilestoneAttachments_pkey", x => x.mStoneAttach_MilestoneAttachmentsId);
                    table.ForeignKey(
                        name: "MilestoneAttachments_UploadedByUserId_fkey",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                    table.ForeignKey(
                        name: "MilestoneAttachments_mStone_MilestonesId_fkey",
                        column: x => x.mStone_MilestonesId,
                        principalTable: "Milestones",
                        principalColumn: "mStone_MilestonesId");
                });

            migrationBuilder.CreateTable(
                name: "PaymentProofs",
                columns: table => new
                {
                    pp_PaymentProofsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    mStone_MilestonesId = table.Column<Guid>(type: "uuid", nullable: false),
                    usr_UploadedById = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0, comment: "Enum PaymentProofStatus: 0=Pending, 1=Confirmed, 2=Disputed"),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DisputedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PaymentProofs_pkey", x => x.pp_PaymentProofsId);
                    table.ForeignKey(
                        name: "PaymentProofs_mStone_MilestonesId_fkey",
                        column: x => x.mStone_MilestonesId,
                        principalTable: "Milestones",
                        principalColumn: "mStone_MilestonesId");
                    table.ForeignKey(
                        name: "PaymentProofs_usr_UploadedById_fkey",
                        column: x => x.usr_UploadedById,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateTable(
                name: "MessageAttachments",
                columns: table => new
                {
                    msgAttach_MessageAttachmentsId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    msg_MessagesId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("MessageAttachments_pkey", x => x.msgAttach_MessageAttachmentsId);
                    table.ForeignKey(
                        name: "MessageAttachments_msg_MessagesId_fkey",
                        column: x => x.msg_MessagesId,
                        principalTable: "Messages",
                        principalColumn: "msg_MessagesId");
                });

            migrationBuilder.CreateTable(
                name: "DisputeEvidence",
                columns: table => new
                {
                    dispEv_DisputeEvidenceId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    disp_DisputesId = table.Column<Guid>(type: "uuid", nullable: false),
                    usr_UploadedById = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("DisputeEvidence_pkey", x => x.dispEv_DisputeEvidenceId);
                    table.ForeignKey(
                        name: "DisputeEvidence_disp_DisputesId_fkey",
                        column: x => x.disp_DisputesId,
                        principalTable: "Disputes",
                        principalColumn: "disp_DisputesId");
                    table.ForeignKey(
                        name: "DisputeEvidence_usr_UploadedById_fkey",
                        column: x => x.usr_UploadedById,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateTable(
                name: "DisputeMessages",
                columns: table => new
                {
                    dispMsg_DisputeMessagesId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    disp_DisputesId = table.Column<Guid>(type: "uuid", nullable: false),
                    usr_SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("DisputeMessages_pkey", x => x.dispMsg_DisputeMessagesId);
                    table.ForeignKey(
                        name: "DisputeMessages_disp_DisputesId_fkey",
                        column: x => x.disp_DisputesId,
                        principalTable: "Disputes",
                        principalColumn: "disp_DisputesId");
                    table.ForeignKey(
                        name: "DisputeMessages_usr_SenderId_fkey",
                        column: x => x.usr_SenderId,
                        principalTable: "Users",
                        principalColumn: "usr_UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_Action",
                table: "AdminAuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_AdminId",
                table: "AdminAuditLogs",
                column: "usr_AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_CreatedAt",
                table: "AdminAuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_EntityId_EntityType",
                table: "AdminAuditLogs",
                columns: new[] { "EntityId", "EntityType" });

            migrationBuilder.CreateIndex(
                name: "IX_AIConversationSessions_ContractsId",
                table: "AIConversationSessions",
                column: "cont_ContractsId");

            migrationBuilder.CreateIndex(
                name: "IX_AIConversationSessions_jp_JobPostsId",
                table: "AIConversationSessions",
                column: "jp_JobPostsId");

            migrationBuilder.CreateIndex(
                name: "IX_AIConversationSessions_Type",
                table: "AIConversationSessions",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_AIConversationSessions_UserId",
                table: "AIConversationSessions",
                column: "usr_UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AIConversationSessions_UserId_Type",
                table: "AIConversationSessions",
                columns: new[] { "usr_UserId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_AIInterviewQuestions_SessionId_SortOrder",
                table: "AIInterviewQuestions",
                columns: new[] { "aiIntv_AIInterviewSessionsId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "AIInterviewSessions_jp_JobPostsId_flPro_FreelancerProfilesI_key",
                table: "AIInterviewSessions",
                columns: new[] { "jp_JobPostsId", "flPro_FreelancerProfilesId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AIInterviewSessions_clPro_ClientProfilesId",
                table: "AIInterviewSessions",
                column: "clPro_ClientProfilesId");

            migrationBuilder.CreateIndex(
                name: "IX_AIInterviewSessions_FreelancerProfilesId",
                table: "AIInterviewSessions",
                column: "flPro_FreelancerProfilesId");

            migrationBuilder.CreateIndex(
                name: "IX_AIInterviewSessions_JobPostsId",
                table: "AIInterviewSessions",
                column: "jp_JobPostsId");

            migrationBuilder.CreateIndex(
                name: "IX_AIInterviewSessions_propo_ProposalsId",
                table: "AIInterviewSessions",
                column: "propo_ProposalsId");

            migrationBuilder.CreateIndex(
                name: "IX_AIInterviewSessions_Status",
                table: "AIInterviewSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AIMessages_SessionId_SortOrder",
                table: "AIMessages",
                columns: new[] { "aiSess_AIConversationSessionsId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_IsActive",
                table: "Categories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentCategoryId",
                table: "Categories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Certifications_FreelancerId",
                table: "Certifications",
                column: "fl_FreelancerId");

            migrationBuilder.CreateIndex(
                name: "ClientProfiles_usr_UserId_key",
                table: "ClientProfiles",
                column: "usr_UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientProfiles_UserId",
                table: "ClientProfiles",
                column: "usr_UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "Contracts_propo_ProposalsId_key",
                table: "Contracts",
                column: "propo_ProposalsId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ClientProfilesId",
                table: "Contracts",
                column: "clPro_ClientProfilesId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ClientProfilesId_Status",
                table: "Contracts",
                columns: new[] { "clPro_ClientProfilesId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_FreelancerProfilesId",
                table: "Contracts",
                column: "flPro_FreelancerProfilesId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_FreelancerProfilesId_Status",
                table: "Contracts",
                columns: new[] { "flPro_FreelancerProfilesId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_JobPostsId",
                table: "Contracts",
                column: "jp_JobPostsId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_Status",
                table: "Contracts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "Conversations_usr_User1Id_usr_User2Id_cont_ContractsId_key",
                table: "Conversations",
                columns: new[] { "usr_User1Id", "usr_User2Id", "cont_ContractsId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ContractsId",
                table: "Conversations",
                column: "cont_ContractsId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_LastMessageAt",
                table: "Conversations",
                column: "LastMessageAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_User1Id",
                table: "Conversations",
                column: "usr_User1Id");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_User2Id",
                table: "Conversations",
                column: "usr_User2Id");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeEvidence_DisputesId",
                table: "DisputeEvidence",
                column: "disp_DisputesId");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeEvidence_usr_UploadedById",
                table: "DisputeEvidence",
                column: "usr_UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_DisputeMessages_DisputesId_CreatedAt",
                table: "DisputeMessages",
                columns: new[] { "disp_DisputesId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DisputeMessages_usr_SenderId",
                table: "DisputeMessages",
                column: "usr_SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_ContractsId",
                table: "Disputes",
                column: "cont_ContractsId");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_InitiatorId",
                table: "Disputes",
                column: "usr_InitiatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_mStone_MilestonesId",
                table: "Disputes",
                column: "mStone_MilestonesId");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_ResolvedByAdminId",
                table: "Disputes",
                column: "ResolvedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_Status",
                table: "Disputes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Educations_FreelancerId",
                table: "Educations",
                column: "fl_FreelancerId");

            migrationBuilder.CreateIndex(
                name: "IX_ESignAuditTrails_Action",
                table: "ESignAuditTrails",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_ESignAuditTrails_DocId_CreatedAt",
                table: "ESignAuditTrails",
                columns: new[] { "eDoc_ESignDocumentsId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ESignAuditTrails_UserId",
                table: "ESignAuditTrails",
                column: "usr_UserId");

            migrationBuilder.CreateIndex(
                name: "ESignDocuments_cont_ContractsId_key",
                table: "ESignDocuments",
                column: "cont_ContractsId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ESignDocuments_DocumentCode",
                table: "ESignDocuments",
                column: "DocumentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ESignDocuments_eTpl_ESignTemplatesId",
                table: "ESignDocuments",
                column: "eTpl_ESignTemplatesId");

            migrationBuilder.CreateIndex(
                name: "IX_ESignDocuments_JobPostsId",
                table: "ESignDocuments",
                column: "jp_JobPostsId");

            migrationBuilder.CreateIndex(
                name: "IX_ESignDocuments_Status",
                table: "ESignDocuments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ESignDocuments_Status_CreatedAt",
                table: "ESignDocuments",
                columns: new[] { "Status", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ESignSignatures_eDoc_ESignDocumentsId_usr_UserId_key",
                table: "ESignSignatures",
                columns: new[] { "eDoc_ESignDocumentsId", "usr_UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ESignSignatures_DocId_Status",
                table: "ESignSignatures",
                columns: new[] { "eDoc_ESignDocumentsId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ESignSignatures_ESignDocumentsId",
                table: "ESignSignatures",
                column: "eDoc_ESignDocumentsId");

            migrationBuilder.CreateIndex(
                name: "IX_ESignSignatures_Status",
                table: "ESignSignatures",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ESignSignatures_UserId",
                table: "ESignSignatures",
                column: "usr_UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ESignTemplates_CreatedBy",
                table: "ESignTemplates",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ESignTemplates_IsActive",
                table: "ESignTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ESignTemplates_Name",
                table: "ESignTemplates",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_FAQCategories_Slug",
                table: "FAQCategories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FAQs_FAQCategoriesId",
                table: "FAQs",
                column: "faqCat_FAQCategoriesId");

            migrationBuilder.CreateIndex(
                name: "IX_FAQs_IsActive",
                table: "FAQs",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "FreelancerProfiles_usr_UserId_key",
                table: "FreelancerProfiles",
                column: "usr_UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerProfiles_Availability",
                table: "FreelancerProfiles",
                column: "Availability");

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerProfiles_ExperienceLevel",
                table: "FreelancerProfiles",
                column: "ExperienceLevel");

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerProfiles_UserId",
                table: "FreelancerProfiles",
                column: "usr_UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "FreelancerSkills_fl_FreelancerId_sk_SkillsId_key",
                table: "FreelancerSkills",
                columns: new[] { "fl_FreelancerId", "sk_SkillsId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerSkills_FreelancerId",
                table: "FreelancerSkills",
                column: "fl_FreelancerId");

            migrationBuilder.CreateIndex(
                name: "IX_FreelancerSkills_SkillsId",
                table: "FreelancerSkills",
                column: "sk_SkillsId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPostAttachments_JobPostsId",
                table: "JobPostAttachments",
                column: "jp_JobPostsId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPosts_EndDate",
                table: "JobPosts",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_JobPosts_CategoryId",
                table: "JobPosts",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPosts_ClientProfilesId",
                table: "JobPosts",
                column: "clPro_ClientProfilesId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPosts_CreatedAt",
                table: "JobPosts",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_JobPosts_Status",
                table: "JobPosts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_JobPosts_Status_Visibility",
                table: "JobPosts",
                columns: new[] { "Status", "Visibility" });

            migrationBuilder.CreateIndex(
                name: "IX_JobPosts_Status_Visibility_CreatedAt",
                table: "JobPosts",
                columns: new[] { "Status", "Visibility", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_JobPostSkills_JobPostsId",
                table: "JobPostSkills",
                column: "jp_JobPostsId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPostSkills_SkillsId",
                table: "JobPostSkills",
                column: "sk_SkillsId");

            migrationBuilder.CreateIndex(
                name: "JobPostSkills_jp_JobPostsId_sk_SkillsId_key",
                table: "JobPostSkills",
                columns: new[] { "jp_JobPostsId", "sk_SkillsId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageAttachments_MessagesId",
                table: "MessageAttachments",
                column: "msg_MessagesId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationsId_CreatedAt",
                table: "Messages",
                columns: new[] { "conv_ConversationsId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_IsRead",
                table: "Messages",
                column: "IsRead");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderId",
                table: "Messages",
                column: "usr_SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_MilestoneAttachments_MilestonesId",
                table: "MilestoneAttachments",
                column: "mStone_MilestonesId");

            migrationBuilder.CreateIndex(
                name: "IX_MilestoneAttachments_UploadedByUserId",
                table: "MilestoneAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Milestones_ContractsId",
                table: "Milestones",
                column: "cont_ContractsId");

            migrationBuilder.CreateIndex(
                name: "IX_Milestones_ContractsId_SortOrder",
                table: "Milestones",
                columns: new[] { "cont_ContractsId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Milestones_Status",
                table: "Milestones",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ReferenceId_ReferenceType",
                table: "Notifications",
                columns: new[] { "ReferenceId", "ReferenceType" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_CreatedAt",
                table: "Notifications",
                columns: new[] { "usr_UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications",
                columns: new[] { "usr_UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProofs_MilestonesId",
                table: "PaymentProofs",
                column: "mStone_MilestonesId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProofs_Status",
                table: "PaymentProofs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProofs_UploadedById",
                table: "PaymentProofs",
                column: "usr_UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformSettings_Key",
                table: "PlatformSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformSettings_UpdatedByAdminId",
                table: "PlatformSettings",
                column: "UpdatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioItems_CategoryId",
                table: "PortfolioItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioItems_FreelancerId",
                table: "PortfolioItems",
                column: "fl_FreelancerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalAttachments_ProposalsId",
                table: "ProposalAttachments",
                column: "propo_ProposalsId");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_FreelancerProfilesId",
                table: "Proposals",
                column: "flPro_FreelancerProfilesId");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_FreelancerProfilesId_Status",
                table: "Proposals",
                columns: new[] { "flPro_FreelancerProfilesId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_JobPostsId",
                table: "Proposals",
                column: "jp_JobPostsId");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_JobPostsId_Status",
                table: "Proposals",
                columns: new[] { "jp_JobPostsId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_Status",
                table: "Proposals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "Proposals_jp_JobPostsId_flPro_FreelancerProfilesId_key",
                table: "Proposals",
                columns: new[] { "jp_JobPostsId", "flPro_FreelancerProfilesId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ExpiresAt",
                table: "RefreshTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "usr_UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReportedEntityId_ReportedEntityType",
                table: "Reports",
                columns: new[] { "ReportedEntityId", "ReportedEntityType" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReporterId",
                table: "Reports",
                column: "usr_ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ResolvedByAdminId",
                table: "Reports",
                column: "ResolvedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_Status",
                table: "Reports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ContractsId",
                table: "Reviews",
                column: "cont_ContractsId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_RevieweeId",
                table: "Reviews",
                column: "usr_RevieweeId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_RevieweeId_IsVisible",
                table: "Reviews",
                columns: new[] { "usr_RevieweeId", "IsVisible" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewerId",
                table: "Reviews",
                column: "usr_ReviewerId");

            migrationBuilder.CreateIndex(
                name: "Reviews_cont_ContractsId_usr_ReviewerId_key",
                table: "Reviews",
                columns: new[] { "cont_ContractsId", "usr_ReviewerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedFreelancers_FreelancerProfilesId",
                table: "SavedFreelancers",
                column: "flPro_FreelancerProfilesId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedFreelancers_UserId",
                table: "SavedFreelancers",
                column: "usr_UserId");

            migrationBuilder.CreateIndex(
                name: "SavedFreelancers_usr_UserId_flPro_FreelancerProfilesId_key",
                table: "SavedFreelancers",
                columns: new[] { "usr_UserId", "flPro_FreelancerProfilesId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedJobs_JobPostsId",
                table: "SavedJobs",
                column: "jp_JobPostsId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedJobs_UserId",
                table: "SavedJobs",
                column: "usr_UserId");

            migrationBuilder.CreateIndex(
                name: "SavedJobs_usr_UserId_jp_JobPostsId_key",
                table: "SavedJobs",
                columns: new[] { "usr_UserId", "jp_JobPostsId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skills_CategoriesId",
                table: "Skills",
                column: "cate_CategoriesId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_IsActive",
                table: "Skills",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_Name",
                table: "Skills",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_IsActive",
                table: "SubscriptionPlans",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_TargetRole",
                table: "SubscriptionPlans",
                column: "TargetRole");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_EndDate",
                table: "Subscriptions",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_SubscriptionPlansId",
                table: "Subscriptions",
                column: "subPlan_SubscriptionPlansId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_UserId_Status",
                table: "Subscriptions",
                columns: new[] { "usr_UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsActive",
                table: "Users",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Role",
                table: "Users",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "IX_WorkExperiences_FreelancerId",
                table: "WorkExperiences",
                column: "fl_FreelancerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAuditLogs");

            migrationBuilder.DropTable(
                name: "AIInterviewQuestions");

            migrationBuilder.DropTable(
                name: "AIMessages");

            migrationBuilder.DropTable(
                name: "Certifications");

            migrationBuilder.DropTable(
                name: "DisputeEvidence");

            migrationBuilder.DropTable(
                name: "DisputeMessages");

            migrationBuilder.DropTable(
                name: "Educations");

            migrationBuilder.DropTable(
                name: "ESignAuditTrails");

            migrationBuilder.DropTable(
                name: "ESignSignatures");

            migrationBuilder.DropTable(
                name: "FAQs");

            migrationBuilder.DropTable(
                name: "FreelancerSkills");

            migrationBuilder.DropTable(
                name: "JobPostAttachments");

            migrationBuilder.DropTable(
                name: "JobPostSkills");

            migrationBuilder.DropTable(
                name: "MessageAttachments");

            migrationBuilder.DropTable(
                name: "MilestoneAttachments");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PaymentProofs");

            migrationBuilder.DropTable(
                name: "PlatformSettings");

            migrationBuilder.DropTable(
                name: "PortfolioItems");

            migrationBuilder.DropTable(
                name: "ProposalAttachments");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "SavedFreelancers");

            migrationBuilder.DropTable(
                name: "SavedJobs");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "WorkExperiences");

            migrationBuilder.DropTable(
                name: "AIInterviewSessions");

            migrationBuilder.DropTable(
                name: "AIConversationSessions");

            migrationBuilder.DropTable(
                name: "Disputes");

            migrationBuilder.DropTable(
                name: "ESignDocuments");

            migrationBuilder.DropTable(
                name: "FAQCategories");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropTable(
                name: "Milestones");

            migrationBuilder.DropTable(
                name: "ESignTemplates");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "Proposals");

            migrationBuilder.DropTable(
                name: "FreelancerProfiles");

            migrationBuilder.DropTable(
                name: "JobPosts");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "ClientProfiles");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
