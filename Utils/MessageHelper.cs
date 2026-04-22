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
            Message.EMAIL_NOT_VERIFIED => "Please verify your email address to continue.",
            Message.VERIFICATION_CODE_INVALID => "The verification code is incorrect. Please try again.",
            Message.VERIFICATION_CODE_EXPIRED => "Your verification code has expired. Please request a new one.",
            Message.VERIFICATION_RESEND_TOO_SOON => "Please wait a moment before requesting another code.",
            Message.EMAIL_ALREADY_VERIFIED => "This email has already been verified. Please log in.",
            Message.VERIFICATION_TOO_MANY_ATTEMPTS => "Too many failed attempts. Please request a new code.",
            
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
            Message.VERIFICATION_CODE_SENT => "A verification code has been sent to your email.",
            Message.EMAIL_VERIFIED_SUCCESSFULLY => "Your email has been verified.",
            Message.REGISTRATION_PENDING_VERIFICATION => "Please check your email for a verification code to complete your registration.",

            _ => "An error occurred"
        };
    }

    public static string GetMessageCode(Message messageCode)
    {
        return messageCode.ToString();
    }
}