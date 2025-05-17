using FluentResults;
using WeightApiService.Core.Interfaces;
using WeightApiService.Core.Models;

namespace WeightApiService.Infrastructure.Services;

public class UserService(IUserRepository repository) : IUserService
{
    public Task<Result> AddAsync(User user)
    {
        return repository.AddAsync(user);
    }

    public Task<Result<User>> GetByIdAsync(string tgId)
    {
        return repository.GetByIdAsync(tgId);
    }
}