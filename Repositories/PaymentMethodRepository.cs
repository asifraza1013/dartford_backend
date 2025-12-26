using inflan_api.Interfaces;
using inflan_api.Models;
using inflan_api.MyDBContext;
using Microsoft.EntityFrameworkCore;

namespace inflan_api.Repositories;

public class PaymentMethodRepository : IPaymentMethodRepository
{
    private readonly InflanDBContext _context;

    public PaymentMethodRepository(InflanDBContext context)
    {
        _context = context;
    }

    public async Task<PaymentMethod?> GetByIdAsync(int id)
    {
        return await _context.PaymentMethods
            .FirstOrDefaultAsync(pm => pm.Id == id && pm.IsActive);
    }

    public async Task<List<PaymentMethod>> GetByUserIdAsync(int userId)
    {
        return await _context.PaymentMethods
            .Where(pm => pm.UserId == userId && pm.IsActive)
            .OrderByDescending(pm => pm.IsDefault)
            .ThenByDescending(pm => pm.CreatedAt)
            .ToListAsync();
    }

    public async Task<PaymentMethod?> GetDefaultByUserIdAsync(int userId)
    {
        return await _context.PaymentMethods
            .FirstOrDefaultAsync(pm => pm.UserId == userId && pm.IsDefault && pm.IsActive);
    }

    public async Task<PaymentMethod?> GetByAuthorizationCodeAsync(string authorizationCode)
    {
        return await _context.PaymentMethods
            .FirstOrDefaultAsync(pm => pm.AuthorizationCode == authorizationCode && pm.IsActive);
    }

    public async Task<PaymentMethod> CreateAsync(PaymentMethod paymentMethod)
    {
        paymentMethod.CreatedAt = DateTime.UtcNow;
        _context.PaymentMethods.Add(paymentMethod);
        await _context.SaveChangesAsync();
        return paymentMethod;
    }

    public async Task<PaymentMethod> UpdateAsync(PaymentMethod paymentMethod)
    {
        _context.PaymentMethods.Update(paymentMethod);
        await _context.SaveChangesAsync();
        return paymentMethod;
    }

    public async Task DeleteAsync(int id)
    {
        var paymentMethod = await _context.PaymentMethods.FindAsync(id);
        if (paymentMethod != null)
        {
            paymentMethod.IsActive = false; // Soft delete
            await _context.SaveChangesAsync();
        }
    }

    public async Task SetDefaultAsync(int userId, int paymentMethodId)
    {
        // Remove default flag from all user's payment methods
        var userMethods = await _context.PaymentMethods
            .Where(pm => pm.UserId == userId && pm.IsActive)
            .ToListAsync();

        foreach (var method in userMethods)
        {
            method.IsDefault = method.Id == paymentMethodId;
        }

        await _context.SaveChangesAsync();
    }
}
