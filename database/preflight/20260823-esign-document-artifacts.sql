BEGIN TRANSACTION READ ONLY ISOLATION LEVEL REPEATABLE READ;

SELECT
    count(*) FILTER (WHERE c."FinalizedDocumentContent" IS NOT NULL
        AND octet_length(c."FinalizedDocumentContent") > 0) AS legacy_docx_count,
    coalesce(sum(octet_length(c."FinalizedDocumentContent")) FILTER (
        WHERE c."FinalizedDocumentContent" IS NOT NULL), 0) AS legacy_docx_bytes,
    count(*) FILTER (WHERE c."PdfDocumentContent" IS NOT NULL
        AND octet_length(c."PdfDocumentContent") > 0) AS legacy_pdf_count,
    coalesce(sum(octet_length(c."PdfDocumentContent")) FILTER (
        WHERE c."PdfDocumentContent" IS NOT NULL), 0) AS legacy_pdf_bytes
FROM "ESignDocumentContents" c;

SELECT
    count(*) FILTER (WHERE "ArtifactType" = 1) AS artifact_docx_count,
    coalesce(sum("SizeBytes") FILTER (WHERE "ArtifactType" = 1), 0) AS artifact_docx_bytes,
    count(*) FILTER (WHERE "ArtifactType" = 2) AS artifact_pdf_count,
    coalesce(sum("SizeBytes") FILTER (WHERE "ArtifactType" = 2), 0) AS artifact_pdf_bytes
FROM "ESignDocumentArtifacts";

COMMIT;
