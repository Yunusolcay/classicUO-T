// SPDX-License-Identifier: BSD-2-Clause

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ClassicUO.Launcher.Core;
using ClassicUO.Launcher.ViewModels;
using ClassicUO.Launcher.Views;

namespace ClassicUO.Launcher
{
    internal sealed partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var vm = new MainViewModel(LauncherConfig.Load());
                desktop.MainWindow = new MainWindow { DataContext = vm };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
