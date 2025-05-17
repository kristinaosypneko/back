using FluentResults;
using WeightApiService.Core.Models;

namespace WeightApiService.Core.Interfaces;

public interface IUserRepository
{
    Task<Result> AddAsync(User user);
    Task<Result<User>> GetByIdAsync(string tgId);
}