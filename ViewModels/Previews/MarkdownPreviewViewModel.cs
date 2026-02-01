using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Markdig;
using YiboFile.ViewModels;
using YiboFile.Previews;

namespace YiboFile.ViewModels.Previews
{
    public class MarkdownPreviewViewModel : BasePreviewViewModel
    {
        private string _htmlContent;
        public string HtmlContent
        {
            get => _htmlContent;
            set => SetProperty(ref _htmlContent, value);
        }

        private string _sourceContent;
        public string SourceContent
        {
            get => _sourceContent;
            set => SetProperty(ref _sourceContent, value);
        }

        private bool _isSourceView;
        public bool IsSourceView
        {
            get => _isSourceView;
            set => SetProperty(ref _isSourceView, value);
        }

        private bool _isWordWrap = true;
        public bool IsWordWrap
        {
            get => _isWordWrap;
            set => SetProperty(ref _isWordWrap, value);
        }

        public ICommand ReloadCommand { get; }
        public ICommand ToggleViewCommand { get; }

        public MarkdownPreviewViewModel()
        {
            ReloadCommand = new RelayCommand(async () => await LoadAsync(FilePath));
            ToggleViewCommand = new RelayCommand(() => IsSourceView = !IsSourceView);
            OpenExternalCommand = new RelayCommand(() => PreviewHelper.OpenInDefaultApp(FilePath));
        }

        public async Task LoadAsync(string filePath)
        {
            FilePath = filePath;
            Title = Path.GetFileName(filePath);
            Icon = "📝";
            IsLoading = true;

            try
            {
                string text = await Task.Run(() => File.ReadAllText(filePath));
                SourceContent = text;

                // Render Markdown to HTML
                var pipeline = new MarkdownPipelineBuilder()
                    .UseAdvancedExtensions()
                    .Build();

                string renderedBody = await Task.Run(() => Markdown.ToHtml(text, pipeline));

                // Wrap in local CSS
                HtmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif;
            font-size: 16px;
            line-height: 1.5;
            word-wrap: break-word;
            padding: 2.5rem;
            max-width: 900px;
            margin: 0 auto;
            color: #24292e;
            background-color: #fff;
        }}
        pre {{
            background-color: #f6f8fa;
            border-radius: 6px;
            padding: 16px;
            overflow: auto;
        }}
        code {{
            background-color: rgba(27,31,35,0.05);
            border-radius: 3px;
            padding: 0.2em 0.4em;
        }}
        blockquote {{
            border-left: 0.25em solid #dfe2e5;
            color: #6a737d;
            padding: 0 1em;
            margin: 0;
        }}
        img {{ max-width: 100%; }}
        table {{
            border-spacing: 0;
            border-collapse: collapse;
            width: 100%;
            margin-bottom: 16px;
        }}
        table th, table td {{
            padding: 6px 13px;
            border: 1px solid #dfe2e5;
        }}
        table tr:nth-child(2n) {{ background-color: #f6f8fa; }}
        h1, h2 {{ border-bottom: 1px solid #eaecef; padding-bottom: 0.3em; }}
    </style>
</head>
<body>
    {renderedBody}
</body>
</html>";
            }
            catch (Exception ex)
            {
                SourceContent = $"Error loading Markdown: {ex.Message}";
                HtmlContent = $"<h1>Error</h1><p>{ex.Message}</p>";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
