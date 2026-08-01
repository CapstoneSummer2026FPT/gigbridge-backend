using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.IService;
using Application.Features.Reviews.Common;
using Application.Features.Reviews.Common.DTOs;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Reviews.Common.CreateReview.Commands;

public class CreateReviewCommandHandler : IRequestHandler<CreateReviewCommand, ReviewDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly IUserEloService _userEloService;
    private readonly INotificationService _notificationService;

    public CreateReviewCommandHandler(
        IApplicationDbContext context,
        IDateTimeService dateTimeService,
        IUserEloService userEloService,
        INotificationService notificationService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _userEloService = userEloService;
        _notificationService = notificationService;
    }

    public async Task<ReviewDto> Handle(CreateReviewCommand command, CancellationToken cancellationToken)
    {
        var contract = await _context.Set<Contract>()
            .Include(contract => contract.ClientProfiles)
                .ThenInclude(clientProfile => clientProfile.User)
            .Include(contract => contract.FreelancerProfiles)
                .ThenInclude(freelancerProfile => freelancerProfile!.User)
            .FirstOrDefaultAsync(
                contract => contract.ContractsId == command.Request.ContractId,
                cancellationToken);

        if (contract is null)
        {
            throw new NotFoundException("Contract does not exist.");
        }

        if (contract.Status != (int)ContractStatus.Completed)
        {
            throw new BadRequestException("Only completed contracts can be reviewed.");
        }

        var revieweeId = ResolveRevieweeId(contract, command.UserId);
        var alreadyReviewed = await _context.Set<Review>()
            .AnyAsync(
                review => review.ContractsId == contract.ContractsId &&
                    review.ReviewerId == command.UserId,
                cancellationToken);

        if (alreadyReviewed)
        {
            throw new ConflictException("You have already reviewed this contract.");
        }

        var now = _dateTimeService.UtcNow;
        var rating = CalculateOverallRating(command.Request);
        var review = new Review
        {
            ReviewsId = Guid.NewGuid(),
            ContractsId = contract.ContractsId,
            ReviewerId = command.UserId,
            RevieweeId = revieweeId,
            Rating = rating,
            Comment = string.IsNullOrWhiteSpace(command.Request.Comment)
                ? null
                : command.Request.Comment.Trim(),
            CommunicationRating = command.Request.CommunicationRating,
            QualityRating = command.Request.QualityRating,
            TimelinessRating = command.Request.TimelinessRating,
            IsVisible = true,
            CreatedAt = now
        };

        _context.Set<Review>().Add(review);
        await _userEloService.ApplyReviewScoreAsync(
            review.ReviewsId,
            revieweeId,
            review.Rating,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        review.Contracts = contract;
        review.Reviewer = command.UserId == contract.ClientProfiles.UserId
            ? contract.ClientProfiles.User
            : contract.FreelancerProfiles!.User;

        await _notificationService.CreateNotificationAsync(
            revieweeId,
            NotificationType.ReviewReceived,
            "You received a new project review",
            $"{review.Reviewer.FullName} reviewed your work on {contract.Title}.",
            contract.ContractsId,
            nameof(Contract),
            cancellationToken);

        return ReviewProjection.ToDto(review);
    }

    private static int CalculateOverallRating(CreateReviewRequest request)
    {
        var average = (
            request.CommunicationRating!.Value +
            request.QualityRating!.Value +
            request.TimelinessRating!.Value) / 3m;

        return (int)Math.Round(average, MidpointRounding.AwayFromZero);
    }

    private static Guid ResolveRevieweeId(Contract contract, Guid reviewerId)
    {
        if (contract.FreelancerProfiles is null)
        {
            throw new BadRequestException("Contract does not have a freelancer to review.");
        }

        if (contract.ClientProfiles.UserId == reviewerId)
        {
            return contract.FreelancerProfiles.UserId;
        }

        if (contract.FreelancerProfiles.UserId == reviewerId)
        {
            return contract.ClientProfiles.UserId;
        }

        throw new ForbiddenAccessException("You do not have permission to review this contract.");
    }
}
