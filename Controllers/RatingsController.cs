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

    public RatingsController(InflanDBContext context)
    {
        _context = context;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("id")?.Value;
        return int.Parse(claim ?? "0");
    }

    /// <summary>
    /// Submit (or update) the current user's rating of the other party on a campaign.
    /// Either party can rate the other once the campaign is active or completed.
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

        // Only allow rating once the engagement has actually started.
        if (campaign.CampaignStatus < (int)CampaignStatus.ACTIVE)
            return BadRequest(new { message = "You can only rate active or completed campaigns.", code = "CAMPAIGN_NOT_RATEABLE" });

        var rateeId = campaign.BrandId == raterId ? campaign.InfluencerId : campaign.BrandId;
        var ratee = await _context.Users.FindAsync(rateeId);
        if (ratee == null)
            return NotFound(new { message = "The other party was not found.", code = "RATEE_NOT_FOUND" });

        var existing = await _context.Ratings
            .FirstOrDefaultAsync(r => r.CampaignId == request.CampaignId && r.RaterId == raterId);

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
        return Ok(new { message = "Rating saved.", rateeId, stars = request.Stars });
    }

    /// <summary>
    /// Get the current user's rating for a campaign (null if not yet rated).
    /// </summary>
    [HttpGet("campaign/{campaignId}")]
    public async Task<IActionResult> GetMyRating(int campaignId)
    {
        var raterId = GetCurrentUserId();
        var rating = await _context.Ratings
            .FirstOrDefaultAsync(r => r.CampaignId == campaignId && r.RaterId == raterId);

        if (rating == null) return Ok(new { rated = false });
        return Ok(new { rated = true, rating.Stars, rating.Comment, rating.CreatedAt });
    }
}
