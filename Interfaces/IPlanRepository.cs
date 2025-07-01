using dartford_api.Models;

namespace dartford_api.Interfaces;

public interface IPlanRepository
{
    Task<IEnumerable<Plan>> GetAll();
    Task<Plan?> GetById(int id);
    Task<Plan> Create(Plan plan);
    Task Update(Plan plan);
    Task Delete(int id);
    Task<IEnumerable<Plan>> GetPlansByUserId(int userId);

}