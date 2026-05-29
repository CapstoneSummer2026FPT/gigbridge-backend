using System;

namespace Application.Common.Constants
{
    public static class SD
    {
        // ===== Job Types =====
        public const string JobType_FullTime = "Full Time";
        public const string JobType_PartTime = "Part Time";
        public const string JobType_Freelance = "Freelance";
        public const string JobType_Seasonal = "Seasonal";

        // ===== Categories =====
        public const string Category_IT = "IT";
        public const string Category_Retail = "Retail";
        public const string Category_Education = "Education";

        //=================== ORDER STATUS ==================
        public const string OrderStatus_Pending = "Pending";
        public const string OrderStatus_Paid = "Paid";
        public const string OrderStatus_Canceled = "Canceled";

        //=================== ROLES ==================
        public const string Role_Client = "Client";
        public const string Role_Freelancer = "Freelancer";
        public const string Role_Admin = "Admin";

        //=================== EXTERNAL AUTH PROVIDERS ==================
        public const string Provider_Google = "Google";
        public const string Provider_Facebook = "Facebook";

        //============================ PLAN NAMES ==================================
        public const string Plan_Free = "Free";
        public const string Plan_Silver = "Silver";
        public const string Plan_Gold = "Gold";

        //============================ PLAN LIMITS ==================================
        public const int Free_Post_Limit = 5;
        public const int Free_Apply_Limit = 5;
        public const int Silver_Post_Limit = 20;

        //=================== USER STATUS ==================
        public const string UserStatus_Active = "active";
        public const string UserStatus_Suspended = "suspended";

        //=================== SINGLE JOIN TABLE STRINGS ==================
        public const string Join_Admin = "Admin";
        public const string Join_Category = "Category";
        public const string Join_ClientProfile = "ClientProfile";
        public const string Join_ClientProfiles = "ClientProfiles";
        public const string Join_Contract = "Contract";
        public const string Join_Contracts = "Contracts";
        public const string Join_CreatedByNavigation = "CreatedByNavigation";
        public const string Join_EsignDocument = "EsignDocument";
        public const string Join_FreelancerProfile = "FreelancerProfile";
        public const string Join_Initiator = "Initiator";
        public const string Join_ParentCategory = "ParentCategory";
        public const string Join_Reporter = "Reporter";
        public const string Join_ResolvedByAdmin = "ResolvedByAdmin";
        public const string Join_Reviewee = "Reviewee";
        public const string Join_Reviewer = "Reviewer";
        public const string Join_Sender = "Sender";
        public const string Join_UpdatedByAdmin = "UpdatedByAdmin";
        public const string Join_UploadedBy = "UploadedBy";
        public const string Join_UploadedByUser = "UploadedByUser";
        public const string Join_User = "User";
        public const string Join_User1 = "User1";
        public const string Join_User2 = "User2";

        //============================ JOIN COLLECTION TABLE STRING ==================================
        public const string Collection_Join_AdminAuditLogs = "AdminAuditLogs";
        public const string Collection_Join_Contracts = "Contracts";
        public const string Collection_Join_Conversations = "Conversations";
        public const string Collection_Join_ConversationUser1s = "ConversationUser1s";
        public const string Collection_Join_ConversationUser2s = "ConversationUser2s";
        public const string Collection_Join_DisputeEvidences = "DisputeEvidences";
        public const string Collection_Join_DisputeInitiators = "DisputeInitiators";
        public const string Collection_Join_DisputeMessages = "DisputeMessages";
        public const string Collection_Join_DisputeResolvedByAdmins = "DisputeResolvedByAdmins";
        public const string Collection_Join_Disputes = "Disputes";
        public const string Collection_Join_EsignDocuments = "EsignDocuments";
        public const string Collection_Join_EsignSignatures = "EsignSignatures";
        public const string Collection_Join_EsignTemplates = "EsignTemplates";
        public const string Collection_Join_Faqs = "Faqs";
        public const string Collection_Join_FreelancerSkills = "FreelancerSkills";
        public const string Collection_Join_InverseParentCategory = "InverseParentCategory";
        public const string Collection_Join_JobPostAttachments = "JobPostAttachments";
        public const string Collection_Join_JobPosts = "JobPosts";
        public const string Collection_Join_JobPostSkills = "JobPostSkills";
        public const string Collection_Join_MessageAttachments = "MessageAttachments";
        public const string Collection_Join_Messages = "Messages";
        public const string Collection_Join_MilestoneAttachments = "MilestoneAttachments";
        public const string Collection_Join_Milestones = "Milestones";
        public const string Collection_Join_Notifications = "Notifications";
        public const string Collection_Join_PaymentProofs = "PaymentProofs";
        public const string Collection_Join_PlatformSettings = "PlatformSettings";
        public const string Collection_Join_PortfolioItems = "PortfolioItems";
        public const string Collection_Join_ProposalAttachments = "ProposalAttachments";
        public const string Collection_Join_Proposals = "Proposals";
        public const string Collection_Join_RefreshTokens = "RefreshTokens";
        public const string Collection_Join_ReportReporters = "ReportReporters";
        public const string Collection_Join_ReportResolvedByAdmins = "ReportResolvedByAdmins";
        public const string Collection_Join_ReviewReviewees = "ReviewReviewees";
        public const string Collection_Join_ReviewReviewers = "ReviewReviewers";
        public const string Collection_Join_Reviews = "Reviews";
        public const string Collection_Join_SavedFreelancers = "SavedFreelancers";
        public const string Collection_Join_SavedJobs = "SavedJobs";
        public const string Collection_Join_Skills = "Skills";
        public const string Collection_Join_Subscriptions = "Subscriptions";
        public const string Collection_Join_WorkExperiences = "WorkExperiences";

        //============================ HELPER METHODS ==================================
        /// <summary>
        /// Calculates the start of the current 1-month rolling cycle.
        /// All dates (baseDate and return value) are in UTC.
        /// </summary>
        public static DateTime CalculateCycleStart(DateTime? baseDate)
        {
            var now = DateTime.UtcNow;
            if (!baseDate.HasValue) return now.AddMonths(-1);

            var cycleStart = baseDate.Value;

            if (cycleStart <= now)
            {
                // Advance forward until the NEXT cycle would start after 'now'
                while (cycleStart.AddMonths(1) <= now)
                {
                    cycleStart = cycleStart.AddMonths(1);
                }
            }
            else
            {
                // Roll back until cycleStart is in the past or exactly now
                while (cycleStart > now)
                {
                    cycleStart = cycleStart.AddMonths(-1);
                }
            }

            return cycleStart;
        }
    }
}
