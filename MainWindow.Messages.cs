using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using YiboFile.Models;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Services.Navigation;

namespace YiboFile
{
    public partial class MainWindow
    {
        internal void InitializeMessageSubscriptions()
        {
            if (_messageBus == null) return;

            // 1. 库高亮请求
            _messageBus.Subscribe<LibrarySelectedMessage>(msg =>
            {
                this.Dispatcher.Invoke(() => _libraryEventHandler?.HighlightMatchingLibrary(msg.Library));
            });

            // 2. 焦点面板变更 (同步逻辑焦点)
            _messageBus.Subscribe<FocusedPaneChangedMessage>(msg =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    if (msg.IsSecondPaneFocused)
                    {
                        SecondFileBrowser?.Focus();
                        SecondFileBrowser?.FilesList?.Focus();
                    }
                    else
                    {
                        FileBrowser?.Focus();
                        FileBrowser?.FilesList?.Focus();
                    }
                });
            });
        }
    }
}
