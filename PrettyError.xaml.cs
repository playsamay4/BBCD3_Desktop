using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BBCD3_Desktop
{
    /// <summary>
    /// Interaction logic for PrettyError.xaml
    /// </summary>
    public partial class PrettyError : Window
    {
        public string errorContent
        {
            set { ErrorContent.Text = value; }
        }

        public PrettyError()
        {
            InitializeComponent();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }


}
