using System.Windows;
using EchoClip.ViewModels;

namespace EchoClip
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;
        }

        protected override void OnSourceInitialized(System.EventArgs e)
        {
            base.OnSourceInitialized(e);
            // Initialize hotkeys after the HWND is available
            _vm.InitializeHotkeys(this);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_vm.Settings.MinimizeToTray)
            {
                e.Cancel = true;
                Hide();
            }
            // Actual shutdown happens from the tray "Exit" item via App.Current.Shutdown()
        }
    }
}
