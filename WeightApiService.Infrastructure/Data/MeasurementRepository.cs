using Microsoft.EntityFrameworkCore;
using FluentResults;
using WeightApiService.Core.Interfaces;
using WeightApiService.Core.Models;
using WeightApiService.Infrastructure.Persistence;

namespace WeightApiService.Infrastructure.Data;

public class MeasurementRepository : IMeasurementRepository
{
    private readonly TgDbContext _context;

    public MeasurementRepository(TgDbContext context)
    {
        _context = context ?? throw new NullReferenceException("Context for MeasurementRepository is null");
    }
    
    public async Task<Result> AddAsync(Measurement measurement)
    {
        if (measurement == null) 
            return Result.Fail("Measurement is null");
        
        try
        {
            await _context.Measurement.AddAsync(measurement);
            await _context.SaveChangesAsync();
            return Result.Ok();
        }
        catch (Exception e)
        {
            return Result.Fail($"Failed to add measurement: {e.Message}");
        }
    }

    public async Task<Result<List<Measurement>>> GetByUserTgIdAsync(string tgId)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.Measurements)
                .FirstOrDefaultAsync(u => u.TgId == tgId);
                
            if (user == null)
                return Result.Fail($"User with Telegram ID {tgId} not found");
                
            return Result.Ok(user.Measurements.ToList());
        }
        catch (Exception e)
        {
            return Result.Fail($"Failed to get measurements: {e.Message}");
        }
    }

    public async Task<Result<Measurement>> GetByIdAsync(Guid id)
    {
        try
        {
            var measurement = await _context.Measurement
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.Id == id);
                
            return measurement != null 
                ? Result.Ok(measurement) 
                : Result.Fail($"Measurement with ID {id} not found");
        }
        catch (Exception e)
        {
            return Result.Fail($"Failed to get measurement: {e.Message}");
        }
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        try
        {
            var measurement = await _context.Measurement.FindAsync(id);
            if (measurement == null)
                return Result.Fail($"Measurement with ID {id} not found");
                
            _context.Measurement.Remove(measurement);
            await _context.SaveChangesAsync();
            return Result.Ok();
        }
        catch (Exception e)
        {
            return Result.Fail($"Failed to delete measurement: {e.Message}");
        }
    }
} 