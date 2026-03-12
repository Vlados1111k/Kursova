using System.Globalization;

namespace ParkingCourseWork.WinFormsUi;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("uk-UA");
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("uk-UA");
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
