using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OoiMRR.Models;
using OoiMRR.Services;

namespace OoiMRR
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
            try
            {
                RenderPredictionResults(new List<TagTrain.Services.TagPredictionResult>());
            }
            catch { }
            
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
        
        // 渲染 AI 预测结果到标签页的预测面板
        private void RenderPredictionResults(List<TagTrain.Services.TagPredictionResult> preds)
        {
            try
            {
                if (TagEditPanel == null) return;
                
                TagEditPanel.PredictionPanel.Children.Clear();
                if (preds == null || preds.Count == 0)
                {
                    TagEditPanel.NoPredictionText.Text = "暂无预测结果";
                    TagEditPanel.NoPredictionText.Visibility = Visibility.Visible;
                    return;
                }
                
                TagEditPanel.NoPredictionText.Visibility = Visibility.Collapsed;
                
                foreach (var p in preds.OrderByDescending(x => x.Confidence).Take(5))
                {
                    var name = OoiMRRIntegration.GetTagName(p.TagId) ?? p.TagId.ToString();
                    var border = new Border
                    {
                        Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 245, 253)),
                        BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(144, 202, 249)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2, 6, 2),
                        Margin = new Thickness(4, 4, 4, 4)
                    };
                    var sp = new StackPanel { Orientation = Orientation.Horizontal };
                    sp.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,6,0) });
                    sp.Children.Add(new TextBlock { Text = $"{p.Confidence:P1}", Foreground = new SolidColorBrush(Colors.Gray) });
                    border.Child = sp;
                    TagEditPanel?.PredictionPanel?.Children.Add(border);
                }
            }
            catch { }
        }
    }
}
