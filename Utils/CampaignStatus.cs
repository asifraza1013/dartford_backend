namespace inflan_api.Utils;

public enum CampaignStatus
{
    // Brand creates campaign, waiting for influencer response
    DRAFT = 1,

    // Influencer rejected the campaign
    REJECTED = 2,

    // Influencer accepted, contract generated, waiting for brand to sign
    AWAITING_CONTRACT_SIGNATURE = 3,

    // Brand signed contract, waiting for payment
    AWAITING_PAYMENT = 4,

    // Payment completed, campaign is now active/ongoing
    ACTIVE = 5,

    // Campaign work completed
    COMPLETED = 6,

    // Campaign cancelled by either party
    CANCELLED = 7
}