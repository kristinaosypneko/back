using Microsoft.AspNetCore.Mvc;
using WeightApiService.Core.Models;
using WeightApiService.Core.Interfaces;
using FluentResults;
using WeightApiService.Core.Models.DTOs;

namespace WeigthApiService.Controllers;

[ApiController]
[Route("api/measurements")]
public class MeasurementsController : ControllerBase
{
    private readonly IMeasurementService _measurementService;

    public MeasurementsController(IMeasurementService measurementService)
    {
        _measurementService = measurementService;
    }

    /// <summary>
    /// Добавляет новое измерение веса для пользователя
    /// </summary>
    /// <param name="measurement">Данные измерения</param>
    /// <param name="tgId">Telegram ID пользователя</param>
    /// <returns>Результат добавления</returns>
    [HttpPost("{tgId}")]
    public async Task<IActionResult> AddMeasurement([FromBody] Measurement measurement, string tgId)
    {
        if (measurement == null)
        {
            return BadRequest("Measurement data is required");
        }

        var result = await _measurementService.AddAsync(measurement, tgId);

        if (result.IsSuccess)
        {
            return Ok(new { 
                message = "Measurement added successfully",
                measurementId = measurement.Id 
            });
        }

        return BadRequest(new
        {
            message = "Failed to add measurement",
            errors = result.Errors.Select(e => e.Message)
        });
    }

    /// <summary>
    /// Добавляет новое измерение веса для пользователя по Telegram ID
    /// </summary>
    /// <param name="measurementDTO">DTO с весом и Telegram ID пользователя</param>
    /// <returns>Результат добавления</returns>
    [HttpPost]
    public async Task<IActionResult> AddMeasurementByTgId([FromBody] MeasurementDTO measurementDTO)
    {
        if (measurementDTO == null)
        {
            return BadRequest("Measurement data is required");
        }

        var result = await _measurementService.AddByTgIdAsync(measurementDTO);

        if (result.IsSuccess)
        {
            return Ok(new { 
                message = "Measurement added successfully"
            });
        }

        return BadRequest(new
        {
            message = "Failed to add measurement",
            errors = result.Errors.Select(e => e.Message)
        });
    }

    /// <summary>
    /// Получает все измерения пользователя по Telegram ID
    /// </summary>
    /// <param name="tgId">Telegram ID пользователя</param>
    /// <returns>Список измерений</returns>
    [HttpGet("user/{tgId}")]
    public async Task<IActionResult> GetUserMeasurements(string tgId)
    {
        var result = await _measurementService.GetByUserTgIdAsync(tgId);

        if (result.IsFailed)
        {
            return NotFound(new
            {
                message = "Measurements not found",
                errors = result.Errors.Select(e => e.Message)
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Получает измерение по ID
    /// </summary>
    /// <param name="id">ID измерения</param>
    /// <returns>Данные измерения</returns>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetMeasurement(Guid id)
    {
        var result = await _measurementService.GetByIdAsync(id);

        if (result.IsFailed)
        {
            return NotFound(new
            {
                message = "Measurement not found",
                errors = result.Errors.Select(e => e.Message)
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Удаляет измерение по ID
    /// </summary>
    /// <param name="id">ID измерения</param>
    /// <returns>Результат удаления</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMeasurement(Guid id)
    {
        var result = await _measurementService.DeleteAsync(id);

        if (result.IsSuccess)
        {
            return Ok(new { message = "Measurement deleted successfully" });
        }

        return NotFound(new
        {
            message = "Failed to delete measurement",
            errors = result.Errors.Select(e => e.Message)
        });
    }
} 