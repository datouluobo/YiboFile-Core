using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using YiboFile.ViewModels;
using YiboFile.Previews;

namespace YiboFile.ViewModels.Previews
{
    public class ImagePreviewViewModel : BasePreviewViewModel
    {
        private static readonly HashSet<string> _imageMagickFormats = new()
        {
            ".tga", ".blp", ".heic", ".heif", ".ai", ".psd", ".svg"
        };

        private ImageSource _imageSource;
        public ImageSource ImageSource
        {
            get => _imageSource;
            set => SetProperty(ref _imageSource, value);
        }

        private double _zoomLevel = 1.0;
        public double ZoomLevel
        {
            get => _zoomLevel;
            set => SetProperty(ref _zoomLevel, value);
        }

        private double _rotationAngle = 0;
        public double RotationAngle
        {
            get => _rotationAngle;
            set => SetProperty(ref _rotationAngle, value);
        }

        private bool _isFitToWindow = true;
        public bool IsFitToWindow
        {
            get => _isFitToWindow;
            set => SetProperty(ref _isFitToWindow, value);
        }

        private string _dimensions;
        public string Dimensions
        {
            get => _dimensions;
            set => SetProperty(ref _dimensions, value);
        }

        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand ResetZoomCommand { get; }
        public ICommand FitToWindowCommand { get; }
        public ICommand RotateCommand { get; }
        public ICommand ToggleFullscreenCommand { get; }

        public ImagePreviewViewModel()
        {
            ZoomInCommand = new RelayCommand<object>(p => Zoom(1.2, p));
            ZoomOutCommand = new RelayCommand<object>(p => Zoom(1.0 / 1.2, p));
            ResetZoomCommand = new RelayCommand(() => ResetZoom());
            FitToWindowCommand = new RelayCommand(() => ToggleFitToWindow());
            RotateCommand = new RelayCommand(() => Rotate());
            OpenExternalCommand = new RelayCommand(() => PreviewHelper.OpenInDefaultApp(FilePath));
        }

        public async Task LoadAsync(string filePath)
        {
            FilePath = filePath;
            Title = Path.GetFileName(filePath);
            Icon = "🖼️";
            IsLoading = true;

            try
            {
                var extension = Path.GetExtension(filePath)?.ToLower();
                ImageSource source = null;

                if (_imageMagickFormats.Contains(extension))
                {
                    source = await Task.Run(() => DecodeWithImageMagick(filePath));
                }
                else
                {
                    source = await Task.Run(() => CreateBitmapSource(filePath));
                }

                ImageSource = source;
                if (source is BitmapSource bitmap)
                {
                    Dimensions = $"{(int)bitmap.PixelWidth} × {(int)bitmap.PixelHeight}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading image: {ex.Message}");
                Title = "Error loading image";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private ImageSource CreateBitmapSource(string filePath)
        {
            BitmapImage bitmap = new BitmapImage();
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                bitmap.BeginInit();
                bitmap.StreamSource = fs;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
            }
            bitmap.Freeze();
            return bitmap;
        }

        private BitmapSource DecodeWithImageMagick(string filePath)
        {
            try
            {
                using var magickImage = new MagickImage(filePath);

                // For very large images, resize for preview performance
                const int maxDim = 2000;
                if (magickImage.Width > maxDim || magickImage.Height > maxDim)
                {
                    magickImage.Resize(new MagickGeometry(maxDim, maxDim) { IgnoreAspectRatio = false });
                }

                // Try PNG first (best for transparency)
                try
                {
                    var bytes = magickImage.ToByteArray(MagickFormat.Png);
                    return CreateBitmapFromBytes(bytes);
                }
                catch
                {
                    // Fallback to BMP (no transparency but very compatible)
                    var bytes = magickImage.ToByteArray(MagickFormat.Bmp);
                    return CreateBitmapFromBytes(bytes);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Magick.NET failed: {ex.Message}");
                throw;
            }
        }

        private BitmapSource CreateBitmapFromBytes(byte[] bytes)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = new MemoryStream(bytes);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }


        private void Zoom(double factor, object parameter)
        {
            if (IsFitToWindow)
            {
                // Calculate current actual zoom level from the rendered image size
                if (parameter is System.Windows.Controls.Image img && img.Source is BitmapSource bmp && bmp.PixelWidth > 0)
                {
                    // Update ZoomLevel to match the visual "Fit" scale
                    // Use ActualWidth for more accurate "Fit" scaling detection
                    ZoomLevel = img.ActualWidth / bmp.PixelWidth;
                }
                else
                {
                    // Fallback
                    ZoomLevel = 1.0;
                }
                IsFitToWindow = false;
            }

            ZoomLevel *= factor;
        }

        private void ResetZoom()
        {
            IsFitToWindow = false;
            ZoomLevel = 1.0;
        }

        private void ToggleFitToWindow()
        {
            IsFitToWindow = true;
            ZoomLevel = 1.0;
        }

        private void Rotate()
        {
            RotationAngle = (RotationAngle + 90) % 360;
        }
    }
}
