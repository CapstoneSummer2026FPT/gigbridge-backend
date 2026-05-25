# Mediator / MediatR Clean Code Rule

## Decision Rule

Use MediatR only when an endpoint represents an explicit application use case
or workflow. Do not introduce MediatR merely to avoid injecting a service into
a controller.

Prefer a direct controller-to-service call for simple CRUD, generic table
maintenance, and plain lookup/list endpoints when no workflow is being
orchestrated.

## Controller Responsibility

Controllers must stay thin:

- Validate HTTP-level input only.
- Map route, body, and query data into a command or query when the endpoint is
  a MediatR use case.
- Call `Mediator.Send(...)` for that command or query.
- Return the HTTP response.
- Do not contain business logic, EF queries, transactions, or authorization
  workflow.

For simple CRUD or plain retrieval endpoints that are intentionally outside
MediatR, controllers may delegate directly to one focused application/service
contract and return the response.

## Requests

Create one request for each real use case.

Good examples:

- `CreateJobPostCommand`
- `ApproveJobPostCommand`
- `BanUserCommand`
- `ResolveDisputeCommand`
- `GetAdminDashboardQuery`
- `ListPendingReportsQuery`

Avoid:

- `GenericCreateCommand<T>`
- `GenericUpdateCommand<T>`
- `GenericDeleteCommand<T>`
- `GenericRepositoryCommand`
- A single handler reused for unrelated business actions

## Commands

- Represent state-changing operations.
- Use imperative names such as `ApproveJobPostCommand` and
  `ResolveReportCommand`.
- Return only what the caller needs: an identifier, DTO, or `Unit`.
- Enforce business rules inside the handler or delegated domain/service layer.

## Queries

- Represent read-only operations.
- Use intent-revealing names such as `GetUserByIdQuery` and
  `ListPendingReportsQuery`.
- Must not change database state.
- May return DTOs directly.
- Prefer optimized read projections rather than loading large entities.

## Handlers

- A handler has one responsibility.
- A handler orchestrates a use case; it must not become a large general-purpose
  service.
- Use domain/services when business logic is shared or complex.
- Use EF Core or repositories for persistence, but do not hide important
  business rules inside repositories.
- Do not inject `IMediator` into handlers unless there is a strong documented
  reason. Avoid hidden handler chains.

## Pipeline Behaviors

Use pipeline behaviors only for cross-cutting concerns:

- Validation
- Logging
- Authorization checks
- Performance measurement
- Transaction handling
- Exception handling

Do not use pipeline behaviors for use-case-specific business logic.

## Validation And Transactions

- Structural validation belongs in validators, for example a required title.
- Business validation belongs in handlers or domain/services, for example
  rejecting an already resolved report.
- Use transaction behavior for commands only when multiple writes must
  succeed or fail together.
- Do not wrap simple queries in transactions.
- Do not save in multiple nested services unless the transaction boundary is
  explicit.

## Structure

Group MediatR requests by feature and use case:

```text
Features/
  JobPosts/
    Approve/
      ApproveJobPostCommand.cs
      ApproveJobPostHandler.cs
      ApproveJobPostValidator.cs
```

## Admin API Application

Use MediatR for these named workflows:

- Get dashboard moderation summary.
- Cancel a violating job post.
- Change review visibility as moderation.
- Start report review and resolve/dismiss a report.
- Start dispute review and resolve a dispute.
- Send an administrative system alert.

Use direct focused services for these plain CRUD/read operations:

- Listing or retrieving job posts, reviews, reports, and disputes.
- Listing or retrieving audit logs.
- FAQ create, read, update, and delete administration.
