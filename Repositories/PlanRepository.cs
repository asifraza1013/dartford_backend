using dartford_api.Interfaces;
using dartford_api.Models;
using dartford_api.MyDBContext;
using Microsoft.EntityFrameworkCore;

namespace dartford_api.Repositories;

public class PlanRepository : IPlanRepository
{
    private readonly DartfordDBContext _context;

    public PlanRepository()
    {
        _context = new DartfordDBContext();
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
}