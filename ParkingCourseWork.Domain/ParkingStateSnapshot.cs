namespace ParkingCourseWork.Domain;

public class ParkingStateSnapshot
{
    public List<ParkingPlace> Places { get; set; } = new();
    public List<ParkingSession> Sessions { get; set; } = new();
    public ParkingTariff Tariff { get; set; } = new();
}
