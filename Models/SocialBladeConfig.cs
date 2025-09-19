namespace inflan_api.Models
{
    public class SocialBladeConfig
    {
        public const string SectionName = "SocialBlade";
        
        public string ApiKey { get; set; } = ""; // This is clientid
        public string ApiSecret { get; set; } = ""; // This is token
        public string BaseUrl { get; set; } = "https://matrix.sbapis.com/b";
        public string TestUsername { get; set; } = "rick"; // For testing without credits
        public bool UseTestMode { get; set; } = true; // Switch to false for production
        public int TimeoutSeconds { get; set; } = 30;
        public int RetryCount { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 1000;
    }
}