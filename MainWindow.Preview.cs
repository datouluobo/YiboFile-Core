using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.Models;
using YiboFile.Services;

namespace YiboFile
{
    public partial class MainWindow
    {
        internal void RightPanel_PreviewMiddleClickRequested(object sender, MouseButtonEventArgs e)
        {
            // 预览区中键打开文件
            if (FileBrowser?.FilesSelectedItem is FileSystemItem selectedItem)
            {
                _previewService?.HandlePreviewMiddleClickRequest(selectedItem);
            }
        }

        internal void RightPanel_PreviewOpenFileRequested(object sender, string filePath)
        {
            // 预览区打开文件请求 - 在当前预览区显示文件内容
            _previewService?.HandlePreviewOpenFileRequest(filePath);
        }

        private void LoadFilePreview(FileSystemItem item)
        {
            _previewService?.LoadFilePreview(item);
        }

        private void ClearPreviewAndInfo()
        {
            // 清除预览（使用 PreviewService）
            _previewService?.ClearPreview();

            // 清空预测面板
            // RenderPredictionResults 已移除 - Phase 2将重新实现
            // RenderPredictionResults(new List<TagTrain.Services.TagPredictionResult>());

            // 清除文件信息
            if (FileBrowser?.FileInfoPanelControl != null)
            {
                FileBrowser.FileInfoPanelControl.Children.Clear();
            }

            // 清除备注
            if (RightPanel?.NotesTextBox != null)
            {
                RightPanel.NotesTextBox.Text = "";
            }
        }

        // RenderPredictionResults 已移除 - Phase 2将重新实现
        // private void RenderPredictionResults(List<TagTrain.Services.TagPredictionResult> preds) { }
    }
}

