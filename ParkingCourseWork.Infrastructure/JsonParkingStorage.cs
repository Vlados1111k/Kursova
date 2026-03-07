using System.Text.Json;
using ParkingCourseWork.Domain;

namespace ParkingCourseWork.Infrastructure;

public class JsonParkingStorage
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true
    };

    public void Save(string path, ParkingManager manager)
    {
        var snapshot = manager.CreateSnapshot();
        var json = JsonSerializer.Serialize(snapshot, _options);
        File.WriteAllText(path, json);
    }

    public ParkingStateSnapshot Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Data file was not found.", path);
        }

        var json = File.ReadAllText(path);
        var snapshot = JsonSerializer.Deserialize<ParkingStateSnapshot>(json, _options);

        if (snapshot is null)
        {
            throw new InvalidOperationException("Failed to deserialize parking state.");
        }

        return snapshot;
    }
}
