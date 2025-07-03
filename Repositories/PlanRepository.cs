using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.MyDBContext;
using Microsoft.EntityFrameworkCore;

namespace inflan_api.Repositories;

public class PlanRepository : IPlanRepository
{
    private readonly InflanDBContext _context;

    public PlanRepository()
    {
        _context = new InflanDBContext();
    }

    public async Task<IEnumerable<Plan>> GetAll()
    {
        return await _context.Plans.ToListAsync();
    }

    public async Task<Plan?> GetById(int id)
    {
        return await _context.Plans.FindAsync(id);
    }

    public async Task<Plan> Create(Plan plan)
    {
        _context.Plans.Add(plan);
        await _context.SaveChangesAsync();
        return plan;
    }

    public async Task Update(Plan plan)
    {
        _context.Plans.Update(plan);
        await _context.SaveChangesAsync();
    }

    public async Task Delete(int id)
    {
        var plan = await _context.Plans.FindAsync(id);
        if (plan != null)
        {
            _context.Plans.Remove(plan);
            await _context.SaveChangesAsync();
        }
    }
    public async Task<IEnumerable<Plan>> GetPlansByUserId(int userId)
    {
        return await _context.Plans
            .Where(p => p.UserId == userId)
            .ToListAsync();
    }

}