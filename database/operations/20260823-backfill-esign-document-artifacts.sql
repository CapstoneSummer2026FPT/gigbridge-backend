BEGIN;

SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '15min';

-- Serialize repeated operator runs without locking ESign reads/downloads.
SELECT pg_advisory_xact_lock(4400759050002);

INSERT INTO "ESignDocumentArtifacts"
    ("EsignDocumentArtifactId", "EsignDocumentsId", "ArtifactType", "Content",
     "FileName", "MimeType", "SizeBytes", "ContentHashSha256", "ArtifactRevision",
     "CreatedAt", "UpdatedAt")
SELECT gen_random_uuid(), d."ESignDocumentsId", 1, c."FinalizedDocumentContent",
       COALESCE(NULLIF(d."FinalizedDocumentFileName", ''), d."DocumentCode" || '.docx'),
       COALESCE(NULLIF(c."FinalizedDocumentMimeType", ''),
           'application/vnd.openxmlformats-officedocument.wordprocessingml.document'),
       octet_length(c."FinalizedDocumentContent"),
       encode(digest(c."FinalizedDocumentContent", 'sha256'), 'hex'),
       d."ContentRevision", COALESCE(d."UpdatedAt", d."CreatedAt"), d."UpdatedAt"
FROM "ESignDocuments" d
JOIN "ESignDocumentContents" c ON c."EsignDocumentsId" = d."EsignDocumentsId"
WHERE c."FinalizedDocumentContent" IS NOT NULL
  AND octet_length(c."FinalizedDocumentContent") > 0
ON CONFLICT ("EsignDocumentsId", "ArtifactType") DO NOTHING;

INSERT INTO "ESignDocumentArtifacts"
    ("EsignDocumentArtifactId", "EsignDocumentsId", "ArtifactType", "Content",
     "FileName", "MimeType", "SizeBytes", "ContentHashSha256", "ArtifactRevision",
     "CreatedAt", "UpdatedAt")
SELECT gen_random_uuid(), d."ESignDocumentsId", 2, c."PdfDocumentContent",
       COALESCE(NULLIF(c."PdfDocumentFileName", ''), d."DocumentCode" || '.pdf'),
       'application/pdf', octet_length(c."PdfDocumentContent"),
       encode(digest(c."PdfDocumentContent", 'sha256'), 'hex'),
       d."ContentRevision", COALESCE(d."UpdatedAt", d."CreatedAt"), d."UpdatedAt"
FROM "ESignDocuments" d
JOIN "ESignDocumentContents" c ON c."EsignDocumentsId" = d."EsignDocumentsId"
WHERE c."PdfDocumentContent" IS NOT NULL
  AND octet_length(c."PdfDocumentContent") > 0
ON CONFLICT ("EsignDocumentsId", "ArtifactType") DO NOTHING;

COMMIT;

-- This operation performs INSERT ... SELECT entirely inside PostgreSQL. It never
-- returns bytea through EF, the deployment host, or Supavisor.
