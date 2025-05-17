using Microsoft.EntityFrameworkCore;
using FluentResults;
using WeightApiService.Core.Interfaces;
using WeightApiService.Core.Models;
using WeightApiService.Infrastructure.Persistence;

namespace WeightApiService.Infrastructure.Data;

public class UserRepository : IUserRepository
{
    private TgDbContext _context;

    public UserRepository(TgDbContext context)
    {
        _context = context ?? throw new NullReferenceException("Context for UserRepository is null");
    }
    
    public async Task<Result> AddAsync(User user)
    {
        if (user == null) throw new NullReferenceException("User is null");
        
        try
        {
            if (await IsUserExist(user.TgId))
            {
                return Result.Fail("User already Exists");
            } 
            
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
            return Result.Ok();
            
        }
        catch (Exception e)
        {
            return Result.Fail($"{e.Message}");
        }
    }

    public async Task<Result<User>> GetByIdAsync(string tgId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.TgId == tgId);
        return user != null
            ? Result.Ok(user)
            : Result.Fail($"User with ID {tgId} not found");
    }

    private async Task<bool> IsUserExist(string id)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.TgId == id);
            return user != null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}