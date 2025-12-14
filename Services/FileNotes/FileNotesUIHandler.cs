using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OoiMRR.Controls;

namespace OoiMRR.Services.FileNotes
{
    /// <summary>
    /// 文件备注 UI 处理
    /// 负责备注的加载、文本变化处理和自动保存
    /// </summary>
    public class FileNotesUIHandler
    {
        private readonly RightPanelControl _rightPanel;
        private readonly FileBrowserControl _fileBrowser;

        /// <summary>
        /// 初始化文件备注 UI 处理器
        /// </summary>
        /// <param name="rightPanel">右侧面板控件</param>
        /// <param name="fileBrowser">文件浏览器控件</param>
        public FileNotesUIHandler(RightPanelControl rightPanel, FileBrowserControl fileBrowser)
        {
            _rightPanel = rightPanel ?? throw new ArgumentNullException(nameof(rightPanel));
            _fileBrowser = fileBrowser ?? throw new ArgumentNullException(nameof(fileBrowser));
        }

        /// <summary>
        /// 加载文件备注
        /// </summary>
        /// <param name="item">文件系统项</param>
        public void LoadFileNotes(FileSystemItem item)
        {
            if (_rightPanel?.NotesTextBox == null) return;
            
            if (item != null)
            {
                var notes = FileNotesService.GetFileNotes(item.Path);
                _rightPanel.NotesTextBox.Text = notes;
            }
            else
            {
                _rightPanel.NotesTextBox.Text = "";
            }
        }

        /// <summary>
        /// 备注文本框文本变化处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">文本变化事件参数</param>
        public void NotesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 备注文本变化时，实时更新列表中显示的备注
            if (_rightPanel?.NotesTextBox == null) return;
            if (_fileBrowser?.FilesSelectedItem is FileSystemItem selectedItem)
            {
                var notesText = _rightPanel.NotesTextBox.Text;
                // 更新备注的第一行显示
                if (!string.IsNullOrEmpty(notesText))
                {
                    var firstLine = notesText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                    selectedItem.Notes = firstLine.Length > 100 ? firstLine.Substring(0, 100) + "..." : firstLine;
                }
                else
                {
                    selectedItem.Notes = "";
                }
                
                // 刷新显示
                if (_fileBrowser?.FilesList != null)
                    _fileBrowser.FilesList.Items.Refresh();
            }
        }

        /// <summary>
        /// 备注自动保存处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        public async void NotesAutoSaved_Handler(object sender, RoutedEventArgs e)
        {
            if (_rightPanel?.NotesTextBox == null) return;
            
            try
            {
                if (_fileBrowser?.FilesSelectedItem is FileSystemItem selectedItem)
                {
                    // 异步保存，提升性能
                    await FileNotesService.SetFileNotesAsync(selectedItem.Path, _rightPanel.NotesTextBox.Text);
                    
                    // 确保备注显示已更新
                    var notesText = _rightPanel.NotesTextBox.Text;
                    if (!string.IsNullOrEmpty(notesText))
                    {
                        var firstLine = notesText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                        selectedItem.Notes = firstLine.Length > 100 ? firstLine.Substring(0, 100) + "..." : firstLine;
                    }
                    else
                    {
                        selectedItem.Notes = "";
                    }
                    
                    // 刷新显示
                    if (_fileBrowser?.FilesList != null)
                        _fileBrowser.FilesList.Items.Refresh();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存备注失败: {ex.Message}");
            }
        }
    }
}



