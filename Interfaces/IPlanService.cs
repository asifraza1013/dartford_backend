using dartford_api.Models;

namespace dartford_api.Interfaces;

public interface IPlanService
{
    Task<IEnumerable<Plan>> GetAllPlans();
    Task<Plan?> GetPlanById(int id);
    Task<Plan> CreatePlan(Plan plan);
    Task<bool> UpdatePlan(int id, Plan plan);
    Task<bool> DeletePlan(int id);
}