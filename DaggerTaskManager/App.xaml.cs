using System.Configuration;
using System.Data;
using System.Windows;

namespace DaggerTaskManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                MessageBox.Show(args.ExceptionObject.ToString(), "Unhandled Exception");
            };

            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show(args.Exception.ToString(), "Dispatcher Exception");
                args.Handled = true;
            };

            base.OnStartup(e);
        }
    }
}
