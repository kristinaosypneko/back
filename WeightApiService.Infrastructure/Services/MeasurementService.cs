using FluentResults;
using Microsoft.Extensions.Logging;
using WeightApiService.Core.Interfaces;
using WeightApiService.Core.Models;
using WeightApiService.Core.Models.DTOs;

namespace WeightApiService.Infrastructure.Services;

public class MeasurementService : IMeasurementService
    {
        private readonly IMeasurementRepository _measurementRepository;
        private readonly IUserRepository _userRepository;
        private readonly ICacheService _cacheService;
        private readonly ILogger<MeasurementService> _logger;

        public MeasurementService(
            IMeasurementRepository measurementRepository,
            IUserRepository userRepository,
            ICacheService cacheService,
            ILogger<MeasurementService> logger) // Добавлен ILogger
        {
            _measurementRepository = measurementRepository;
            _userRepository = userRepository;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<Result> AddAsync(Measurement measurement, string tgId)
        {
            _logger.LogInformation("Attempting to add measurement for TgId: {TgId}", tgId);
            var userResult = await _userRepository.GetByIdAsync(tgId);
            if (userResult.IsFailed)
            {
                _logger.LogWarning("Failed to add measurement. User not found for TgId: {TgId}. Errors: {Errors}", tgId, string.Join(", ", userResult.Errors.Select(e => e.Message)));
                return Result.Fail(userResult.Errors);
            }

            measurement.Id = Guid.NewGuid();
            measurement.UserId = userResult.Value.Id;
            measurement.Date = DateTime.UtcNow;

            var addResult = await _measurementRepository.AddAsync(measurement);
            if (addResult.IsSuccess)
            {
                _logger.LogInformation("Measurement added successfully for TgId: {TgId}. Invalidating cache.", tgId);
                await _cacheService.RemoveAsync($"measurements_user_{tgId}");
                await _cacheService.RemoveAsync($"measurement_{measurement.Id}"); // Также инвалидируем кэш для конкретного измерения
            }
            else
            {
                _logger.LogError("Failed to add measurement to repository for TgId: {TgId}. Errors: {Errors}", tgId, string.Join(", ", addResult.Errors.Select(e => e.Message)));
            }
            return addResult;
        }

        public async Task<Result> AddByTgIdAsync(MeasurementDTO measurementDTO)
        {
            _logger.LogInformation("Attempting to add measurement by DTO for TgId: {TgId}", measurementDTO.TgId);
            if (measurementDTO == null)
            {
                _logger.LogWarning("AddByTgIdAsync failed: MeasurementDTO is null.");
                return Result.Fail("MeasurementDTO is null");
            }

            var userResult = await _userRepository.GetByIdAsync(measurementDTO.TgId);
            if (userResult.IsFailed)
            {
                _logger.LogWarning("Failed to add measurement by DTO. User not found for TgId: {TgId}. Errors: {Errors}", measurementDTO.TgId, string.Join(", ", userResult.Errors.Select(e => e.Message)));
                return Result.Fail(userResult.Errors);
            }

            var measurement = new Measurement
            {
                Id = Guid.NewGuid(),
                Weight = measurementDTO.Weight,
                Date = DateTime.UtcNow,
                UserId = userResult.Value.Id
            };

            var addResult = await _measurementRepository.AddAsync(measurement);
            if (addResult.IsSuccess)
            {
                _logger.LogInformation("Measurement added successfully by DTO for TgId: {TgId}. Invalidating cache.", measurementDTO.TgId);
                await _cacheService.RemoveAsync($"measurements_user_{measurementDTO.TgId}");
                await _cacheService.RemoveAsync($"measurement_{measurement.Id}");
            }
            else
            {
                 _logger.LogError("Failed to add measurement by DTO to repository for TgId: {TgId}. Errors: {Errors}", measurementDTO.TgId, string.Join(", ", addResult.Errors.Select(e => e.Message)));
            }
            return addResult;
        }

        public async Task<Result<List<Measurement>>> GetByUserTgIdAsync(string tgId)
        {
            string cacheKey = $"measurements_user_{tgId}";
            _logger.LogInformation("Attempting to get measurements for user TgId: {TgId}. Cache key: {CacheKey}", tgId, cacheKey);

            var cachedMeasurements = await _cacheService.GetAsync<List<Measurement>>(cacheKey);
            if (cachedMeasurements != null)
            {
                _logger.LogInformation("Cache hit for measurements for user TgId: {TgId}", tgId);
                return Result.Ok(cachedMeasurements);
            }
            _logger.LogInformation("Cache miss for measurements for user TgId: {TgId}. Fetching from database.", tgId);

            var resultFromDb = await _measurementRepository.GetByUserTgIdAsync(tgId);
            if (resultFromDb.IsSuccess && resultFromDb.Value != null && resultFromDb.Value.Any())
            {
                _logger.LogInformation("Fetched {Count} measurements from database for user TgId: {TgId}. Setting to cache.", resultFromDb.Value.Count, tgId);
                await _cacheService.SetAsync(cacheKey, resultFromDb.Value, TimeSpan.FromMinutes(5)); // Кэшируем на 5 минут
            }
            else if (resultFromDb.IsSuccess) // Успешно, но пусто
            {
                 _logger.LogInformation("No measurements found in database for user TgId: {TgId}. Caching empty result.", tgId);
                 await _cacheService.SetAsync(cacheKey, new List<Measurement>(), TimeSpan.FromMinutes(5)); // Кэшируем пустой список
            }
            else
            {
                _logger.LogWarning("Failed to fetch measurements from database for user TgId: {TgId}. Errors: {Errors}", tgId, string.Join(", ", resultFromDb.Errors.Select(e => e.Message)));
            }
            return resultFromDb;
        }

        public async Task<Result<Measurement>> GetByIdAsync(Guid id)
        {
            string cacheKey = $"measurement_{id}";
             _logger.LogInformation("Attempting to get measurement by Id: {MeasurementId}. Cache key: {CacheKey}", id, cacheKey);

            var cachedMeasurement = await _cacheService.GetAsync<Measurement>(cacheKey);
            if (cachedMeasurement != null)
            {
                _logger.LogInformation("Cache hit for measurement Id: {MeasurementId}", id);
                return Result.Ok(cachedMeasurement);
            }
            _logger.LogInformation("Cache miss for measurement Id: {MeasurementId}. Fetching from database.", id);

            var resultFromDb = await _measurementRepository.GetByIdAsync(id);
            if (resultFromDb.IsSuccess && resultFromDb.Value != null)
            {
                _logger.LogInformation("Fetched measurement from database for Id: {MeasurementId}. Setting to cache.", id);
                await _cacheService.SetAsync(cacheKey, resultFromDb.Value, TimeSpan.FromMinutes(10)); // Кэшируем на 10 минут
            }
            else if(resultFromDb.IsFailed)
            {
                _logger.LogWarning("Failed to fetch measurement from database for Id: {MeasurementId}. Errors: {Errors}", id, string.Join(", ", resultFromDb.Errors.Select(e => e.Message)));
            }
            return resultFromDb;
        }

        public async Task<Result> DeleteAsync(Guid id)
        {
            _logger.LogInformation("Attempting to delete measurement with Id: {MeasurementId}", id);
            var measurementToDeleteResult = await GetByIdAsync(id);
            
            if (measurementToDeleteResult.IsFailed)
            {
                _logger.LogWarning("Failed to delete measurement. Measurement not found for Id: {MeasurementId}. Errors: {Errors}", id, string.Join(", ", measurementToDeleteResult.Errors.Select(e => e.Message)));
                return measurementToDeleteResult.ToResult();
            }

            var deleteResult = await _measurementRepository.DeleteAsync(id);
            if (deleteResult.IsSuccess)
            {
                _logger.LogInformation("Measurement deleted successfully for Id: {MeasurementId}. Invalidating cache.", id);
                await _cacheService.RemoveAsync($"measurement_{id}");
                if (measurementToDeleteResult.Value?.User?.TgId != null)
                {
                     _logger.LogInformation("Invalidating user measurements cache for TgId: {TgId}", measurementToDeleteResult.Value.User.TgId);
                    await _cacheService.RemoveAsync($"measurements_user_{measurementToDeleteResult.Value.User.TgId}");
                }
                else
                {
                    _logger.LogWarning("Could not invalidate user measurements cache for measurement Id: {MeasurementId} because User or User.TgId was null.", id);
                }
            }
            else
            {
                 _logger.LogError("Failed to delete measurement from repository for Id: {MeasurementId}. Errors: {Errors}", id, string.Join(", ", deleteResult.Errors.Select(e => e.Message)));
            }
            return deleteResult;
        }
    }