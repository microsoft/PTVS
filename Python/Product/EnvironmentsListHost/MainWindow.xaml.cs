using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.EnvironmentsList.Host {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        readonly IInterpreterOptionsService _service = new InterpreterOptionsService(null);


        public MainWindow() {
            InitializeComponent();

            EnvironmentsToolWindow.ViewCreated += EnvironmentsToolWindow_ViewCreated;
            EnvironmentsToolWindow.Service = _service;
        }

        void EnvironmentsToolWindow_ViewCreated(object sender, EnvironmentViewEventArgs e) {
            e.View.Extensions.Add(new PipExtensionProvider(e.View.Factory));
            var withDb = e.View.Factory as IPythonInterpreterFactoryWithDatabase;
            if (withDb != null) {
                e.View.Extensions.Add(new DBExtensionProvider(withDb));
            }
        }


        private void OpenInteractiveWindow_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = e.Parameter is EnvironmentView;
        }

        private void OpenInteractiveWindow_Executed(object sender, ExecutedRoutedEventArgs e) {
            MessageBox.Show("Opening interactive window for " + ((EnvironmentView)e.Parameter).Description);
        }

        private void UnhandledException_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void UnhandledException_Executed(object sender, ExecutedRoutedEventArgs e) {
            System.Diagnostics.Debug.WriteLine((Exception)e.Parameter);
        }

        private void OpenInteractiveOptions_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = e.Parameter is EnvironmentView;
        }

        private void OpenInteractiveOptions_Executed(object sender, ExecutedRoutedEventArgs e) {
            MessageBox.Show("Opening interactive options for " + ((EnvironmentView)e.Parameter).Description);
        }

    }
}
