using Domain.Entities;

namespace Application.Common.InternalServices.Reviews.Models;
public sealed record ReviewModerationResult(Review Review, bool Changed, int EloDelta);
