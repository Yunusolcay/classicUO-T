// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ClassicUO.Launcher.ViewModels;

namespace ClassicUO.Launcher.Views
{
    internal sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override async void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            if (DataContext is MainViewModel vm)
            {
                vm.PickFolder = PickFolderAsync;
                vm.RequestClose = Close;
                await vm.OnStartedAsync();
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Cancel();
                vm.Save();
            }

            base.OnClosing(e);
        }

        private async Task<string?> PickFolderAsync()
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = Strings.SelectUoFolder,
                AllowMultiple = false
            });

            return folders.FirstOrDefault()?.TryGetLocalPath();
        }
    }
}
