using inflan_api.DTOs;
using inflan_api.Models;

namespace inflan_api.Interfaces
{
    public interface IOnboardingService
    {
        Task<OnboardingStatusDto> GetStatusAsync(User user);
    }
}
