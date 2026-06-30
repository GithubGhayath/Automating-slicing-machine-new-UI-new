using System.Windows;
using DataAccess.Data;

namespace MR200.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            // Ensure the database exists, is up to date, and is seeded.
            DbInitializer.Initialize();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not connect to or initialize the database.\n\n{ex.Message}\n\n" +
                "Make sure SQL Server is running on the local instance.",
                "Database Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        base.OnStartup(e);
    }
}
