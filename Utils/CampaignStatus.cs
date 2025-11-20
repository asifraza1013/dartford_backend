namespace inflan_api.Utils;

public enum CampaignStatus
{
    // Brand creates campaign, waiting for influencer response
    DRAFT = 1,

    // Influencer rejected the campaign
    REJECTED = 2,

    // Influencer accepted, contract generated, waiting for brand to sign
    AWAITING_CONTRACT_SIGNATURE = 3,

    // Brand uploaded signed contract, waiting for influencer to approve
    AWAITING_SIGNATURE_APPROVAL = 4,

    // Influencer approved signed contract, waiting for payment
    AWAITING_PAYMENT = 5,

    // Payment completed, campaign is now active/ongoing
    ACTIVE = 6,

    // Campaign work completed
    COMPLETED = 7,

    // Campaign cancelled by either party
    CANCELLED = 8
}