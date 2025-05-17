using System;

namespace WeightApiService.Core.Models.DTOs;

public class MeasurementDTO
{
    public float Weight { get; set; }
    public required string TgId { get; set; }
    public DateTime? MessageTimestamp { get; set; }
} 