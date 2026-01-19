namespace inflan_api.DTOs
{
    // Dashboard DTOs
    public class DashboardStatsDto
    {
        public int TotalCampaigns { get; set; }
        public int InitiatedCampaigns { get; set; }
        public int CompletedCampaigns { get; set; }
        public int CancelledCampaigns { get; set; }
    }

    public class CampaignBreakdownDto
    {
        public int Low { get; set; }
        public int Medium { get; set; }
        public int High { get; set; }
        public string Month { get; set; } = string.Empty;
    }

    public class PaymentVolumeDataDto
    {
        public decimal Gbp { get; set; }
        public decimal Ngn { get; set; }
        public string Period { get; set; } = string.Empty;
    }

    // User DTOs
    public class AdminUserListDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public int UserType { get; set; }
        public string UserTypeLabel { get; set; } = string.Empty;
        public int Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public string? BrandName { get; set; }
        public string? ProfileImage { get; set; }
        public DateTime CreatedAt { get; set; }
        public int TotalCampaigns { get; set; }
    }

    public class AdminUserDetailDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public int UserType { get; set; }
        public string UserTypeLabel { get; set; } = string.Empty;
        public int Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public string? BrandName { get; set; }
        public string? BrandCategory { get; set; }
        public string? BrandSector { get; set; }
        public string? Goals { get; set; }
        public string? ProfileImage { get; set; }
        public string? Currency { get; set; }
        public string? Location { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<AdminCampaignSummaryDto> Campaigns { get; set; } = new();
        public decimal TotalSpent { get; set; } // For brands
        public decimal TotalEarned { get; set; } // For influencers
    }

    // Campaign DTOs
    public class AdminCampaignListDto
    {
        public int Id { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public int BrandId { get; set; }
        public string BrandName { get; set; } = string.Empty;
        public int? InfluencerId { get; set; }
        public string? InfluencerName { get; set; }
        public int CampaignStatus { get; set; }
        public string CampaignStatusLabel { get; set; } = string.Empty;
        public int PaymentStatus { get; set; }
        public string PaymentStatusLabel { get; set; } = string.Empty;
        public int PaymentType { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public PlanInfoDto? Plan { get; set; }
        public List<string>? ContentFiles { get; set; }
        public List<string>? InstructionDocuments { get; set; }
        public string? SignedContractPdfPath { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class PlanInfoDto
    {
        public int Id { get; set; }
        public string? PlanName { get; set; }
        public string? Currency { get; set; }
        public string? Interval { get; set; }
        public float Price { get; set; }
        public int NumberOfMonths { get; set; }
    }

    public class AdminCampaignSummaryDto
    {
        public int Id { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public int CampaignStatus { get; set; }
        public string CampaignStatusLabel { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Commission Report DTOs
    public class CommissionStatsDto
    {
        public decimal TotalPaidGbp { get; set; }
        public decimal TotalPaidNgn { get; set; }
        public decimal TotalPendingGbp { get; set; }
        public decimal TotalPendingNgn { get; set; }
        public decimal TotalCommissionGbp { get; set; }
        public decimal TotalCommissionNgn { get; set; }
    }

    public class CommissionReportDto
    {
        public int TransactionId { get; set; }
        public string TransactionReference { get; set; } = string.Empty;
        public int CampaignId { get; set; }
        public string CampaignName { get; set; } = string.Empty;
        public int BrandId { get; set; }
        public string BrandName { get; set; } = string.Empty;
        public int? InfluencerId { get; set; }
        public string? InfluencerName { get; set; }
        public decimal Amount { get; set; }
        public decimal Commission { get; set; }
        public string Currency { get; set; } = string.Empty;
        public int Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    // Withdrawal Report DTOs
    public class WithdrawalReportDto
    {
        public int Id { get; set; }
        public int InfluencerId { get; set; }
        public string InfluencerName { get; set; } = string.Empty;
        public string InfluencerEmail { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public int Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public string? BankName { get; set; }
        public string? AccountNumber { get; set; }
        public string? PaymentGateway { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public List<WithdrawalCampaignDto> RelatedCampaigns { get; set; } = new();
    }

    public class WithdrawalCampaignDto
    {
        public int CampaignId { get; set; }
        public string CampaignName { get; set; } = string.Empty;
        public string BrandName { get; set; } = string.Empty;
    }

    // Pagination DTO
    public class PaginatedResponseDto<T>
    {
        public List<T> Data { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
