using System.Windows;

namespace CoreTest
{
    /// <summary>
    /// Interaction logic for Login.xaml
    /// </summary>
    public partial class Login : Window
    {
        public static bool IsOpen = false;

        public Login()
        {
            IsOpen = true;
            InitializeComponent();
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            return;

          
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            App.Current.Shutdown();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //DateTime start, end; string team;
            //WindowHelper.GetWorkInfo(DateTime.Now, out start, out end, out team);
            //var tag1 = App.Server["P1_DIE"];
            //txtring1.Text = tag1.ToString();
            //txtring2.Text = App.Server["P2_DIE"].ToString();
        }

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            //EditUser frm = new EditUser();
            //frm.ShowDialog();
        }
    }
}
