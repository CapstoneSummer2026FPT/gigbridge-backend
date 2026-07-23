-- Private finalized DOCX artifacts and versioned contract ESign documents.

ALTER TABLE "ESignDocuments"
    DROP CONSTRAINT IF EXISTS "ESignDocuments_cont_ContractsId_key";
DROP INDEX IF EXISTS "ESignDocuments_cont_ContractsId_key";

ALTER TABLE "ESignSignatures"
    ADD COLUMN IF NOT EXISTS "PolicyAcceptedAt" timestamptz NULL,
    ADD COLUMN IF NOT EXISTS "PolicyVersion" varchar(50) NULL;

ALTER TABLE "ESignDocuments"
    ADD COLUMN IF NOT EXISTS "ContractSnapshotJson" jsonb NULL,
    ADD COLUMN IF NOT EXISTS "FinalizedDocumentContent" bytea NULL,
    ADD COLUMN IF NOT EXISTS "FinalizedDocumentFileName" varchar(255) NULL,
    ADD COLUMN IF NOT EXISTS "FinalizedDocumentMimeType" varchar(150) NULL,
    ADD COLUMN IF NOT EXISTS "FinalizedDocumentSizeBytes" bigint NULL;

ALTER TABLE "DeliveryOutboxes"
    ALTER COLUMN "ScheduleId" DROP NOT NULL;

CREATE INDEX IF NOT EXISTS "IX_ESignDocuments_ContractsId"
    ON "ESignDocuments" ("ContractsId");
