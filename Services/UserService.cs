using System.Security.Cryptography;
using System.Text;
using inflan_api.Repositories;
using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.DTOs;
using inflan_api.Utils;

namespace inflan_api.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IInfluencerService _influencerService;
        private readonly IFollowerCountService _followerCountService;

        public UserService(IUserRepository userRepository, IInfluencerService influencerService, IFollowerCountService followerCountService)
        {
            _userRepository = userRepository;
            _influencerService = influencerService;
            _followerCountService = followerCountService;
        }

        public async Task<IEnumerable<User>> GetAllUsers()
        {
            return await _userRepository.GetAll();
        }

        public async Task<User?> GetUserById(int id)
        {
            return await _userRepository.GetById(id);
        }

        public async Task<User> CreateUser(User user)
        {
            return await _userRepository.Create(user);
        }

        public async Task<(bool success, string? errorMessage)> UpdateUser(int id, UpdateUserDto userDto)
        {
            try
            {
                Console.WriteLine($"UpdateUser called for ID: {id}");
                Console.WriteLine($"User data received: Name={userDto.Name}, Email={userDto.Email}, BrandCategory={userDto.BrandCategory}, BrandSector={userDto.BrandSector}, Goals={userDto.Goals?.Count}");

                var existingUser = await _userRepository.GetById(id);
                if (existingUser == null)
                {
                    Console.WriteLine($"User with ID {id} not found");
                    return (false, "User not found");
                }

                // Only update fields that are provided (not null)
                if (!string.IsNullOrWhiteSpace(userDto.Name))
                    existingUser.Name = userDto.Name;

                if (!string.IsNullOrWhiteSpace(userDto.UserName))
                    existingUser.UserName = userDto.UserName;

                if (!string.IsNullOrWhiteSpace(userDto.Email))
                {
                    // Validate email format (already done by DTO validation attribute)
                    var userExist = await GetByEmailAsync(userDto.Email);
                    // Only check for duplicate email if it's a different user (not the current one)
                    if (userExist != null && userExist.Id != id)
                    {
                        Console.WriteLine($"Email {userDto.Email} already exists for another user");
                        return (false, "Email already exists");
                    }
                    existingUser.Email = userDto.Email;
                }

                if (!string.IsNullOrWhiteSpace(userDto.Password))
                {
                    // Password validation (min 6 chars) is already done by DTO validation attribute
                    existingUser.Password = userDto.Password;
                }

                if (!string.IsNullOrWhiteSpace(userDto.BrandName))
                    existingUser.BrandName = userDto.BrandName;

                if (!string.IsNullOrWhiteSpace(userDto.BrandCategory))
                    existingUser.BrandCategory = userDto.BrandCategory;

                if (!string.IsNullOrWhiteSpace(userDto.BrandSector))
                    existingUser.BrandSector = userDto.BrandSector;

                if (userDto.Goals != null)
                    existingUser.Goals = userDto.Goals;

                if (!string.IsNullOrWhiteSpace(userDto.ProfileImage))
                    existingUser.ProfileImage = userDto.ProfileImage;

                // Handle optional integer fields
                if (userDto.UserType.HasValue && userDto.UserType.Value != 0)
                    existingUser.UserType = userDto.UserType.Value;

                if (userDto.Status.HasValue)
                    existingUser.Status = userDto.Status.Value;

                Console.WriteLine("About to call repository update...");
                await _userRepository.Update(existingUser);
                Console.WriteLine("Repository update completed successfully");

                // Update influencer data if user is an influencer and influencer fields are provided
                if (existingUser.UserType == (int)UserType.INFLUENCER)
                {
                    bool hasSocialMediaUpdate = !string.IsNullOrWhiteSpace(userDto.YouTube) ||
                                               !string.IsNullOrWhiteSpace(userDto.Instagram) ||
                                               !string.IsNullOrWhiteSpace(userDto.Facebook) ||
                                               !string.IsNullOrWhiteSpace(userDto.TikTok);

                    bool hasBioUpdate = !string.IsNullOrWhiteSpace(userDto.Bio);

                    if (hasSocialMediaUpdate || hasBioUpdate)
                    {
                        Console.WriteLine("Updating influencer data...");
                        var existingInfluencer = await _influencerService.GetInfluencerBasicByUserId(id);

                        if (existingInfluencer == null)
                        {
                            existingInfluencer = new Influencer
                            {
                                UserId = id,
                                Bio = "",
                                Instagram = null,
                                YouTube = null,
                                Facebook = null,
                                TikTok = null,
                                InstagramFollower = 0,
                                YouTubeFollower = 0,
                                FacebookFollower = 0,
                                TikTokFollower = 0
                            };
                        }

                        // Update bio if provided
                        if (hasBioUpdate)
                            existingInfluencer.Bio = userDto.Bio;

                        // If social media accounts are being updated, validate them with SocialBlade
                        if (hasSocialMediaUpdate)
                        {
                            Console.WriteLine("Checking which social media accounts have changed...");

                            // Determine which accounts have actually changed
                            bool instagramChanged = !string.IsNullOrWhiteSpace(userDto.Instagram) &&
                                                   userDto.Instagram != existingInfluencer.Instagram;
                            bool youtubeChanged = !string.IsNullOrWhiteSpace(userDto.YouTube) &&
                                                 userDto.YouTube != existingInfluencer.YouTube;
                            bool tiktokChanged = !string.IsNullOrWhiteSpace(userDto.TikTok) &&
                                                userDto.TikTok != existingInfluencer.TikTok;
                            bool facebookChanged = !string.IsNullOrWhiteSpace(userDto.Facebook) &&
                                                  userDto.Facebook != existingInfluencer.Facebook;

                            // Only validate accounts that have actually changed
                            if (instagramChanged || youtubeChanged || tiktokChanged || facebookChanged)
                            {
                                Console.WriteLine("Validating changed social media accounts with SocialBlade...");
                                Console.WriteLine($"Instagram changed: {instagramChanged}, YouTube changed: {youtubeChanged}, TikTok changed: {tiktokChanged}, Facebook changed: {facebookChanged}");

                                // Get follower counts only for changed accounts
                                var followerResults = await _followerCountService.GetAllPlatformFollowersAsync(
                                    instagramUsername: instagramChanged ? userDto.Instagram : null,
                                    youtubeChannelId: youtubeChanged ? userDto.YouTube : null,
                                    tiktokUsername: tiktokChanged ? userDto.TikTok : null,
                                    facebookUsername: facebookChanged ? userDto.Facebook : null
                                );

                                var errors = new List<string>();

                                // Validate Instagram if changed
                                if (instagramChanged && followerResults.ContainsKey("Instagram"))
                                {
                                    var result = followerResults["Instagram"];
                                    if (result.Success && result.Followers > 0)
                                    {
                                        existingInfluencer.Instagram = userDto.Instagram;
                                        existingInfluencer.InstagramFollower = (int)result.Followers;
                                        Console.WriteLine($"Instagram validated: {userDto.Instagram} with {result.Followers} followers");
                                    }
                                    else
                                    {
                                        errors.Add($"Instagram account '{userDto.Instagram}': {(result.Success ? "Account not found or has no followers" : result.ErrorMessage)}");
                                    }
                                }

                                // Validate YouTube if changed
                                if (youtubeChanged && followerResults.ContainsKey("YouTube"))
                                {
                                    var result = followerResults["YouTube"];
                                    if (result.Success && result.Followers > 0)
                                    {
                                        existingInfluencer.YouTube = userDto.YouTube;
                                        existingInfluencer.YouTubeFollower = (int)result.Followers;
                                        Console.WriteLine($"YouTube validated: {userDto.YouTube} with {result.Followers} followers");
                                    }
                                    else
                                    {
                                        errors.Add($"YouTube account '{userDto.YouTube}': {(result.Success ? "Account not found or has no followers" : result.ErrorMessage)}");
                                    }
                                }

                                // Validate TikTok if changed
                                if (tiktokChanged && followerResults.ContainsKey("TikTok"))
                                {
                                    var result = followerResults["TikTok"];
                                    if (result.Success && result.Followers > 0)
                                    {
                                        existingInfluencer.TikTok = userDto.TikTok;
                                        existingInfluencer.TikTokFollower = (int)result.Followers;
                                        Console.WriteLine($"TikTok validated: {userDto.TikTok} with {result.Followers} followers");
                                    }
                                    else
                                    {
                                        errors.Add($"TikTok account '{userDto.TikTok}': {(result.Success ? "Account not found or has no followers" : result.ErrorMessage)}");
                                    }
                                }

                                // Validate Facebook if changed (optional)
                                if (facebookChanged && followerResults.ContainsKey("Facebook"))
                                {
                                    var result = followerResults["Facebook"];
                                    if (result.Success && result.Followers > 0)
                                    {
                                        existingInfluencer.Facebook = userDto.Facebook;
                                        existingInfluencer.FacebookFollower = (int)result.Followers;
                                        Console.WriteLine($"Facebook validated: {userDto.Facebook} with {result.Followers} followers");
                                    }
                                    else
                                    {
                                        errors.Add($"Facebook account '{userDto.Facebook}': {(result.Success ? "Account not found or has no followers" : result.ErrorMessage)}");
                                    }
                                }

                                // If there are validation errors, return error
                                if (errors.Any())
                                {
                                    Console.WriteLine($"Social media validation failed: {string.Join(", ", errors)}");
                                    return (false, $"Social media validation failed: {string.Join("; ", errors)}");
                                }

                                Console.WriteLine("Changed social media accounts validated successfully");
                            }
                            else
                            {
                                Console.WriteLine("No social media accounts have changed, skipping validation");
                            }
                        }

                        // Update or create the influencer record
                        if (existingInfluencer.Id > 0)
                        {
                            await _influencerService.UpdateInfluencer(id, existingInfluencer);
                            Console.WriteLine("Influencer data updated successfully");
                        }
                        else
                        {
                            await _influencerService.CreateInfluencer(existingInfluencer);
                            Console.WriteLine("Influencer record created successfully");
                        }
                    }
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateUser: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return (false, $"An error occurred: {ex.Message}");
            }
        }

        public async Task<bool> DeleteUser(int id)
        {
            var existingUser = await _userRepository.GetById(id);
            if (existingUser == null) return false;

            await _userRepository.Delete(id);
            return true;
        }
        public async Task<User?> GetByEmailAsync(string email)
           => await _userRepository.GetByEmailAsync(email);

        public async Task<User?> ValidateUserAsync(string email, string password)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null || user.Password != password)
                return null;
            return user;
        }

        public async Task<bool> VerifyPasswordAsync(int userId, string password)
        {
            var user = await _userRepository.GetById(userId);
            if (user == null)
                return false;
            return user.Password == password;
        }

        private string CleanName(string name)
        {
            var parts = name.Trim().ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => "user",
                1 => parts[0],
                _ => parts[0] + parts[^1]
            };
        }

        private string GenerateHashSuffix(int length = 6)
        {
            return Guid.NewGuid().ToString("N").Substring(0, length).ToLower();
        }
        public async Task<string> GenerateUniqueUsernameFromNameAsync(string name)
        {
            string baseUsername = CleanName(name);


            string suffix = GenerateHashSuffix(6);
            string username = baseUsername + suffix;

            var exists = await _userRepository.GetByUsernameAsync(username);
            if (exists == null)
            {
                return username;
            }

            throw new Exception("Failed to generate a unique username.");
        }
        public async Task<string?> SaveOrUpdateProfilePictureAsync(int userId, IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return null;

            var uploadsFolder = Path.Combine("wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);
            
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
                return null;

            var fileName = $"{userId}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/{fileName}";
        }

    }
}
