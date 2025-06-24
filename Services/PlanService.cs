using dartford_api.Interfaces;
using dartford_api.Models;

namespace dartford_api.Services;

public class PlanService : IPlanService
{
    private readonly IPlanRepository _planRepository;

    public PlanService(IPlanRepository planRepository)
    {
        _planRepository = planRepository;
    }

    public async Task<IEnumerable<Plan>> GetAllPlans()
    {
        return await _planRepository.GetAll();
    }

    public async Task<Plan?> GetPlanById(int id)
    {
        return await _planRepository.GetById(id);
    }

    public async Task<Plan> CreatePlan(Plan plan)
    {
        return await _planRepository.Create(plan);
    }

    public async Task<bool> UpdatePlan(int id, Plan plan)
    {
        var existing = await _planRepository.GetById(id);
        if (existing == null) return false;

        existing.PlanName = plan.PlanName ?? existing.PlanName;
        existing.Currency = plan.Currency ?? existing.Currency;
        existing.Interval = plan.Interval ?? existing.Interval;
        existing.Price = plan.Price != 0 ? plan.Price : existing.Price;
        existing.NumberOfMonths = plan.NumberOfMonths != 0 ? plan.NumberOfMonths : existing.NumberOfMonths;
        existing.PlanDetails = plan.PlanDetails ?? existing.PlanDetails;
        existing.Status = plan.Status != 0 ? plan.Status : existing.Status;

        await _planRepository.Update(existing);
        return true;
    }

    public async Task<bool> DeletePlan(int id)
    {
        var existing = await _planRepository.GetById(id);
        if (existing == null) return false;

        await _planRepository.Delete(id);
        return true;
    }
}