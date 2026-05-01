using System.Security.Claims;
using inflan_api.DTOs;
using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace inflan_api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class PostScheduleController : ControllerBase
{
    private readonly IScheduledPostService _scheduledPostService;
    private readonly ICampaignRepository _campaignRepo;
    private readonly IInfluencerService _influencerService;

    public PostScheduleController(
        IScheduledPostService scheduledPostService,
        ICampaignRepository campaignRepo,
        IInfluencerService influencerService)
    {
        _scheduledPostService = scheduledPostService;
        _campaignRepo = campaignRepo;
        _influencerService = influencerService;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null ? int.Parse(claim.Value) : 0;
    }

    /// <summary>
    /// List scheduled posts for the current influencer, optionally filtered by
    /// date range and a free-text query (matched ILIKE against title,
    /// description, and the linked campaign's project name).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery(Name = "q")] string? query = null)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var posts = await _scheduledPostService.GetByInfluencerIdAsync(userId, from, to, query);
        return Ok(posts.Select(Serialize));
    }

    /// <summary>Get one scheduled post (must belong to current influencer).</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var userId = GetCurrentUserId();
        var post = await _scheduledPostService.GetByIdAsync(id);
        if (post == null) return NotFound(new { message = "Scheduled post not found" });
        if (post.InfluencerId != userId) return Forbid();

        return Ok(Serialize(post));
    }

    /// <summary>Create a scheduled post for the current influencer.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateScheduledPostDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        // Verify the campaign belongs to this influencer and is confirmed.
        var campaign = await _campaignRepo.GetById(dto.CampaignId);
        if (campaign == null || campaign.InfluencerId != userId)
            return BadRequest(new { message = "You can only schedule posts for your own campaigns." });

        if (!campaign.InfluencerAcceptedAt.HasValue
            || campaign.CampaignStatus == (int)CampaignStatus.REJECTED
            || campaign.CampaignStatus == (int)CampaignStatus.CANCELLED)
        {
            return BadRequest(new { message = "The campaign must be accepted before scheduling posts." });
        }

        var rangeError = ValidateScheduledAtAgainstCampaign(dto.ScheduledAt, campaign);
        if (rangeError != null) return BadRequest(new { message = rangeError });

        var post = new ScheduledPost
        {
            InfluencerId = userId,
            CampaignId = dto.CampaignId,
            Title = dto.Title.Trim(),
            Description = dto.Description,
            ImageUrl = dto.ImageUrl,
            ScheduledAt = dto.ScheduledAt,
            Platforms = NormalizePlatforms(dto.Platforms)
        };

        var created = await _scheduledPostService.CreateAsync(post);
        // Reload with Campaign include for serialization.
        var reloaded = await _scheduledPostService.GetByIdAsync(created.Id);
        return StatusCode(201, Serialize(reloaded ?? created));
    }

    /// <summary>Update an existing scheduled post (must belong to current influencer).</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateScheduledPostDto dto)
    {
        var userId = GetCurrentUserId();
        var post = await _scheduledPostService.GetByIdAsync(id);
        if (post == null) return NotFound(new { message = "Scheduled post not found" });
        if (post.InfluencerId != userId) return Forbid();

        if (dto.CampaignId.HasValue && dto.CampaignId.Value != post.CampaignId)
        {
            var campaign = await _campaignRepo.GetById(dto.CampaignId.Value);
            if (campaign == null || campaign.InfluencerId != userId)
                return BadRequest(new { message = "You can only schedule posts for your own campaigns." });
            post.CampaignId = dto.CampaignId.Value;
        }

        if (!string.IsNullOrWhiteSpace(dto.Title)) post.Title = dto.Title.Trim();
        if (dto.Description != null) post.Description = dto.Description;
        if (dto.ImageUrl != null) post.ImageUrl = dto.ImageUrl;
        if (dto.ScheduledAt.HasValue) post.ScheduledAt = dto.ScheduledAt.Value;
        if (dto.Platforms != null) post.Platforms = NormalizePlatforms(dto.Platforms);
        if (dto.Status.HasValue) post.Status = dto.Status.Value;

        // Whether the campaign or the date changed (or both), the new
        // (scheduledAt, campaign) pair must satisfy the campaign's window.
        var validationCampaign = await _campaignRepo.GetById(post.CampaignId);
        if (validationCampaign != null)
        {
            var rangeError = ValidateScheduledAtAgainstCampaign(post.ScheduledAt, validationCampaign);
            if (rangeError != null) return BadRequest(new { message = rangeError });
        }

        var updated = await _scheduledPostService.UpdateAsync(post);
        var reloaded = await _scheduledPostService.GetByIdAsync(updated.Id);
        return Ok(Serialize(reloaded ?? updated));
    }

    /// <summary>Delete a scheduled post (must belong to current influencer).</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        var post = await _scheduledPostService.GetByIdAsync(id);
        if (post == null) return NotFound(new { message = "Scheduled post not found" });
        if (post.InfluencerId != userId) return Forbid();

        await _scheduledPostService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Campaigns that the current influencer has accepted — used to populate the "campaign" dropdown in the schedule form.</summary>
    [HttpGet("confirmed-campaigns")]
    public async Task<IActionResult> GetConfirmedCampaigns()
    {
        var userId = GetCurrentUserId();
        var campaigns = (await _campaignRepo.GetCampaignsByInfluencerId(userId))
            .Where(c => c.InfluencerAcceptedAt.HasValue
                        && c.CampaignStatus != (int)CampaignStatus.REJECTED
                        && c.CampaignStatus != (int)CampaignStatus.CANCELLED)
            .OrderBy(c => c.ProjectName)
            .Select(c => new
            {
                id = c.Id,
                projectName = c.ProjectName,
                brandName = c.Brand?.BrandName ?? c.Brand?.Name,
                campaignStartDate = c.CampaignStartDate,
                campaignEndDate = c.CampaignEndDate,
                status = c.CampaignStatus
            })
            .ToList();

        return Ok(campaigns);
    }

    /// <summary>Platforms the current influencer has connected (for the platform multi-select).</summary>
    [HttpGet("platforms")]
    public async Task<IActionResult> GetConnectedPlatforms()
    {
        var userId = GetCurrentUserId();
        var influencer = await _influencerService.GetInfluencerBasicByUserId(userId);
        if (influencer == null)
            return Ok(Array.Empty<object>());

        var platforms = new List<object>();
        if (!string.IsNullOrWhiteSpace(influencer.Instagram))
            platforms.Add(new { key = "instagram", label = "Instagram", handle = influencer.Instagram });
        if (!string.IsNullOrWhiteSpace(influencer.YouTube))
            platforms.Add(new { key = "youtube", label = "YouTube", handle = influencer.YouTube });
        if (!string.IsNullOrWhiteSpace(influencer.TikTok))
            platforms.Add(new { key = "tiktok", label = "TikTok", handle = influencer.TikTok });
        if (!string.IsNullOrWhiteSpace(influencer.Facebook))
            platforms.Add(new { key = "facebook", label = "Facebook", handle = influencer.Facebook });

        return Ok(platforms);
    }

    /// <summary>
    /// Returns an error message when <paramref name="scheduledAt"/> falls outside
    /// the campaign's start/end window, or null when the date is acceptable.
    /// We compare on date-only so an end-of-day post on the campaign's last day
    /// is still allowed regardless of timezone shifts.
    /// </summary>
    private static string? ValidateScheduledAtAgainstCampaign(DateTime scheduledAt, Campaign campaign)
    {
        var scheduledDate = DateOnly.FromDateTime(scheduledAt.ToUniversalTime());

        if (scheduledDate < campaign.CampaignStartDate)
        {
            return $"Scheduled date is before the campaign's start date ({campaign.CampaignStartDate:yyyy-MM-dd}).";
        }

        if (scheduledDate > campaign.CampaignEndDate)
        {
            return $"Scheduled date is after the campaign's end date ({campaign.CampaignEndDate:yyyy-MM-dd}).";
        }

        return null;
    }

    private static List<string> NormalizePlatforms(IEnumerable<string> platforms)
    {
        var allowed = new HashSet<string> { "instagram", "youtube", "tiktok", "facebook" };
        return platforms
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim().ToLowerInvariant())
            .Where(allowed.Contains)
            .Distinct()
            .ToList();
    }

    private static object Serialize(ScheduledPost p) => new
    {
        id = p.Id,
        influencerId = p.InfluencerId,
        campaignId = p.CampaignId,
        campaignName = p.Campaign?.ProjectName,
        title = p.Title,
        description = p.Description,
        imageUrl = p.ImageUrl,
        scheduledAt = p.ScheduledAt,
        platforms = p.Platforms,
        status = p.Status,
        createdAt = p.CreatedAt,
        updatedAt = p.UpdatedAt
    };
}
