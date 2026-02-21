
using System;
using System.Linq;
using System.Windows;

namespace DumpThemes {
    public class Dumper {
        public static void Run() {
            var app = new Application();
            app.StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
            // Too hard to test WPF app via script without proper main loop and initialized DI.
        }
    }
}
