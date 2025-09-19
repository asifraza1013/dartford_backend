# Social Blade Integration Implementation

## Overview

This document outlines the professional, service-oriented implementation for integrating Social Blade API to fetch follower counts for influencers. The implementation is designed to be easily swappable with other providers.

## Architecture

### 🏗️ Service Architecture
```
┌─────────────────────┐    ┌──────────────────────┐    ┌─────────────────────┐
│  InfluencerController│───▶│ IFollowerCountService│───▶│SocialBladeFollower  │
│                     │    │                      │    │Service              │
└─────────────────────┘    └──────────────────────┘    └─────────────────────┘
                                     │
                                     ▼
                           ┌──────────────────────┐
                           │  Future: Different   │
                           │  Provider Service    │
                           └──────────────────────┘
```

### 🔧 Components

1. **Interface**: `IFollowerCountService` - Abstraction for follower count providers
2. **Implementation**: `SocialBladeFollowerService` - Social Blade specific implementation
3. **Configuration**: `SocialBladeConfig` - Social Blade API configuration
4. **Background Service**: `FollowerSyncBackgroundService` - Weekly sync automation
5. **Models**: `FollowerCountResult` - Standardized response format

## API Endpoints

### 🚀 Create Influencer (Updated)
**Endpoint**: `POST /api/influencer/createNewInfluencer`

**Note**: The `twitter` field now accepts YouTube channel names and fetches YouTube subscriber data via Social Blade API. Data is stored in the same database fields for consistency.

**Request Body**:
```json
{
  "twitter": "youtube_channel_name",
  "instagram": "username", 
  "facebook": "username",
  "tikTok": "username",
  "bio": "Optional bio"
}
```

**Response**:
```json
{
  "message": "Influencer created successfully",
  "influencer": {
    "id": 1,
    "userId": 123,
    "twitter": "youtube_channel_name",
    "instagram": "username",
    "facebook": "username",
    "tikTok": "username",
    "twitterFollower": 50000,
    "instagramFollower": 100000,
    "facebookFollower": 75000,
    "tikTokFollower": 250000,
    "bio": "Bio text"
  }
}
```

## Configuration

### 📋 appsettings.json
```json
{
  "SocialBlade": {
    "ApiKey": "YOUR_SOCIAL_BLADE_API_KEY",
    "ApiSecret": "YOUR_SOCIAL_BLADE_API_SECRET", 
    "BaseUrl": "https://api.socialblade.com/v2",
    "TestUsername": "@socialblade",
    "UseTestMode": true,
    "TimeoutSeconds": 30,
    "RetryCount": 3,
    "RetryDelayMs": 1000
  },
  "FollowerSync": {
    "Enabled": false,
    "DayOfWeek": 0,
    "HourUtc": 2,
    "DelayBetweenSyncsMs": 1000
  }
}
```

## Features

### ✨ Key Features

1. **Multi-Platform Support**: Instagram, Twitter, TikTok, Facebook
2. **Error Handling**: Graceful handling of API failures
3. **Retry Logic**: Exponential backoff with Polly
4. **Test Mode**: Use @socialblade account for testing without credits
5. **Rate Limiting**: Configurable delays between requests
6. **Logging**: Comprehensive logging for monitoring
7. **Background Sync**: Weekly automated sync (ready for future)

### 🔄 Provider Switching

To switch to a different provider:

1. Create new service implementing `IFollowerCountService`
2. Update DI registration in `Program.cs`
3. Update configuration section
4. No changes needed in controllers!

Example:
```csharp
// From:
builder.Services.AddHttpClient<IFollowerCountService, SocialBladeFollowerService>()

// To:
builder.Services.AddHttpClient<IFollowerCountService, NewProviderService>()
```

## Social Blade Specifics

### 🔑 Authentication
- Uses API Key and Secret in headers
- Test mode uses @socialblade account for free testing

### 📊 API Endpoints (Social Blade Pattern)
```
GET /instagram/user/{username}
GET /twitter/user/{username}  
GET /tiktok/user/{username}
GET /facebook/user/{username}
```

### 📈 Response Format (Expected)
```json
{
  "status": "success",
  "data": {
    "followers": 100000,
    "total_views": 1000000,
    "total_likes": 50000,
    "engagement_rate": 3.5,
    "updated_at": "2025-09-17T12:00:00Z"
  }
}
```

## Error Handling

### ⚠️ Error Scenarios
1. **Invalid Username**: Returns error with specific message
2. **API Rate Limit**: Automatic retry with exponential backoff
3. **Network Issues**: HTTP retry policy handles transient failures
4. **Authentication Errors**: Clear error messages for invalid keys

### 📝 Logging
- Request/response logging
- Error tracking with details
- Performance monitoring
- Retry attempt logging

## Testing

### 🧪 Test Configuration
Set `UseTestMode: true` in configuration to use @socialblade test account:
- No API credits consumed
- Returns sample data for testing
- All platforms supported in test mode

### 🔍 Manual Testing
```bash
# Test the endpoint
curl -X POST https://api.inflan.com/api/influencer/createNewInfluencer \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "instagram": "testuser",
    "twitter": "testuser", 
    "tikTok": "testuser",
    "facebook": "testuser"
  }'
```

## Future Enhancements

### 🎯 Planned Features
1. **Sync Logs Table**: Track sync history and changes
2. **YouTube Support**: Add YouTube channel subscriber counts
3. **Cron Job**: Enable weekly automatic sync
4. **Analytics**: Track follower growth over time
5. **Multiple Providers**: Support multiple APIs for redundancy
6. **Webhooks**: Real-time updates when follower counts change

### 📊 Monitoring
- API response times
- Success/failure rates  
- Credit usage tracking
- Error rate monitoring

## Security Notes

### 🔒 Security Best Practices
1. **API Keys**: Store in environment variables or Azure Key Vault
2. **Rate Limiting**: Prevent abuse with built-in delays
3. **Input Validation**: Validate usernames before API calls
4. **Logging**: Don't log sensitive API keys or secrets
5. **HTTPS**: All API communications over HTTPS

## Dependencies

### 📦 Required Packages
- `Microsoft.Extensions.Http.Polly` - HTTP retry policies
- `Microsoft.Extensions.Options` - Configuration binding
- `Microsoft.Extensions.Logging` - Logging abstraction

### 🔧 Service Registration
All services are properly registered in `Program.cs` with dependency injection and HTTP client configuration.

---

**Note**: This implementation uses the @socialblade test account initially. Update the API keys and set `UseTestMode: false` when ready for production use.