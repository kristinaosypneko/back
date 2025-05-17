using FluentResults;
using WeightApiService.Core.Models;

namespace WeightApiService.Core.Interfaces;

public interface IMeasurementRepository
{
    Task<Result> AddAsync(Measurement measurement);
    Task<Result<List<Measurement>>> GetByUserTgIdAsync(string tgId);
    Task<Result<Measurement>> GetByIdAsync(Guid id);
    Task<Result> DeleteAsync(Guid id);
}