namespace WeightApiService.Core.Models;

public class User
{
    public Guid Id { get; set; }
    public string TgId { get; set; }
    public string Name { get; set; }
    public DateTime RegistrationTime { get; set; }
    
    public ICollection<Measurement> Measurements { get; set; } = new List<Measurement>();
    
}