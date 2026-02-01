using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Web.WebView2.Wpf;
using YiboFile.ViewModels.Previews;

namespace YiboFile.Controls
{
    public class ChmPreviewControl : UserControl
    {
        private WebView2 _webView;
        private TreeView _tocTree;
        private GridSplitter _splitter;

        public ChmPreviewControl()
        {
            InitializeUI();

            this.DataContextChanged += (s, e) =>
            {
                if (e.NewValue is ChmPreviewViewModel vm)
                {
                    _tocTree.ItemsSource = vm.Toc;
                    UpdateTocVisibility(vm.IsTocVisible);

                    vm.PropertyChanged += (ps, pe) =>
                    {
                        if (pe.PropertyName == nameof(vm.IndexPath))
                            NavigateToIndex(vm.IndexPath);
                        if (pe.PropertyName == nameof(vm.Toc))
                            _tocTree.ItemsSource = vm.Toc;
                        if (pe.PropertyName == nameof(vm.IsTocVisible))
                            UpdateTocVisibility(vm.IsTocVisible);
                    };

                    if (!string.IsNullOrEmpty(vm.IndexPath))
                        NavigateToIndex(vm.IndexPath);
                }
            };
        }

        private void InitializeUI()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // TOC TreeView
            _tocTree = new TreeView
            {
                BorderThickness = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent
            };

            // Define Template
            var textBlockFactory = new FrameworkElementFactory(typeof(TextBlock));
            textBlockFactory.SetBinding(TextBlock.TextProperty, new Binding("Title"));

            var template = new HierarchicalDataTemplate(typeof(ChmTocNode));
            template.ItemsSource = new Binding("Children");
            template.VisualTree = textBlockFactory;

            _tocTree.ItemTemplate = template;
            _tocTree.SelectedItemChanged += OnTocSelectionChanged;
            _tocTree.SetResourceReference(TreeView.ForegroundProperty, "TextPrimaryBrush");

            // Splitter
            _splitter = new GridSplitter
            {
                Width = 4,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                Background = System.Windows.Media.Brushes.Transparent
            };

            // WebView
            _webView = new WebView2();

            Grid.SetColumn(_tocTree, 0);
            Grid.SetColumn(_splitter, 1);
            Grid.SetColumn(_webView, 2);

            grid.Children.Add(_tocTree);
            grid.Children.Add(_splitter);
            grid.Children.Add(_webView);

            this.Content = grid;
        }

        private void UpdateTocVisibility(bool isVisible)
        {
            var visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            _tocTree.Visibility = visibility;
            _splitter.Visibility = visibility;

            // When collapsed, we might want to collapse the column definition to 0 width to properly hide it,
            // but setting Visibility.Collapsed on the element is usually enough if it's in a Grid column.
            // However, the column with Width="250" will stay there empty.
            // Better to change Column Width.

            if (this.Content is Grid grid)
            {
                if (isVisible)
                {
                    if (grid.ColumnDefinitions[0].Width.Value == 0)
                        grid.ColumnDefinitions[0].Width = new GridLength(250);
                    grid.ColumnDefinitions[1].Width = GridLength.Auto;
                }
                else
                {
                    grid.ColumnDefinitions[0].Width = new GridLength(0);
                    grid.ColumnDefinitions[1].Width = new GridLength(0);
                }
            }
        }

        private void OnTocSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is ChmTocNode node && !string.IsNullOrEmpty(node.Url))
            {
                NavigateToIndex(node.Url);
            }
        }

        private async void NavigateToIndex(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                await _webView.EnsureCoreWebView2Async();
                _webView.CoreWebView2.Navigate(new Uri(path).AbsoluteUri);
            }
            catch { }
        }
    }
}
