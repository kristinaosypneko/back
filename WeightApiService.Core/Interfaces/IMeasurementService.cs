using FluentResults;
using WeightApiService.Core.Models;
using WeightApiService.Core.Models.DTOs;

namespace WeightApiService.Core.Interfaces;

public interface IMeasurementService
{
    Task<Result> AddAsync(Measurement measurement, string tgId);
    Task<Result> AddByTgIdAsync(MeasurementDTO measurementDTO);
    Task<Result<List<Measurement>>> GetByUserTgIdAsync(string tgId);
    Task<Result<Measurement>> GetByIdAsync(Guid id);
    Task<Result> DeleteAsync(Guid id);
}