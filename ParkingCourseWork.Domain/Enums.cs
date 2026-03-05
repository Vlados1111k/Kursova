namespace ParkingCourseWork.Domain;

public enum ParkingPlaceType
{
    Standard,
    Disabled,
    Electric
}

public enum ParkingPlaceStatus
{
    Free,
    Occupied,
    OutOfService
}

public enum SessionStatus
{
    Active,
    Completed
}

public enum PaymentMethod
{
    Cash,
    Card,
    MobileApp
}

public enum FineStatus
{
    Unpaid,
    Paid,
    Canceled
}
