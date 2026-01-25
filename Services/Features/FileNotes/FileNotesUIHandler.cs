using System;
using YiboFile.Models;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YiboFile.Controls;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Services.Features.FileNotes;

namespace YiboFile.Services.FileNotes
{
    /// <summary>
    /// 文件备注 UI 处理器
    /// 负责备注的加载、文本变化处理和自动保存
    /// 现已接入 MVVM 消息总线架构
    /// </summary>
    public class FileNotesUIHandler
    {
        private readonly RightPanelControl _rightPanel;
        private readonly FileBrowserControl _fileBrowser;
        private readonly IMessageBus _messageBus;
        private readonly INotesService _notesService;

        private bool _isUpdatingNotes = false;

        /// <summary>
        /// 初始化文件备注 UI 处理器
        /// </summary>
        /// <param name="rightPanel">右侧面板控件</param>
        /// <param name="fileBrowser">文件浏览器控件</param>
        /// <param name="messageBus">消息总线（可选，用于 MVVM 通信）</param>
        /// <param name="notesService">备注服务（可选，用于新架构）</param>
        public FileNotesUIHandler(
            RightPanelControl rightPanel,
            FileBrowserControl fileBrowser,
            IMessageBus messageBus = null,
            INotesService notesService = null)
        {
            _rightPanel = rightPanel ?? throw new ArgumentNullException(nameof(rightPanel));
            _fileBrowser = fileBrowser ?? throw new ArgumentNullException(nameof(fileBrowser));
            _messageBus = messageBus;
            _notesService = notesService;

            // 订阅消息总线事件
            if (_messageBus != null)
            {
                _messageBus.Subscribe<NotesLoadedMessage>(OnNotesLoaded);
                _messageBus.Subscribe<NotesUpdatedMessage>(OnNotesUpdated);
            }
        }

        #region 消息处理

        private void OnNotesLoaded(NotesLoadedMessage message)
        {
            if (_rightPanel?.NotesTextBox == null) return;

            var selectedItem = _fileBrowser?.FilesSelectedItem as FileSystemItem;
            if (selectedItem != null && selectedItem.Path == message.FilePath)
            {
                _isUpdatingNotes = true;
                try
                {
                    _rightPanel.NotesTextBox.Text = message.Notes ?? string.Empty;
                }
                finally
                {
                    _isUpdatingNotes = false;
                }
            }
        }

        private void OnNotesUpdated(NotesUpdatedMessage message)
        {
            // 更新列表中的备注摘要
            if (_fileBrowser?.FilesSelectedItem is FileSystemItem selectedItem
                && selectedItem.Path == message.FilePath)
            {
                selectedItem.Notes = message.Summary ?? string.Empty;
                _fileBrowser.FilesList?.Items.Refresh();
            }
        }

        #endregion

        #region 公开方法

        /// <summary>
        /// 加载文件备注
        /// </summary>
        /// <param name="item">文件系统项</param>
        public void LoadFileNotes(FileSystemItem item)
        {
            if (_rightPanel?.NotesTextBox == null) return;

            _isUpdatingNotes = true;
            try
            {
                if (item != null)
                {
                    // 优先使用新服务
                    string notes;
                    if (_notesService != null)
                    {
                        notes = _notesService.GetNotes(item.Path);
                    }
                    else
                    {
                        // 兼容旧静态服务
                        notes = FileNotesService.GetFileNotes(item.Path);
                    }
                    _rightPanel.NotesTextBox.Text = notes;
                }
                else
                {
                    _rightPanel.NotesTextBox.Text = string.Empty;
                }
            }
            finally
            {
                _isUpdatingNotes = false;
            }
        }

        /// <summary>
        /// 备注文本框文本变化处理
        /// </summary>
        public void NotesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_rightPanel?.NotesTextBox == null || _isUpdatingNotes) return;

            if (_fileBrowser?.FilesSelectedItem is FileSystemItem selectedItem)
            {
                var notesText = _rightPanel.NotesTextBox.Text;

                // 更新备注摘要（第一行）
                if (!string.IsNullOrEmpty(notesText))
                {
                    var firstLine = notesText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                             .FirstOrDefault() ?? string.Empty;
                    selectedItem.Notes = firstLine.Length > 100 ? firstLine.Substring(0, 100) + "..." : firstLine;
                }
                else
                {
                    selectedItem.Notes = string.Empty;
                }

                // 刷新列表显示
                _fileBrowser.FilesList?.Items.Refresh();
            }
        }

        /// <summary>
        /// 备注自动保存处理
        /// </summary>
        public async void NotesAutoSaved_Handler(object sender, RoutedEventArgs e)
        {
            if (_rightPanel?.NotesTextBox == null) return;

            try
            {
                if (_fileBrowser?.FilesSelectedItem is FileSystemItem selectedItem)
                {
                    var notesText = _rightPanel.NotesTextBox.Text;

                    // 优先使用新服务
                    if (_notesService != null)
                    {
                        await _notesService.SaveNotesAsync(selectedItem.Path, notesText);
                    }
                    else
                    {
                        // 兼容旧静态服务
                        await FileNotesService.SetFileNotesAsync(selectedItem.Path, notesText);
                    }

                    // 更新列表中的备注摘要
                    if (!string.IsNullOrEmpty(notesText))
                    {
                        var firstLine = notesText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                                 .FirstOrDefault() ?? string.Empty;
                        selectedItem.Notes = firstLine.Length > 100 ? firstLine.Substring(0, 100) + "..." : firstLine;
                    }
                    else
                    {
                        selectedItem.Notes = string.Empty;
                    }

                    _fileBrowser.FilesList?.Items.Refresh();
                }
            }
            catch (Exception)
            {
                // 静默处理保存错误
            }
        }

        /// <summary>
        /// 切换备注面板可见性
        /// </summary>
        public void ToggleNotesPanel()
        {
            if (_rightPanel != null)
            {
                _rightPanel.Visibility = _rightPanel.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        #endregion
    }
}
