# Review: ESign Architecture Alignment

## What Was Implemented
- Reorganized `GigBridge/Application/Features/ESign` from mixed `Documents`/`Signatures` and legacy root folders into the repository's role-based feature layout.
- Moved client-owned use cases into `ESign/Client`:
  - `CreateDocumentFromJobPost/Commands`
  - `GetDocumentByJobPost/Queries`
  - `SubmitSignature/{Commands,DTOs}`
- Moved cross-role document lookup into `ESign/Common/GetDocument/Queries`.
- Updated namespaces, controller imports, tests, and workflow guidance to match the corrected structure.
- Removed empty legacy ESign folders after confirming they contained no files.

## Key Design Decisions
- Preserved existing API routes, authorization attributes, handler behavior, DTO contracts, and database access logic.
- Kept shared response DTOs and internal helpers under `ESign/Common`.
- Kept Application-layer dependencies pointed inward through `IApplicationDbContext`, project exceptions, MediatR, FluentValidation, and existing application services.

## Risks / Limitations
- This is an architecture/namespace move only; it does not redesign ESign business rules.
- Existing clients should see no route or response contract change.

## Future Improvements
- Add broader integration coverage for the ESign controller routes if API-level regression tests are expanded.
- Consider aligning route role attributes for job-post ESign reads if product rules require client-only access at the controller level.

## Database Impact
- No schema change is required. Entities, enum values, indexes, relationships, and migrations were not changed.

## 2026-06-18 Update: Template Data and Cloudinary Signatures

## What Was Implemented
- Added backend-owned Cloudinary upload for e-sign signatures.
- Job-post e-sign and contract signing now accept image data URI signature payloads, upload them through `IMediaService`, and persist only the returned Cloudinary URL in `ESignSignatures.SignatureImageUrl`.
- Updated local e-sign seed data to include explicit active templates for `CONTRACT_FIXED_PRICE` and `JOB_POST_CLIENT_COMMITMENT`.
- Added `database/migrations/20260618-ensure-job-post-esign-template.sql` to backfill or reactivate the job-post e-sign template in existing databases.
- Added unit coverage with a fake media service for Cloudinary upload behavior and invalid signature payloads.

## Key Design Decisions
- Kept request DTO names and API routes unchanged for frontend compatibility.
- Put reusable signature upload parsing in Application common services so ESign and Contracts do not depend on each other's feature folders.
- Kept the migration data-only and idempotent; it does not change schema.

## Risks / Limitations
- Signature submission now requires Cloudinary configuration in environments where real uploads occur.
- Existing data URI signatures already saved in the database are not migrated to Cloudinary URLs by this change.
- The backend accepts image data URI input only for new signature submissions.

## Future Improvements
- Add a maintenance/backfill job if legacy data URI signatures need to be uploaded to Cloudinary later.
- Consider generating signed Cloudinary delivery URLs if private e-sign assets become a requirement.

## Database Impact
- No schema change is required.
- Data migration ensures an active `JOB_POST_CLIENT_COMMITMENT` template exists and uses an existing active admin/user for the required `CreatedBy` foreign key.

## 2026-06-19 Update: Cloudinary Configuration and Submit Stability

## What Was Implemented
- Added typed Cloudinary options in Infrastructure with support for `CLOUDINARY_CLOUD_NAME`, `CLOUDINARY_API_KEY`, and `CLOUDINARY_API_SECRET` environment overrides.
- Updated media uploads to return a sanitized external-service error when Cloudinary rejects or fails an upload.
- Mapped external-service failures to HTTP 503 instead of leaking provider errors or returning an unhandled 500.
- Removed the duplicate question-save call from the frontend e-sign finalize step; e-sign now only updates job details, prepares/reuses the document, and submits the signature.

## Key Design Decisions
- Kept `POST /api/ESign/signatures` request and response contracts unchanged.
- Preserved the rule that signatures are uploaded to Cloudinary and only the returned URL is stored.
- Environment variables take precedence over appsettings values so real deployment/local credentials can override placeholder config without committing secrets.

## Risks / Limitations
- A valid Cloudinary account is still required for real signature submission.
- Incorrect Cloudinary credentials now surface as a 503 response with a generic message; operational logs retain provider error detail.

## Future Improvements
- Add API-level integration coverage for failed Cloudinary upload responses if controller tests are expanded.
- Move all committed sample provider values to local user secrets or deployment configuration.

## Database Impact
- No schema or data migration is required.

## 2026-06-30 Update: Signed E-Sign Management API

## What Was Implemented
- Added `GET /api/ESign/documents/my-signed` for client and freelancer users to list e-sign documents they have already signed.
- Added a lightweight list response DTO that excludes full rendered HTML and includes document type, title, status, current-user signing metadata, party signing flags, signature count, timestamps, and exported PDF URL.
- Added filtering by document status, document type (`job` or `contract`), search text, and pagination.
- Added unit coverage for client job-post documents, client/freelancer contract documents, exclusion of unsigned documents, role-safe job document filtering, filters, search, and pagination.

## Key Design Decisions
- The query starts from `ESignSignatures` for the current user with `Signed` status, then joins to documents, jobs, and contracts to avoid exposing unrelated e-sign documents.
- Job-post e-sign documents remain visible only to the owning client.
- Contract e-sign documents remain visible only to contract participants.
- Existing detail endpoints remain unchanged; the new endpoint is a list/read model for frontend management pages.

## Risks / Limitations
- The API returns documents the current user has signed, including partially signed documents that may still wait for the other party.
- The endpoint does not create, sign, void, or export documents; it only lists existing signed records.

## Future Improvements
- Add API-level controller tests if the project expands HTTP integration coverage.
- Add frontend tabs for all signed documents versus fully signed documents using the existing `status` filter.

## Database Impact
- No schema or data migration is required.
- Existing indexes on e-sign signatures, documents, job posts, and contracts are sufficient for the initial read model.

## 2026-07-20 Update: DOCX Contract Artifacts and Three-Role Management

## What Was Implemented
- Moved the `1.0-DATN` contract DOCX into Infrastructure as the single embedded template and added OpenXML-based preview/final generation.
- Contract creation now freezes a JSON snapshot. The second valid signature creates a private immutable DOCX artifact, a SHA-256 evidence hash, and two email outbox records in the same database save.
- The outbox sends the DOCX bytes from the database to the Client and Freelancer with a stable Resend idempotency key; missing artifacts retry and eventually dead-letter.
- Added participant and Admin list/search/filter/pagination APIs plus a participant/Admin-protected DOCX download endpoint. Admin deletion is limited to unsigned Draft documents.
- Reused the existing `/contracts/esign` and `/admin/contracts/esign` frontend screen for Client, Freelancer, and Admin, including signature state, sign/wait actions, and authenticated DOCX download.
- Aligned escrow wallet amounts to `1 GigCoin = 1,000 VND`: Client pays 1% at funding, approval releases 80%, Freelancer pays 1% on each release, and the last 20% releases on Client completion or after 72 hours without an active dispute.
- Client and Freelancer contract signatures now require explicit acceptance of policy `1.0-DATN`; the server stores the accepted version/time and includes them in the final evidence hash.

## Key Design Decisions
- Job-post commitment documents remain on their existing one-signature HTML flow.
- Existing fully signed documents without an artifact remain readable as HTML and are not backfilled or emailed.
- Final DOCX bytes stay in PostgreSQL `bytea`; no public Cloudinary artifact URL is created.
- Voided versions are retained and a later contract revision creates a new ESign document.

## API Changes
- `GET /api/ESign/documents/my`
- `GET /api/admin/esign-documents` (`/api/admin/AdminEsignDocuments` remains an alias)
- `GET /api/ESign/documents/{documentId}/download`

## Database Impact
- EF migration: `20260720150107_AddEsignDocxArtifacts`.
- SQL migration: `database/migrations/20260720-add-esign-docx-artifacts.sql`.
- `ESignDocuments` adds snapshot, DOCX bytes, file name, MIME type, and byte size columns.
- Contract-to-ESign changes from one-to-one to one-to-many; the unique contract index becomes a normal index.
- `DeliveryOutboxes.ScheduleId` becomes nullable so the existing retry worker can also deliver finalized contracts.
- `ESignSignatures` adds nullable `PolicyVersion` and `PolicyAcceptedAt` columns; legacy signatures remain valid without backfill.
