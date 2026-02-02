using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using inflan_api.Attributes;
using inflan_api.DTOs;
using inflan_api.MyDBContext;
using inflan_api.Utils;
using inflan_api.Models;
using System.Text;
using System.Globalization;

namespace inflan_api.Controllers
{
    /// <summary>
    /// Admin-only endpoints for managing users, campaigns, commissions, and withdrawals
    /// </summary>
    /// <remarks>
    /// All endpoints require JWT authentication with admin privileges (UserType = 1).
    /// Default admin credentials: admin@dartford.com / Admin@123
    /// </remarks>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [AdminOnly]
    [Produces("application/json")]
    public class AdminController : ControllerBase
    {
        private readonly InflanDBContext _context;

        public AdminController(InflanDBContext context)
        {
            _context = context;
        }

        #region Dashboard APIs

        /// <summary>
        /// Get dashboard statistics (total, initiated, completed, cancelled campaigns)
        /// </summary>
        /// <param name="startDate">Optional start date filter (YYYY-MM-DD)</param>
        /// <param name="endDate">Optional end date filter (YYYY-MM-DD)</param>
        /// <returns>Dashboard statistics</returns>
        /// <response code="200">Returns dashboard statistics</response>
        /// <response code="401">Unauthorized - Invalid or missing JWT token</response>
        /// <response code="403">Forbidden - Admin privileges required</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("dashboard/stats")]
        [ProducesResponseType(typeof(DashboardStatsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDashboardStats(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            try
            {
                var query = _context.Campaigns.AsQueryable();

                // Apply date filters
                if (startDate.HasValue)
                {
                    query = query.Where(c => c.CreatedAt >= startDate.Value);
                }
                if (endDate.HasValue)
                {
                    // Include the entire end date
                    var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(c => c.CreatedAt <= endOfDay);
                }

                var totalCampaigns = await query.CountAsync();
                var initiatedCampaigns = await query.CountAsync(c =>
                    c.CampaignStatus == (int)CampaignStatus.AWAITING_CONTRACT_SIGNATURE ||
                    c.CampaignStatus == (int)CampaignStatus.AWAITING_SIGNATURE_APPROVAL ||
                    c.CampaignStatus == (int)CampaignStatus.AWAITING_PAYMENT ||
                    c.CampaignStatus == (int)CampaignStatus.ACTIVE);
                var completedCampaigns = await query.CountAsync(c => c.CampaignStatus == (int)CampaignStatus.COMPLETED);
                var cancelledCampaigns = await query.CountAsync(c => c.CampaignStatus == (int)CampaignStatus.CANCELLED);

                var stats = new DashboardStatsDto
                {
                    TotalCampaigns = totalCampaigns,
                    InitiatedCampaigns = initiatedCampaigns,
                    CompletedCampaigns = completedCampaigns,
                    CancelledCampaigns = cancelledCampaigns
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to fetch dashboard stats", error = ex.Message });
            }
        }

        /// <summary>
        /// Get campaign breakdown by budget size (low/medium/high) for each month
        /// </summary>
        /// <param name="year">Year to filter (default: current year)</param>
        /// <returns>Monthly campaign breakdown categorized by budget</returns>
        /// <remarks>
        /// Budget categories: Low (£0-500), Medium (£501-1000), High (£1001+)
        /// </remarks>
        /// <response code="200">Returns monthly campaign breakdown</response>
        [HttpGet("dashboard/campaign-breakdown")]
        [ProducesResponseType(typeof(List<CampaignBreakdownDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetCampaignBreakdown([FromQuery] int? year)
        {
            try
            {
                var targetYear = year ?? DateTime.UtcNow.Year;
                var startDate = new DateTime(targetYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var endDate = new DateTime(targetYear, 12, 31, 23, 59, 59, DateTimeKind.Utc);

                var campaigns = await _context.Campaigns
                    .Where(c => c.CreatedAt >= startDate && c.CreatedAt <= endDate)
                    .Select(c => new
                    {
                        c.CreatedAt,
                        c.TotalAmountInPence,
                        c.Currency
                    })
                    .ToListAsync();

                var monthlyBreakdown = new List<CampaignBreakdownDto>();

                for (int month = 1; month <= 12; month++)
                {
                    var monthCampaigns = campaigns.Where(c => c.CreatedAt.Month == month).ToList();

                    // Categorize by amount (low, medium, high)
                    // Low: 0-50000 pence (0-500 currency units)
                    // Medium: 50001-100000 pence (501-1000 currency units)
                    // High: 100001+ pence (1001+ currency units)
                    var low = monthCampaigns.Count(c => c.TotalAmountInPence <= 50000);
                    var medium = monthCampaigns.Count(c => c.TotalAmountInPence > 50000 && c.TotalAmountInPence <= 100000);
                    var high = monthCampaigns.Count(c => c.TotalAmountInPence > 100000);

                    monthlyBreakdown.Add(new CampaignBreakdownDto
                    {
                        Low = low,
                        Medium = medium,
                        High = high,
                        Month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month)
                    });
                }

                return Ok(monthlyBreakdown);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to fetch campaign breakdown", error = ex.Message });
            }
        }

        /// <summary>
        /// Get payment volume chart data split by currency (GBP/NGN) for various time periods
        /// </summary>
        /// <param name="period">Time period: 12months, 3months, 30days, 7days, 24hours (default: 12months)</param>
        /// <returns>Payment volume data split by GBP and NGN</returns>
        /// <response code="200">Returns payment volume data</response>
        /// <response code="400">Invalid period specified</response>
        [HttpGet("dashboard/payment-volume")]
        [ProducesResponseType(typeof(List<PaymentVolumeDataDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPaymentVolume([FromQuery] string period = "12months")
        {
            try
            {
                var now = DateTime.UtcNow;
                DateTime startDate;
                List<PaymentVolumeDataDto> data = new();

                var transactions = await _context.Transactions
                    .Where(t => t.TransactionStatus == (int)PaymentStatus.COMPLETED)
                    .Select(t => new
                    {
                        t.CompletedAt,
                        t.TotalAmountInPence,
                        t.Currency
                    })
                    .ToListAsync();

                switch (period.ToLower())
                {
                    case "12months":
                        startDate = now.AddMonths(-12);
                        var filteredBy12Months = transactions.Where(t => t.CompletedAt >= startDate).ToList();

                        for (int i = 11; i >= 0; i--)
                        {
                            var monthDate = now.AddMonths(-i);
                            var monthTransactions = filteredBy12Months
                                .Where(t => t.CompletedAt.HasValue &&
                                           t.CompletedAt.Value.Year == monthDate.Year &&
                                           t.CompletedAt.Value.Month == monthDate.Month)
                                .ToList();

                            data.Add(new PaymentVolumeDataDto
                            {
                                Gbp = monthTransactions.Where(t => t.Currency == "GBP").Sum(t => t.TotalAmountInPence) / 100m,
                                Ngn = monthTransactions.Where(t => t.Currency == "NGN").Sum(t => t.TotalAmountInPence) / 100m,
                                Period = monthDate.ToString("MMM")
                            });
                        }
                        break;

                    case "3months":
                        startDate = now.AddMonths(-3);
                        var filteredBy3Months = transactions.Where(t => t.CompletedAt >= startDate).ToList();

                        for (int i = 2; i >= 0; i--)
                        {
                            var monthDate = now.AddMonths(-i);
                            var monthTransactions = filteredBy3Months
                                .Where(t => t.CompletedAt.HasValue &&
                                           t.CompletedAt.Value.Year == monthDate.Year &&
                                           t.CompletedAt.Value.Month == monthDate.Month)
                                .ToList();

                            data.Add(new PaymentVolumeDataDto
                            {
                                Gbp = monthTransactions.Where(t => t.Currency == "GBP").Sum(t => t.TotalAmountInPence) / 100m,
                                Ngn = monthTransactions.Where(t => t.Currency == "NGN").Sum(t => t.TotalAmountInPence) / 100m,
                                Period = monthDate.ToString("MMM")
                            });
                        }
                        break;

                    case "30days":
                        startDate = now.AddDays(-30);
                        var filteredBy30Days = transactions.Where(t => t.CompletedAt >= startDate).ToList();

                        for (int i = 29; i >= 0; i -= 5)
                        {
                            var dayDate = now.AddDays(-i);
                            var dayStart = dayDate.Date;
                            var dayEnd = dayStart.AddDays(1);

                            var dayTransactions = filteredBy30Days
                                .Where(t => t.CompletedAt.HasValue &&
                                           t.CompletedAt.Value >= dayStart &&
                                           t.CompletedAt.Value < dayEnd)
                                .ToList();

                            data.Add(new PaymentVolumeDataDto
                            {
                                Gbp = dayTransactions.Where(t => t.Currency == "GBP").Sum(t => t.TotalAmountInPence) / 100m,
                                Ngn = dayTransactions.Where(t => t.Currency == "NGN").Sum(t => t.TotalAmountInPence) / 100m,
                                Period = $"Day {30 - i}"
                            });
                        }
                        break;

                    case "7days":
                        startDate = now.AddDays(-7);
                        var filteredBy7Days = transactions.Where(t => t.CompletedAt >= startDate).ToList();

                        for (int i = 6; i >= 0; i--)
                        {
                            var dayDate = now.AddDays(-i);
                            var dayStart = dayDate.Date;
                            var dayEnd = dayStart.AddDays(1);

                            var dayTransactions = filteredBy7Days
                                .Where(t => t.CompletedAt.HasValue &&
                                           t.CompletedAt.Value >= dayStart &&
                                           t.CompletedAt.Value < dayEnd)
                                .ToList();

                            data.Add(new PaymentVolumeDataDto
                            {
                                Gbp = dayTransactions.Where(t => t.Currency == "GBP").Sum(t => t.TotalAmountInPence) / 100m,
                                Ngn = dayTransactions.Where(t => t.Currency == "NGN").Sum(t => t.TotalAmountInPence) / 100m,
                                Period = dayDate.ToString("ddd")
                            });
                        }
                        break;

                    case "24hours":
                        startDate = now.AddHours(-24);
                        var filteredBy24Hours = transactions.Where(t => t.CompletedAt >= startDate).ToList();

                        for (int i = 20; i >= 0; i -= 4)
                        {
                            var hourDate = now.AddHours(-i);
                            var hourStart = new DateTime(hourDate.Year, hourDate.Month, hourDate.Day, hourDate.Hour, 0, 0, DateTimeKind.Utc);
                            var hourEnd = hourStart.AddHours(1);

                            var hourTransactions = filteredBy24Hours
                                .Where(t => t.CompletedAt.HasValue &&
                                           t.CompletedAt.Value >= hourStart &&
                                           t.CompletedAt.Value < hourEnd)
                                .ToList();

                            data.Add(new PaymentVolumeDataDto
                            {
                                Gbp = hourTransactions.Where(t => t.Currency == "GBP").Sum(t => t.TotalAmountInPence) / 100m,
                                Ngn = hourTransactions.Where(t => t.Currency == "NGN").Sum(t => t.TotalAmountInPence) / 100m,
                                Period = hourStart.ToString("HH:mm")
                            });
                        }
                        break;

                    default:
                        return BadRequest(new { message = "Invalid period. Use: 12months, 3months, 30days, 7days, 24hours" });
                }

                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to fetch payment volume", error = ex.Message });
            }
        }

        /// <summary>
        /// Export campaign data as CSV file with optional date filters
        /// </summary>
        /// <param name="startDate">Optional start date filter (YYYY-MM-DD)</param>
        /// <param name="endDate">Optional end date filter (YYYY-MM-DD)</param>
        /// <returns>CSV file with campaign data</returns>
        /// <response code="200">Returns CSV file download</response>
        [HttpGet("dashboard/export")]
        [Produces("text/csv")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExportDashboard(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            try
            {
                var query = _context.Campaigns
                    .Include(c => c.Brand)
                    .Include(c => c.Influencer)
                    .AsQueryable();

                if (startDate.HasValue)
                {
                    query = query.Where(c => c.CreatedAt >= startDate.Value);
                }
                if (endDate.HasValue)
                {
                    var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(c => c.CreatedAt <= endOfDay);
                }

                var campaigns = await query
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                var csv = new StringBuilder();
                csv.AppendLine("Campaign ID,Project Name,Brand,Influencer,Status,Payment Status,Total Amount,Currency,Created At,Completed At");

                foreach (var campaign in campaigns)
                {
                    csv.AppendLine($"{campaign.Id}," +
                        $"\"{campaign.ProjectName}\"," +
                        $"\"{campaign.Brand?.Name ?? "N/A"}\"," +
                        $"\"{campaign.Influencer?.Name ?? "N/A"}\"," +
                        $"{campaign.CampaignStatus}," +
                        $"{campaign.PaymentStatus}," +
                        $"{campaign.TotalAmountInPence / 100m}," +
                        $"{campaign.Currency}," +
                        $"{campaign.CreatedAt:yyyy-MM-dd HH:mm:ss}," +
                        $"N/A");
                }

                var bytes = Encoding.UTF8.GetBytes(csv.ToString());
                return File(bytes, "text/csv", $"dashboard-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to export dashboard data", error = ex.Message });
            }
        }

        #endregion

        #region User Management APIs

        /// <summary>
        /// Get paginated list of all users (brands and influencers) with filters
        /// </summary>
        /// <param name="userType">Filter by user type: 2=Brand, 3=Influencer</param>
        /// <param name="status">Filter by status: 1=Active, 2=Inactive</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Items per page (default: 20)</param>
        /// <param name="search">Search by name, email, username, or brand name</param>
        /// <returns>Paginated list of users</returns>
        /// <response code="200">Returns paginated user list</response>
        [HttpGet("users")]
        [ProducesResponseType(typeof(PaginatedResponseDto<AdminUserListDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUsers(
            [FromQuery] int? userType,
            [FromQuery] int? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null)
        {
            try
            {
                var query = _context.Users
                    .Where(u => u.UserType != (int)UserType.ADMIN) // Don't show admins
                    .AsQueryable();

                // Apply filters
                if (userType.HasValue)
                {
                    query = query.Where(u => u.UserType == userType.Value);
                }
                if (status.HasValue)
                {
                    query = query.Where(u => u.Status == status.Value);
                }
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchLower = search.ToLower();
                    query = query.Where(u =>
                        u.Name.ToLower().Contains(searchLower) ||
                        u.Email.ToLower().Contains(searchLower) ||
                        (u.UserName != null && u.UserName.ToLower().Contains(searchLower)) ||
                        (u.BrandName != null && u.BrandName.ToLower().Contains(searchLower)));
                }

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var users = await query
                    .OrderByDescending(u => u.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new AdminUserListDto
                    {
                        Id = u.Id,
                        Name = u.Name ?? "",
                        Email = u.Email ?? "",
                        UserName = u.UserName,
                        UserType = u.UserType,
                        UserTypeLabel = u.UserType == 2 ? "Brand" : "Influencer",
                        Status = u.Status,
                        StatusLabel = u.Status == 1 ? "Active" : "Inactive",
                        BrandName = u.BrandName,
                        ProfileImage = u.ProfileImage,
                        CreatedAt = DateTime.UtcNow, // Default since model doesn't have this field
                        TotalCampaigns = u.UserType == 2
                            ? _context.Campaigns.Count(c => c.BrandId == u.Id)
                            : _context.Campaigns.Count(c => c.InfluencerId == u.Id)
                    })
                    .ToListAsync();

                var response = new PaginatedResponseDto<AdminUserListDto>
                {
                    Data = users,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to fetch users", error = ex.Message });
            }
        }

        /// <summary>
        /// Get detailed information about a specific user including campaigns and financial data
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Detailed user information</returns>
        /// <response code="200">Returns user details</response>
        /// <response code="404">User not found</response>
        [HttpGet("users/{id}")]
        [ProducesResponseType(typeof(AdminUserDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetUserDetails(int id)
        {
            try
            {
                var user = await _context.Users
                    .Where(u => u.Id == id)
                    .Select(u => new AdminUserDetailDto
                    {
                        Id = u.Id,
                        Name = u.Name ?? "",
                        Email = u.Email ?? "",
                        UserName = u.UserName,
                        UserType = u.UserType,
                        UserTypeLabel = u.UserType == 2 ? "Brand" : u.UserType == 3 ? "Influencer" : "Admin",
                        Status = u.Status,
                        StatusLabel = u.Status == 1 ? "Active" : "Inactive",
                        BrandName = u.BrandName,
                        BrandCategory = u.BrandCategory,
                        BrandSector = u.BrandSector,
                        Goals = u.Goals != null ? string.Join(", ", u.Goals) : null,
                        ProfileImage = u.ProfileImage,
                        Currency = u.Currency,
                        Location = u.Location,
                        CreatedAt = DateTime.UtcNow, // Default since model doesn't have this field
                        Campaigns = new List<AdminCampaignSummaryDto>(),
                        TotalSpent = 0,
                        TotalEarned = 0
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                // Get campaigns
                if (user.UserType == 2) // BRAND
                {
                    user.Campaigns = await _context.Campaigns
                        .Where(c => c.BrandId == id)
                        .Select(c => new AdminCampaignSummaryDto
                        {
                            Id = c.Id,
                            ProjectName = c.ProjectName,
                            CampaignStatus = c.CampaignStatus,
                            CampaignStatusLabel = ((CampaignStatus)c.CampaignStatus).ToString(),
                            TotalAmount = c.TotalAmountInPence / 100m,
                            CreatedAt = c.CreatedAt
                        })
                        .ToListAsync();

                    user.TotalSpent = await _context.Campaigns
                        .Where(c => c.BrandId == id && c.PaymentStatus == (int)PaymentStatus.COMPLETED)
                        .SumAsync(c => c.PaidAmountInPence) / 100m;
                }
                else if (user.UserType == 3) // INFLUENCER
                {
                    user.Campaigns = await _context.Campaigns
                        .Where(c => c.InfluencerId == id)
                        .Select(c => new AdminCampaignSummaryDto
                        {
                            Id = c.Id,
                            ProjectName = c.ProjectName,
                            CampaignStatus = c.CampaignStatus,
                            CampaignStatusLabel = ((CampaignStatus)c.CampaignStatus).ToString(),
                            TotalAmount = c.TotalAmountInPence / 100m,
                            CreatedAt = c.CreatedAt
                        })
                        .ToListAsync();

                    user.TotalEarned = await _context.Campaigns
                        .Where(c => c.InfluencerId == id)
                        .SumAsync(c => c.ReleasedToInfluencerInPence) / 100m;
                }

                return Ok(user);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to fetch user details", error = ex.Message });
            }
        }

        /// <summary>
        /// Activate or suspend a user account
        /// </summary>
        /// <param name="id">User ID</param>
        /// <param name="request">Status update request (status: 1=Active, 2=Inactive)</param>
        /// <returns>Success message with updated user info</returns>
        /// <response code="200">User status updated successfully</response>
        /// <response code="400">Invalid status value or cannot modify admin</response>
        /// <response code="404">User not found</response>
        [HttpPatch("users/{id}/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateUserStatus(int id, [FromBody] UpdateUserStatusRequest request)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                if (user.UserType == 1) // ADMIN
                {
                    return BadRequest(new { message = "Cannot modify admin user status" });
                }

                if (request.Status < 1 || request.Status > 2)
                {
                    return BadRequest(new { message = "Invalid status value. Use 1 for Active, 2 for Inactive" });
                }

                user.Status = request.Status;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = request.Status == 1 ? "User activated successfully" : "User suspended successfully",
                    user = new
                    {
                        user.Id,
                        user.Name,
                        user.Email,
                        Status = user.Status,
                        StatusLabel = user.Status == 1 ? "Active" : "Inactive"
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to update user status", error = ex.Message });
            }
        }

        #endregion

        #region Campaign Management APIs

        /// <summary>
        /// Get paginated list of all campaigns with filters
        /// </summary>
        /// <param name="status">Filter by campaign status (1-8)</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Items per page (default: 20)</param>
        /// <param name="search">Search by project name, brand name, or influencer name</param>
        /// <returns>Paginated list of campaigns</returns>
        /// <response code="200">Returns paginated campaign list</response>
        [HttpGet("campaigns")]
        [ProducesResponseType(typeof(PaginatedResponseDto<AdminCampaignListDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetCampaigns(
            [FromQuery] int? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null)
        {
            try
            {
                var query = _context.Campaigns
                    .Include(c => c.Brand)
                    .Include(c => c.Influencer)
                    .AsQueryable();

                // Apply filters
                if (status.HasValue)
                {
                    query = query.Where(c => c.CampaignStatus == status.Value);
                }
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchLower = search.ToLower();
                    query = query.Where(c =>
                        c.ProjectName.ToLower().Contains(searchLower) ||
                        (c.Brand != null && c.Brand.Name.ToLower().Contains(searchLower)) ||
                        (c.Influencer != null && c.Influencer.Name.ToLower().Contains(searchLower)));
                }

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                // Get all plan IDs from campaigns
                var planIds = await query.Select(c => c.PlanId).Distinct().ToListAsync();
                var plans = await _context.Plans
                    .Where(p => planIds.Contains(p.Id))
                    .ToListAsync();

                var campaignsList = await query
                    .OrderByDescending(c => c.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var campaigns = campaignsList.Select(c =>
                {
                    var plan = plans.FirstOrDefault(p => p.Id == c.PlanId);
                    return new AdminCampaignListDto
                    {
                        Id = c.Id,
                        ProjectName = c.ProjectName,
                        Description = c.AboutProject,
                        StartDate = c.CampaignStartDate,
                        EndDate = c.CampaignEndDate,
                        BrandId = c.BrandId,
                        BrandName = c.Brand != null ? c.Brand.Name ?? "Unknown" : "Unknown",
                        InfluencerId = c.InfluencerId,
                        InfluencerName = c.Influencer != null ? c.Influencer.Name ?? "Not assigned" : "Not assigned",
                        CampaignStatus = c.CampaignStatus,
                        CampaignStatusLabel = ((CampaignStatus)c.CampaignStatus).ToString(),
                        PaymentStatus = c.PaymentStatus,
                        PaymentStatusLabel = ((PaymentStatus)c.PaymentStatus).ToString(),
                        PaymentType = c.PaymentType,
                        TotalAmount = c.TotalAmountInPence / 100m,
                        PaidAmount = c.PaidAmountInPence / 100m,
                        Currency = c.Currency ?? "GBP",
                        Plan = plan != null ? new PlanInfoDto
                        {
                            Id = plan.Id,
                            PlanName = plan.PlanName,
                            Currency = plan.Currency,
                            Interval = plan.Interval,
                            Price = plan.Price,
                            NumberOfMonths = plan.NumberOfMonths
                        } : null,
                        ContentFiles = c.ContentFiles,
                        InstructionDocuments = c.InstructionDocuments,
                        SignedContractPdfPath = c.SignedContractPdfPath,
                        CreatedAt = c.CreatedAt,
                        CompletedAt = c.PaymentCompletedAt
                    };
                }).ToList();

                var response = new PaginatedResponseDto<AdminCampaignListDto>
                {
                    Data = campaigns,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to fetch campaigns", error = ex.Message });
            }
        }

        #endregion

        #region Commission Report APIs

        /// <summary>
        /// Get aggregated commission statistics split by currency (GBP/NGN)
        /// </summary>
        /// <returns>Commission statistics including total paid, pending, and commission amounts</returns>
        /// <response code="200">Returns commission statistics</response>
        [HttpGet("commission/stats")]
        [ProducesResponseType(typeof(CommissionStatsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetCommissionStats()
        {
            try
            {
                var completedTransactions = await _context.Transactions
                    .Where(t => t.TransactionStatus == (int)PaymentStatus.COMPLETED)
                    .Select(t => new { t.TotalAmountInPence, t.PlatformFeeInPence, t.Currency })
                    .ToListAsync();

                var pendingTransactions = await _context.Transactions
                    .Where(t => t.TransactionStatus == (int)PaymentStatus.PENDING)
                    .Select(t => new { t.TotalAmountInPence, t.PlatformFeeInPence, t.Currency })
                    .ToListAsync();

                var stats = new CommissionStatsDto
                {
                    TotalPaidGbp = completedTransactions
                        .Where(t => t.Currency == "GBP")
                        .Sum(t => t.TotalAmountInPence) / 100m,
                    TotalPaidNgn = completedTransactions
                        .Where(t => t.Currency == "NGN")
                        .Sum(t => t.TotalAmountInPence) / 100m,
                    TotalPendingGbp = pendingTransactions
                        .Where(t => t.Currency == "GBP")
                        .Sum(t => t.TotalAmountInPence) / 100m,
                    TotalPendingNgn = pendingTransactions
                        .Where(t => t.Currency == "NGN")
                        .Sum(t => t.TotalAmountInPence) / 100m,
                    TotalCommissionGbp = completedTransactions
                        .Where(t => t.Currency == "GBP")
                        .Sum(t => t.PlatformFeeInPence) / 100m,
                    TotalCommissionNgn = completedTransactions
                        .Where(t => t.Currency == "NGN")
                        .Sum(t => t.PlatformFeeInPence) / 100m
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to fetch commission stats", error = ex.Message });
            }
        }

        /// <summary>
        /// Get detailed commission report with transaction-level data and filters
        /// </summary>
        /// <param name="minAmount">Minimum transaction amount filter</param>
        /// <param name="maxAmount">Maximum transaction amount filter</param>
        /// <param name="influencerId">Filter by influencer ID</param>
        /// <param name="brandId">Filter by brand ID</param>
        /// <param name="startDate">Start date filter (YYYY-MM-DD)</param>
        /// <param name="endDate">End date filter (YYYY-MM-DD)</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Items per page (default: 20)</param>
        /// <returns>Paginated commission report</returns>
        /// <response code="200">Returns paginated commission report</response>
        [HttpGet("commission/report")]
        [ProducesResponseType(typeof(PaginatedResponseDto<CommissionReportDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetCommissionReport(
            [FromQuery] decimal? minAmount,
            [FromQuery] decimal? maxAmount,
            [FromQuery] int? influencerId,
            [FromQuery] int? brandId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var query = _context.Transactions
                    .Include(t => t.Campaign)
                        .ThenInclude(c => c.Brand)
                    .Include(t => t.Campaign)
                        .ThenInclude(c => c.Influencer)
                    .AsQueryable();

                // Apply filters
                if (minAmount.HasValue)
                {
                    var minAmountInPence = (long)(minAmount.Value * 100);
                    query = query.Where(t => t.TotalAmountInPence >= minAmountInPence);
                }
                if (maxAmount.HasValue)
                {
                    var maxAmountInPence = (long)(maxAmount.Value * 100);
                    query = query.Where(t => t.TotalAmountInPence <= maxAmountInPence);
                }
                if (influencerId.HasValue)
                {
                    query = query.Where(t => t.Campaign != null && t.Campaign.InfluencerId == influencerId.Value);
                }
                if (brandId.HasValue)
                {
                    query = query.Where(t => t.Campaign != null && t.Campaign.BrandId == brandId.Value);
                }
                if (startDate.HasValue)
                {
                    query = query.Where(t => t.CreatedAt >= startDate.Value);
                }
                if (endDate.HasValue)
                {
                    var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(t => t.CreatedAt <= endOfDay);
                }

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var transactions = await query
                    .OrderByDescending(t => t.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => new CommissionReportDto
                    {
                        TransactionId = t.Id,
                        TransactionReference = t.TransactionReference,
                        CampaignId = t.CampaignId,
                        CampaignName = t.Campaign != null ? t.Campaign.ProjectName : "N/A",
                        BrandId = t.Campaign != null ? t.Campaign.BrandId : 0,
                        BrandName = t.Campaign != null && t.Campaign.Brand != null ? t.Campaign.Brand.Name ?? "N/A" : "N/A",
                        InfluencerId = t.Campaign != null ? t.Campaign.InfluencerId : null,
                        InfluencerName = t.Campaign != null && t.Campaign.Influencer != null ? t.Campaign.Influencer.Name ?? "N/A" : "N/A",
                        Amount = t.TotalAmountInPence / 100m,
                        Commission = t.PlatformFeeInPence / 100m,
                        Currency = t.Currency,
                        PaymentGateway = t.Gateway,
                        Status = t.TransactionStatus,
                        StatusLabel = ((PaymentStatus)t.TransactionStatus).ToString(),
                        CreatedAt = t.CreatedAt,
                        CompletedAt = t.CompletedAt
                    })
                    .ToListAsync();

                var response = new PaginatedResponseDto<CommissionReportDto>
                {
                    Data = transactions,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to fetch commission report", error = ex.Message });
            }
        }

        #endregion

        #region Withdrawal Report APIs

        /// <summary>
        /// Get detailed withdrawal report for influencer payouts with filters
        /// </summary>
        /// <param name="influencerId">Filter by influencer ID</param>
        /// <param name="startDate">Start date filter (YYYY-MM-DD)</param>
        /// <param name="endDate">End date filter (YYYY-MM-DD)</param>
        /// <param name="minAmount">Minimum withdrawal amount</param>
        /// <param name="maxAmount">Maximum withdrawal amount</param>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Items per page (default: 20)</param>
        /// <returns>Paginated withdrawal report with related campaigns</returns>
        /// <response code="200">Returns paginated withdrawal report</response>
        [HttpGet("withdrawals")]
        [ProducesResponseType(typeof(PaginatedResponseDto<WithdrawalReportDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetWithdrawals(
            [FromQuery] int? influencerId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] decimal? minAmount,
            [FromQuery] decimal? maxAmount,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var query = _context.Withdrawals
                    .Include(w => w.Influencer)
                    .AsQueryable();

                // Apply filters
                if (influencerId.HasValue)
                {
                    query = query.Where(w => w.InfluencerId == influencerId.Value);
                }
                if (startDate.HasValue)
                {
                    query = query.Where(w => w.CreatedAt >= startDate.Value);
                }
                if (endDate.HasValue)
                {
                    var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(w => w.CreatedAt <= endOfDay);
                }
                if (minAmount.HasValue)
                {
                    var minAmountInPence = (long)(minAmount.Value * 100);
                    query = query.Where(w => w.AmountInPence >= minAmountInPence);
                }
                if (maxAmount.HasValue)
                {
                    var maxAmountInPence = (long)(maxAmount.Value * 100);
                    query = query.Where(w => w.AmountInPence <= maxAmountInPence);
                }

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var withdrawals = await query
                    .OrderByDescending(w => w.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var withdrawalDtos = new List<WithdrawalReportDto>();

                foreach (var withdrawal in withdrawals)
                {
                    // Get related campaigns for this influencer
                    var relatedCampaigns = await _context.Campaigns
                        .Where(c => c.InfluencerId == withdrawal.InfluencerId &&
                               c.CampaignStatus == (int)CampaignStatus.COMPLETED)
                        .Include(c => c.Brand)
                        .Take(5) // Limit to 5 recent campaigns
                        .Select(c => new WithdrawalCampaignDto
                        {
                            CampaignId = c.Id,
                            CampaignName = c.ProjectName,
                            BrandName = c.Brand != null ? c.Brand.Name ?? "Unknown" : "Unknown"
                        })
                        .ToListAsync();

                    withdrawalDtos.Add(new WithdrawalReportDto
                    {
                        Id = withdrawal.Id,
                        InfluencerId = withdrawal.InfluencerId,
                        InfluencerName = withdrawal.Influencer?.Name ?? "Unknown",
                        InfluencerEmail = withdrawal.Influencer?.Email ?? "N/A",
                        Amount = withdrawal.AmountInPence / 100m,
                        Currency = withdrawal.Currency,
                        Status = withdrawal.Status,
                        StatusLabel = ((WithdrawalStatus)withdrawal.Status).ToString(),
                        BankName = withdrawal.BankName,
                        AccountNumber = withdrawal.AccountNumber,
                        PaymentGateway = withdrawal.PaymentGateway,
                        CreatedAt = withdrawal.CreatedAt,
                        CompletedAt = withdrawal.CompletedAt,
                        RelatedCampaigns = relatedCampaigns
                    });
                }

                var response = new PaginatedResponseDto<WithdrawalReportDto>
                {
                    Data = withdrawalDtos,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to fetch withdrawals", error = ex.Message });
            }
        }

        #endregion
    }

    // Request models
    public class UpdateUserStatusRequest
    {
        public int Status { get; set; }
    }
}
