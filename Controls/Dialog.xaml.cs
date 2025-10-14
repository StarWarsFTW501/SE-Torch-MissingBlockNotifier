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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TorchPlugin
{
    /// <summary>
    /// Interakční logika pro Dialog.xaml
    /// </summary>
    public partial class Dialog : Window
    {
        public UserControl ContentControl { get; }
        Dialog(UserControl content, object dataContext)
        {
            InitializeComponent();
            ContentControl = content ?? throw new ArgumentNullException(nameof(content));
            DataContext = dataContext;
            Loaded += (s, e) => Activate();
        }

        /// <summary>
        /// Creates a new dialog window with the specified content, owner window and data context and shows it as a modal dialog blocking the executing thread until closed.
        /// </summary>
        /// <param name="content">Content to fill the dialog window with.</param>
        /// <param name="owner">Owning window. Will enforce priority focus on the dialog over this window.</param>
        /// <param name="dataContext">DataContext to assign to the dialog. May be null if not required.</param>
        /// <param name="topmost">Whether to make this dialog the topmost window of the application at all times.</param>
        public static void CreateModalBlocking(UserControl content, Window owner, object dataContext = null, bool topmost = true)
        {
            var dialog = new Dialog(content, dataContext)
            {
                Owner = owner,
                Topmost = topmost
            };
            dialog.ShowDialog();
        }
    }
}
