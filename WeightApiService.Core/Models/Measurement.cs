using System.Text.Json.Serialization;

namespace WeightApiService.Core.Models;

public class Measurement
{
    public Guid Id { get; set; }
    public float Weight { get; set; }
    public DateTime Date { get; set; }
    
    public Guid UserId { get; set; }
    
    [JsonIgnore]
    public User User { get; set; }
}