namespace ParkingCourseWork.Domain;

public class ParkingPlace
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public ParkingPlaceType Type { get; set; } = ParkingPlaceType.Standard;
    public ParkingPlaceStatus Status { get; set; } = ParkingPlaceStatus.Free;

    public bool CanBeOccupied()
    {
        return Status == ParkingPlaceStatus.Free;
    }
}
