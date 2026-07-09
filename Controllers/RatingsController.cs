using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.MyDBContext;
using inflan_api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace inflan_api.Controllers;

public class SubmitRatingRequest
{
    public int CampaignId { get; set; }
    public int Stars { get; set; }
    public string? Comment { get; set; }
}

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class RatingsController : ControllerBase
{
    private readonly InflanDBContext _context;
    private readonly INotificationService _notificationService;

    public RatingsController(InflanDBContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("id")?.Value;
        return int.Parse(claim ?? "0");
    }

    // A review pair becomes visible once both parties have rated, or after this window elapses.
    private static readonly TimeSpan ReviewWindow = TimeSpan.FromDays(14);

    /// <summary>
    /// Submit (or update) the current user's rating of the other party on a campaign.
    /// Either party can rate the other once the campaign has been completed.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SubmitRating([FromBody] SubmitRatingRequest request)
    {
        var raterId = GetCurrentUserId();

        if (request.Stars < 1 || request.Stars > 5)
            return BadRequest(new { message = "Stars must be between 1 and 5.", code = "INVALID_STARS" });

        var campaign = await _context.Campaigns.FindAsync(request.CampaignId);
        if (campaign == null)
            return NotFound(new { message = "Campaign not found.", code = "CAMPAIGN_NOT_FOUND" });

        if (campaign.BrandId != raterId && campaign.InfluencerId != raterId)
            return BadRequest(new { message = "You are not a participant in this campaign.", code = "NOT_A_PARTICIPANT" });

        // Only allow rating once the campaign has been marked completed by the brand.
        if (campaign.CampaignStatus != (int)CampaignStatus.COMPLETED)
            return BadRequest(new { message = "You can only rate a completed campaign.", code = "CAMPAIGN_NOT_RATEABLE" });

        var rateeId = campaign.BrandId == raterId ? campaign.InfluencerId : campaign.BrandId;
        var ratee = await _context.Users.FindAsync(rateeId);
        if (ratee == null)
            return NotFound(new { message = "The other party was not found.", code = "RATEE_NOT_FOUND" });

        var existing = await _context.Ratings
            .FirstOrDefaultAsync(r => r.CampaignId == request.CampaignId && r.RaterId == raterId);

        var isNew = existing == null;
        if (existing != null)
        {
            existing.Stars = request.Stars;
            existing.Comment = request.Comment;
            existing.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.Ratings.Add(new Rating
            {
                CampaignId = request.CampaignId,
                RaterId = raterId,
                RateeId = rateeId,
                RateeUserType = ratee.UserType,
                Stars = request.Stars,
                Comment = request.Comment,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        // Notify the rated party that a review was left (kept double-blind — content is not revealed).
        if (isNew)
        {
            var rater = await _context.Users.FindAsync(raterId);
            var raterName = rater?.Name ?? "The other party";
            await _notificationService.CreateCampaignNotificationAsync(
                rateeId,
                campaign.Id,
                campaign.ProjectName,
                NotificationType.CampaignUpdate,
                $"{raterName} left you a review. Leave your review to reveal it.");
        }

        return Ok(new { message = "Rating saved.", rateeId, stars = request.Stars });
    }

    /// <summary>
    /// Get the current user's rating for a campaign, plus double-blind status:
    /// whether the other party has rated and whether the pair is now published.
    /// </summary>
    [HttpGet("campaign/{campaignId}")]
    public async Task<IActionResult> GetMyRating(int campaignId)
    {
        var raterId = GetCurrentUserId();

        var campaign = await _context.Campaigns.FindAsync(campaignId);
        if (campaign == null)
            return NotFound(new { message = "Campaign not found.", code = "CAMPAIGN_NOT_FOUND" });

        var mine = await _context.Ratings
            .FirstOrDefaultAsync(r => r.CampaignId == campaignId && r.RaterId == raterId);

        var counterpartRated = await _context.Ratings
            .AnyAsync(r => r.CampaignId == campaignId && r.RaterId != raterId);

        var windowElapsed = campaign.CompletedAt.HasValue &&
                            DateTime.UtcNow >= campaign.CompletedAt.Value.Add(ReviewWindow);
        var published = counterpartRated || windowElapsed;

        return Ok(new
        {
            rated = mine != null,
            stars = mine?.Stars,
            comment = mine?.Comment,
            createdAt = mine?.CreatedAt,
            counterpartRated,
            published,
            revealsAt = campaign.CompletedAt?.Add(ReviewWindow)
        });
    }

    /// <summary>
    /// The campaigns the current user has already rated (so the UI can hide the "rate" action).
    /// </summary>
    [HttpGet("mine")]
    public async Task<IActionResult> GetMyRatings()
    {
        var raterId = GetCurrentUserId();
        var mine = await _context.Ratings
            .Where(r => r.RaterId == raterId)
            .Select(r => new { r.CampaignId, r.Stars })
            .ToListAsync();

        return Ok(mine);
    }

    /// <summary>
    /// Public rating summary for a user (influencer or brand): average stars, count, and the
    /// list of received reviews. Only <b>published</b> reviews are included — a review pair is
    /// published once both parties have rated the campaign, or after the review window elapses.
    /// </summary>
    [HttpGet("user/{userId}/summary")]
    public async Task<IActionResult> GetUserRatingSummary(int userId)
    {
        var now = DateTime.UtcNow;

        // All ratings this user has received.
        var received = await _context.Ratings
            .Where(r => r.RateeId == userId)
            .Include(r => r.Campaign)
            .ToListAsync();

        // Campaigns on which this user has themselves rated back (their side of the pair exists).
        var ratedBackCampaignIds = (await _context.Ratings
            .Where(r => r.RaterId == userId)
            .Select(r => r.CampaignId)
            .ToListAsync())
            .ToHashSet();

        // A received rating is visible when the user rated back on that campaign (both sides in),
        // or the review window has elapsed since the campaign completed.
        var published = received.Where(r =>
            ratedBackCampaignIds.Contains(r.CampaignId) ||
            (r.Campaign?.CompletedAt is DateTime completedAt && now >= completedAt.Add(ReviewWindow))
        ).ToList();

        var raterIds = published.Select(r => r.RaterId).Distinct().ToList();
        var raters = await _context.Users
            .Where(u => raterIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.ProfileImage })
            .ToDictionaryAsync(u => u.Id);

        var reviews = published
            .OrderByDescending(r => r.CreatedAt)
            .Select(r =>
            {
                raters.TryGetValue(r.RaterId, out var rater);
                return new
                {
                    r.Stars,
                    r.Comment,
                    r.CreatedAt,
                    raterId = r.RaterId,
                    raterName = rater?.Name,
                    raterProfileImage = rater?.ProfileImage,
                    campaignName = r.Campaign?.ProjectName
                };
            })
            .ToList();

        var ratingCount = published.Count;
        var avgRating = ratingCount > 0 ? Math.Round(published.Average(r => r.Stars), 2) : 0d;

        return Ok(new
        {
            userId,
            avgRating,
            ratingCount,
            reviews
        });
    }
}
