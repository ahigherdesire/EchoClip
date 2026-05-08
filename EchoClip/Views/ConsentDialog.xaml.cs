using System.Windows;

namespace EchoClip.Views
{
    public partial class ConsentDialog : Window
    {
        public bool Accepted { get; private set; }

        public ConsentDialog()
        {
            InitializeComponent();
        }

        private void AcceptClick(object sender, RoutedEventArgs e)
        {
            Accepted = true;
            Close();
        }

        private void DeclineClick(object sender, RoutedEventArgs e)
        {
            Accepted = false;
            Close();
        }
    }
}
