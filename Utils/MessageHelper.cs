namespace inflan_api.Utils;

public static class MessageHelper
{
    public static string GetMessageText(Message messageCode)
    {
        return messageCode switch
        {
            Message.EMAIL_ALREADY_REGISTERED => "Email already registered. Please login instead.",
            Message.INVALID_EMAIL_PASSWORD => "Invalid email or password",
            Message.USER_NOT_FOUND => "User not found",
            Message.INFLUENCER_NOT_FOUND => "Influencer not found",
            Message.INFLUENCER_NOT_IN_USER_TABLE => "Influencer not found in user table",
            Message.PLAN_NOT_FOUND => "Plan not found",
            Message.BRAND_INFO_NOT_FILLED => "Please complete your brand profile",
            Message.INFLUENCER_INFO_NOT_FILLED => "Please complete your influencer profile",
            
            Message.INFLUENCER_USER_UPDATE_FAILED => "Failed to update influencer user information",
            Message.INFLUENCER_DELETE_FAILED => "Failed to delete influencer",
            Message.INFLUENCER_UPDATE_FAILED => "Failed to update influencer profile",
            Message.INFLUENCER_USER_DELETE_FAILED => "Failed to delete influencer user",
            Message.USER_UPDATE_FAILED => "Failed to update user information",
            Message.USER_DELETE_FAILED => "Failed to delete user",
            Message.PLAN_UPDATE_FAILED => "Failed to update plan",
            Message.PLAN_DELETE_FAILED => "Failed to delete plan",
            
            Message.USER_CREATED_SUCCESSFULLY => "User created successfully",
            Message.INFLUENCER_CREATED_SUCCESSFULLY => "Social media accounts added successfully",
            Message.PLAN_CREATED_SUCCESSFULLY => "Plan created successfully",
            Message.USER_UPDATED_SUCCESSFULLY => "User updated successfully",
            
            _ => "An error occurred"
        };
    }

    public static string GetMessageCode(Message messageCode)
    {
        return messageCode.ToString();
    }
}