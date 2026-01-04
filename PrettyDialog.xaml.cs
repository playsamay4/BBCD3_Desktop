using System.Windows;

namespace BBCD3_Desktop
{
    public partial class PrettyDialog : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.No;

        public PrettyDialog(string message, string changelog)
        {
            InitializeComponent();
            MessageText.Text = message;
            ChangelogBox.Text = changelog; 
        }

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            this.Close();
        }

        private void BtnLater_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            this.Close();
        }

        private void BtnSkip_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            this.Close();
        }
    }
}