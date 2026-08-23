BEGIN TRANSACTION READ ONLY ISOLATION LEVEL REPEATABLE READ;

DO $validation$
DECLARE
    mismatch_count bigint;
BEGIN
    SELECT count(*) INTO mismatch_count
    FROM (
        SELECT c."EsignDocumentsId", 1 AS artifact_type,
               octet_length(c."FinalizedDocumentContent") AS expected_size,
               encode(digest(c."FinalizedDocumentContent", 'sha256'), 'hex') AS expected_hash
        FROM "ESignDocumentContents" c
        WHERE c."FinalizedDocumentContent" IS NOT NULL
          AND octet_length(c."FinalizedDocumentContent") > 0
        UNION ALL
        SELECT c."EsignDocumentsId", 2,
               octet_length(c."PdfDocumentContent"),
               encode(digest(c."PdfDocumentContent", 'sha256'), 'hex')
        FROM "ESignDocumentContents" c
        WHERE c."PdfDocumentContent" IS NOT NULL
          AND octet_length(c."PdfDocumentContent") > 0
    ) legacy
    LEFT JOIN "ESignDocumentArtifacts" artifact
      ON artifact."EsignDocumentsId" = legacy."EsignDocumentsId"
     AND artifact."ArtifactType" = legacy.artifact_type
    WHERE artifact."EsignDocumentArtifactId" IS NULL
       OR artifact."SizeBytes" <> legacy.expected_size
       OR artifact."ContentHashSha256" <> legacy.expected_hash;

    IF mismatch_count <> 0 THEN
        RAISE EXCEPTION 'ESign artifact parity failed: % missing or mismatched rows.', mismatch_count;
    END IF;
END
$validation$;

SELECT "ArtifactType", count(*) AS row_count, sum("SizeBytes") AS total_bytes
FROM "ESignDocumentArtifacts"
GROUP BY "ArtifactType"
ORDER BY "ArtifactType";

COMMIT;
