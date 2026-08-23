BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS "ESignDocumentArtifacts" (
    "EsignDocumentArtifactId" uuid NOT NULL DEFAULT gen_random_uuid(),
    "EsignDocumentsId" uuid NOT NULL,
    "ArtifactType" integer NOT NULL,
    "Content" bytea NOT NULL,
    "FileName" varchar(255) NOT NULL,
    "MimeType" varchar(150) NOT NULL,
    "SizeBytes" bigint NOT NULL,
    "ContentHashSha256" character(64) NOT NULL,
    "ArtifactRevision" integer NOT NULL,
    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
    "UpdatedAt" timestamptz NULL,
    CONSTRAINT "ESignDocumentArtifacts_pkey" PRIMARY KEY ("EsignDocumentArtifactId"),
    CONSTRAINT "ESignDocumentArtifacts_eDoc_ESignDocumentsId_fkey"
        FOREIGN KEY ("EsignDocumentsId") REFERENCES "ESignDocuments" ("EsignDocumentsId") ON DELETE CASCADE,
    CONSTRAINT "CK_ESignDocumentArtifacts_ArtifactType" CHECK ("ArtifactType" IN (1, 2)),
    CONSTRAINT "CK_ESignDocumentArtifacts_SizeBytes"
        CHECK ("SizeBytes" = octet_length("Content") AND "SizeBytes" > 0)
);

CREATE UNIQUE INDEX IF NOT EXISTS "ESignDocumentArtifacts_eDoc_Type_key"
    ON "ESignDocumentArtifacts" ("EsignDocumentsId", "ArtifactType");

COMMIT;

-- Schema only. Run database/operations/20260823-backfill-esign-document-artifacts.sql
-- after all pre-dual-write backend instances have drained. Do not run this file in
-- addition to the equivalent EF migration on the same deployment path.
