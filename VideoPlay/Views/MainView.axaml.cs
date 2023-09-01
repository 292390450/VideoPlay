using System;
using System.Collections.Generic;
using Avalonia.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FFmpeg.AutoGen;
using VideoPlay.Models;
using System.Runtime.CompilerServices;
using Avalonia.Controls.Shapes;
using PropertyChanged;
using Microsoft.CodeAnalysis.Text;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Rendering;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;
using Control = Avalonia.Controls.Control;

namespace VideoPlay.Views;
[DoNotNotify]
public partial class MainView : UserControl,INotifyPropertyChanged
{
    private const double minWidth = 20, minHeight = 20;
    private ObservableCollection<Media> _media;

    public ObservableCollection<Media> Media
    {
        get { return _media; }
        set
        {
            _media = value;
            OnPropertyChanged();
        }
    }
    private double _playInterval;
    private TimeSpan _currentTime;
    public TimeSpan CurrentTime
    {
        get { return _currentTime; }
        set
        {
            _currentTime = value;
            OnPropertyChanged();
        }
    }
    private bool isPlay;
    private Stopwatch stopwatch;
    public MainView()
    {
        InitializeComponent();
        this.DataContext = this;
        stopwatch = new Stopwatch();
        stopwatch.Start();
        Media = new ObservableCollection<Media>();
        //FFmpegLoader.FFmpegPath = System.IO.Path.Combine(AppContext.BaseDirectory, "2022");
        ffmpeg.RootPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Libx64");
        RegistEvent();
        PalyThread();
    }
    private async void PalyThread()
    {
        while (true)
        {
            if (isPlay)
            {
                //

                CurrentTime = TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds - _playInterval);
                if (CurrentTime > TimeLine2.TotalTimeSpan)
                {
                    CurrentTime = TimeSpan.Zero;
                }
                var timePosi = CurrentTime / TimeLine2.Per * TimeLine2.BigScale;
                //根据当前时间，对所有媒体进行可视化，帧筛选
                Canvas.SetLeft(TimePosition, timePosi);

                foreach (var medium in Media)
                {
                    if (medium.StartTimeSpan <= CurrentTime && medium.EndTimeSpan >= CurrentTime)
                    {
                        if (medium.MediaContainer.IsPlay)
                        {

                        }
                        else
                        {

                            var offset = CurrentTime - medium.StartTimeSpan;
                            //调整到内部起点位置
                            medium.MediaContainer.TryGetFrame(medium.InternalStartTime + offset);
                            medium.MediaContainer.Play();
                            medium.IsVisibe = true;
                        }

                    }
                    else
                    {
                        if (medium.MediaContainer.IsPlay)
                        {
                            medium.MediaContainer.Stop();
                        }

                        medium.IsVisibe = false;
                    }
                }

                if (Media.All(x => (x.EndTimeSpan + TimeSpan.FromSeconds(2)) < CurrentTime))
                {
                    isPlay = false;
                }

                RefreshImage();
                //时间是否在媒体内部，在就开始播放

            }

         
            await Task.Delay(20);

        }

    }

    private void RefreshImage()
    {
        var cab = PreviewItemControl.ItemsPanelRoot as Canvas;
        if (cab != null)
        {
            foreach (var cabChild in cab.Children)
            {
                if (cabChild is ContentPresenter contentPresenter)
                {
                    if (contentPresenter.Child is Grid grid)
                    {
                        grid.Children[0].InvalidateVisual();
                    }

                }
            }
        }
    }
    private void Play(object sender, RoutedEventArgs e)
    {
        //时差
        _playInterval = stopwatch.ElapsedMilliseconds - CurrentTime.TotalMilliseconds;
        isPlay = !isPlay;
        if (!isPlay)
        {
            foreach (var medium in Media)
            {
                medium.MediaContainer.Stop();
            }
        }
    }
    private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
      var res=  await TopLevel.GetTopLevel(this).StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            FileTypeFilter = new List<FilePickerFileType>()
            {
                new FilePickerFileType("视频"){Patterns = new List<string>(){ "*.mp4", "*.MTS"}}
            }
        });
      
        if (res!=null &&res.Any())
        {
            var first = res.First();
            var mediaContainer = new MediaContainer(stopwatch);
            mediaContainer.OpenFile(first.Path.LocalPath);

            //
            Media media = new Media()
            {
                MediaContainer = mediaContainer,
                Duration = mediaContainer.Duration,
                StartTimeSpan = TimeSpan.FromSeconds(10),
                InternalStartTime = TimeSpan.Zero,
                InternalEndTimeSpan = mediaContainer.Duration,
                EndTimeSpan = mediaContainer.Duration + TimeSpan.FromSeconds(10),
                Position = new Rect(0, 0, PreviewItemControl.Bounds.Width, PreviewItemControl.Bounds.Height),
                ScaleRect = new Rect(0, 0, 1, 1)
            };
            Media.Add(media);
            var maxMedia = Media.MaxBy(x => (x.EndTimeSpan));
            if (maxMedia != null)
            {
                TimeLine2.TotalTimeSpan = (maxMedia.EndTimeSpan) * 4 / 3;

            }
            Caculate();
        }
    }
    private void Caculate()
    {



        //重新计算每个视频源的宽度和位置
        foreach (var medium in Media)
        {
            var x = medium.StartTimeSpan / TimeLine2.Per * TimeLine2.BigScale;
            var wi = medium.Duration / TimeLine2.Per * TimeLine2.BigScale;
            medium.Start = x;
            medium.ActualWidth = wi;
        }

        var listRemove = new List<Control>();
        foreach (Control bigCanvasChild in BigCanvas.Children)
        {
            if (bigCanvasChild is MediaDuraControl)
            {
                listRemove.Add(bigCanvasChild);
            }
        }

        foreach (var uiElement in listRemove)
        {
            BigCanvas.Children.Remove(uiElement);
        }
        listRemove.Clear();
        //重画
        foreach (var medium in Media)
        {
            var cont = new MediaDuraControl(TimeLine2)
            {
                DataContext = medium,
                Width = medium.ActualWidth,
                Height = 25
            };
            BigCanvas.Children.Add(cont);
            Canvas.SetLeft(cont, medium.Start);

            Canvas.SetTop(cont, medium.LevelIndex * 30);
        }
        //
        var Px = CurrentTime / TimeLine2.Per * TimeLine2.BigScale;
        Canvas.SetLeft(TimePosition, Px);
    }
    private void VideoBottomPanel_OnLoaded(object? sender, RoutedEventArgs e)
    {
        //计算出itemcontrol的宽高
        var ree = VideoBottomPanel.Bounds.Width / VideoBottomPanel.Bounds.Height;
        if (ree > 16 / 9.0)
        {
            //实际宽度更长，以高为准
            var width = VideoBottomPanel.Bounds.Height * 16 / 9;
            PreviewItemControl.Width = width;
            PreviewItemControl.Height = VideoBottomPanel.Bounds.Height;
        }
        else
        {
            //以宽为准
            var height = VideoBottomPanel.Bounds.Width * 9 / 16;
            PreviewItemControl.Height = height;
            PreviewItemControl.Width = VideoBottomPanel.Bounds.Width;
        }
    }

    private void VideoBottomPanel_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        //计算出itemcontrol的宽高
        var ree = VideoBottomPanel.Bounds.Width / VideoBottomPanel.Bounds.Height;
        if (ree > 16 / 9.0)
        {
            //实际宽度更长，以高为准
            var width = VideoBottomPanel.Bounds.Height * 16 / 9;
            PreviewItemControl.Width = width;
            PreviewItemControl.Height = VideoBottomPanel.Bounds.Height;
        }
        else
        {
            //以宽为准
            var height = VideoBottomPanel.Bounds.Width * 9 / 16;
            PreviewItemControl.Height = height;
            PreviewItemControl.Width = VideoBottomPanel.Bounds.Width;
        }
        //全部都更新，按比例的更新
        foreach (var medium in Media)
        {
            medium.Position = new Rect(medium.ScaleRect.X * PreviewItemControl.Width,
                medium.ScaleRect.Y * PreviewItemControl.Height,
                medium.ScaleRect.Width * PreviewItemControl.Width,
                medium.ScaleRect.Height * PreviewItemControl.Height);
        }

    }
    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void TimeLine2_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (isPlay)
        {
            return;
        }
        var timePosi = e.GetPosition(TimeLine2);
        CurrentTime = timePosi.X / TimeLine2.BigScale * TimeLine2.Per;
        //根据当前时间，对所有媒体进行可视化，帧筛选
        Canvas.SetLeft(TimePosition, timePosi.X);
        OnTimeChange();
    }
    private void OnTimeChange()
    {
        foreach (var medium in Media)
        {
            if (medium.StartTimeSpan <= CurrentTime && medium.EndTimeSpan >= CurrentTime)
            {
                //显示，并调取当前时间画面
                if (medium.MediaContainer.TryGetFrame(CurrentTime - medium.StartTimeSpan + medium.InternalStartTime))
                {

                }
                medium.IsVisibe = true;
            }
            else
            {
                //不显示
                medium.IsVisibe = false;
            }
        }
        RefreshImage();
    }

    private void ResizeThumb_OnDragDelta(object sender, VectorEventArgs e)
    {
        //修改模型的宽高，计算比例后保存
        var data = (sender as Control)?.DataContext as Media;
        if (data != null)
        {
            var desiredWidth = (int)(data.Position.Width + e.Vector.X);
            var maxWidth = Math.Max(desiredWidth, minWidth);

            // Resize window height (honoring minimum height)
            var desiredHeight = (int)(data.Position.Height + e.Vector.Y);

            var maxHeight = Math.Max(desiredHeight, minHeight);
            data.Position = new Rect(new Point(data.Position.X, data.Position.Y),
                new Size(maxWidth, maxHeight));
            //计算相对于画布的缩放矩形
            data.ScaleRect = new Rect(new Point(data.Position.X / PreviewItemControl.Bounds.Width,
                    data.Position.Y / PreviewItemControl.Bounds.Height),
                new Size(data.Position.Width / PreviewItemControl.Bounds.Width, data.Position.Height / PreviewItemControl.Bounds.Height));
        }
    }

    #region  底部拖拽
    private bool _isDown;
    private bool _isDragging;
    private Canvas _myCanvas;
    private Control _originalElement;
    private double _originalLeft;
    private double _originalTop;
    private SelectionAdorner _overlayElement;
    private Point _startPoint;

    private async void RegistEvent()
    {
        BigCanvas.AddHandler(InputElement.PointerPressedEvent, MyCanvas_PreviewMouseLeftButtonDown, RoutingStrategies.Tunnel);
        BigCanvas.AddHandler(InputElement.PointerReleasedEvent, MyCanvas_PreviewMouseLeftButtonUp, RoutingStrategies.Tunnel);
        BigCanvas.AddHandler(InputElement.PointerMovedEvent, MyCanvas_PreviewMouseMove, RoutingStrategies.Tunnel);

        Canvas cab =null;
        do
        {
           await Task.Delay(20);
            cab = PreviewItemControl.ItemsPanelRoot as Canvas;
        } while (cab==null);

        cab.AddHandler(InputElement.PointerPressedEvent, PreviewMouseLeftButtonDown, RoutingStrategies.Tunnel);
        cab.AddHandler(InputElement.PointerReleasedEvent, PreviewMouseLeftButtonUp, RoutingStrategies.Tunnel);
        cab.AddHandler(InputElement.PointerMovedEvent, PreviewMouseMove, RoutingStrategies.Tunnel);
    }
    private void MyCanvas_PreviewMouseLeftButtonDown(object sender, PointerPressedEventArgs e)
    {
       
            if (isPlay)
            {
                return;
            }
            if (_myCanvas == null)
            {
                _myCanvas = sender as Canvas;
            }

            if (e.Source == _myCanvas)
            {
                if (isPlay)
                {
                    return;
                }
                var timePosi = e.GetPosition(TimeLine2);
                CurrentTime = timePosi.X / TimeLine2.BigScale * TimeLine2.Per;
                //根据当前时间，对所有媒体进行可视化，帧筛选
                Canvas.SetLeft(TimePosition, timePosi.X);
                OnTimeChange();
            }
            else
            {
                if (e.Source is Rectangle rectangle && rectangle.DataContext is Media)
                {
                    var presenter = Utils.FindVisualParent<MediaDuraControl>(rectangle);
                    if (presenter != null)
                    {
                        _isDown = true;
                        _startPoint = e.GetPosition(_myCanvas);
                        _originalElement = presenter;
                        e.Handled = true;
                    }

                }
                else
                {
                    if (isPlay)
                    {
                        return;
                    }
                    var timePosi = e.GetPosition(TimeLine2);
                    CurrentTime = timePosi.X / TimeLine2.BigScale * TimeLine2.Per;
                    //根据当前时间，对所有媒体进行可视化，帧筛选
                    Canvas.SetLeft(TimePosition, timePosi.X);
                    OnTimeChange();
                }

            }


        
      



    }
    private void MyCanvas_PreviewMouseLeftButtonUp(object sender, PointerReleasedEventArgs e)
    {
       
            if (_isDown)
            {
                DragFinished(false);
                e.Handled = true;
            }
      
       
    }

    private SolidColorBrush defaultBorderBrush = new SolidColorBrush(Color.FromArgb(0, 25, 25, 25));
    private SolidColorBrush highBorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
    private Border hitBorder;
    private async void DragFinished(bool cancelled)
    {
        //System.Windows.Input.Mouse.Capture(null);
        if (_isDragging)
        {
            AdornerLayer.GetAdornerLayer(_myCanvas).Children.Remove(_overlayElement);

            if (cancelled == false)
            {
                if (hitBorder != null)
                {
                    var top = Canvas.GetTop(hitBorder);
                    if (int.TryParse(hitBorder.Tag.ToString(), out var num))
                    {
                        if (_originalElement is MediaDuraControl control)
                        {
                            (control.DataContext as Media).LevelIndex = num;
                        }
                    }
                    Canvas.SetTop(_originalElement, top);
                    hitBorder.Background = defaultBorderBrush;
                }

                var x = _originalLeft + _overlayElement.Left;
                Canvas.SetLeft(_originalElement, x);
                if (_originalElement is MediaDuraControl mediaDuraControl)
                {
                    //修改开始时间
                    var stattime = x / TimeLine2.BigScale * TimeLine2.Per;
                    var data = (mediaDuraControl.DataContext as Media);
                    if (data != null)
                    {
                        data.StartTimeSpan = stattime;
                        data.EndTimeSpan = stattime + data.Duration;
                    }

                }
            }

            _overlayElement = null;
        }

        _isDragging = false;
        _isDown = false;
    }
    private void MyCanvas_PreviewMouseMove(object sender, PointerEventArgs e)
    {
        if (_isDown)
        {
            if ((_isDragging == false) &&
                ((Math.Abs(e.GetPosition(_myCanvas).X - _startPoint.X) >
                 4) ||
                 (Math.Abs(e.GetPosition(_myCanvas).Y - _startPoint.Y) >
                  4)))
            {
                DragStarted();
            }

            if (_isDragging)
            {
                DragMoved(e);
            }
        }
    }
    private void DragStarted()
    {
        var layer = AdornerLayer.GetAdornerLayer(_myCanvas);
        if (layer is null)
        {
            return;
        }
        _isDragging = true;

        _originalLeft = Canvas.GetLeft(_originalElement);
        _originalTop = Canvas.GetTop(_originalElement);
        _overlayElement = new SelectionAdorner(_originalElement)
        {
            [AdornerLayer.AdornedElementProperty] = _myCanvas
        };
        
        layer.Children.Add(_overlayElement);
       
       
    }
    private void DragMoved(PointerEventArgs e)
    {
        
        
        var currentPosition = e.GetPosition(_myCanvas);

      
        if (currentPosition.X >= 0 && currentPosition.X <= _myCanvas.Bounds.Width && currentPosition.Y >= 0 &&
            currentPosition.Y <= _myCanvas.Bounds.Height)
        {
            var leftOsset = currentPosition.X - _startPoint.X;
            var topOsset = currentPosition.Y - _startPoint.Y;

            var destHor = _originalLeft + leftOsset;
            if (destHor < 0 || destHor + (_originalElement as Control)?.Bounds.Width > _myCanvas.Bounds.Width)
            {
                return;
            }

            var destVer = _originalTop + topOsset;
            if (destVer < 0 || destVer + (_originalElement as Control)?.Bounds.Height > _myCanvas.Bounds.Height)
            {
                return;
            }
            //相对于拖拽元素
          // var point= _originalElement.TranslatePoint(new Point(leftOsset, topOsset), _myCanvas);
            _overlayElement.Left = leftOsset;
            _overlayElement.Top = topOsset;
          
          
            VisTest(currentPosition);
        }
        else
        {
            //在外面向内可以

            var leftOsset = currentPosition.X - _startPoint.X;
            var topOsset = currentPosition.Y - _startPoint.Y;
            var centerX = _myCanvas.Bounds.Width / 2;
            var centerY = _myCanvas.Bounds.Height / 2;
            if (((currentPosition.X > centerX && leftOsset <= 0) ||
                 (currentPosition.X < centerX && leftOsset >= 0)) &&
                ((currentPosition.Y < centerY && topOsset >= 0) || (currentPosition.Y > centerY && topOsset <= 0)))
            {
                //var point = _originalElement.TranslatePoint(new Point(leftOsset, topOsset), _myCanvas);
                _overlayElement.Left = leftOsset;
                _overlayElement.Top = topOsset;
                VisTest(currentPosition);
            }


        }

    }
    private void VisTest(Point currentPosition)
    {

        hitBorder = null;
        Border0.Background = defaultBorderBrush;
        Border1.Background = defaultBorderBrush;
        Border2.Background = defaultBorderBrush;
        Border3.Background = defaultBorderBrush;
        Border4.Background = defaultBorderBrush;
        Border5.Background = defaultBorderBrush;
        foreach (var bigCanvasChild in BigCanvas.Children)
        {
            if (bigCanvasChild is Border bor)
            {
                if (bor.Bounds.Contains(currentPosition))
                {
                    hitBorder = bor;
                    bor.Background = highBorderBrush;
                    break;
                }
            }
        }
    }
    #endregion


    #region 预览拖

    private bool _isPreviewDown;
    private bool _isPreviewDragging;
    private Canvas _myPreviewCanvas;
    private Control _originalPreviewElement;
    private double _originalPreviewLeft;
    private double _originalPreviewTop;
    private SelectionAdorner _overlayPreviewElement;
    private Point _startPreviewPoint;

    private void PreviewMouseLeftButtonDown(object sender, PointerPressedEventArgs e)
    {
        if (_myPreviewCanvas == null)
        {
            _myPreviewCanvas = sender as Canvas;
        }

        if (e.Source == _myPreviewCanvas)
        {
        }
        else
        {
            if (e.Source is Grid grid)
            {
                //找到pern
                var presenter = Utils.FindVisualParent<ContentPresenter>(grid);
                if (presenter != null)
                {
                    _isPreviewDown = true;
                    _startPreviewPoint = e.GetPosition(_myPreviewCanvas);
                    _originalPreviewElement = presenter;
                   // _myPreviewCanvas.CaptureMouse();
                    e.Handled = true;
                }

            }

        }
    }
    private void PreviewMouseLeftButtonUp(object sender, PointerReleasedEventArgs e)
    {
        if (_isPreviewDown)
        {
            PreviewDragFinished(false);
            e.Handled = true;
        }
    }
    private async void PreviewDragFinished(bool cancelled)
    {
        //System.Windows.Input.Mouse.Capture(null);
        if (_isPreviewDragging)
        {
            AdornerLayer.GetAdornerLayer(_myPreviewCanvas).Children.Remove(_overlayPreviewElement);
            if (cancelled == false)
            {
                var data = (_originalPreviewElement)?.DataContext as Media;
                if (data != null)
                {
                    var y = _originalPreviewTop + _overlayPreviewElement.Top;
                    var x = _originalPreviewLeft + _overlayPreviewElement.Left;
                    //不需要直接设置坐标，计算下
                    data.Position= data.Position.WithX(x).WithY(y);
                    data.ScaleRect= data.ScaleRect.WithX(data.Position.X / PreviewItemControl.Bounds.Width).WithY(data.Position.Y / PreviewItemControl.Bounds.Height);
                }

            }

            _overlayPreviewElement = null;
        }

        _isPreviewDragging = false;
        _isPreviewDown = false;
    }

    private void PreviewMouseMove(object sender, PointerEventArgs e)
    {
        if (_isPreviewDown)
        {
            if ((_isPreviewDragging == false) &&
                ((Math.Abs(e.GetPosition(_myPreviewCanvas).X - _startPreviewPoint.X) >
                 4) ||
                 (Math.Abs(e.GetPosition(_myPreviewCanvas).Y - _startPreviewPoint.Y) >
                 4)))
            {
                PreviewDragStarted();
            }

            if (_isPreviewDragging)
            {
                PreviewDragMoved(e);
            }
        }
    }
   
    private void PreviewDragStarted()
    {
        var layer = AdornerLayer.GetAdornerLayer(_myPreviewCanvas);
        if (layer is null)
        {
            return;
        }
        _isPreviewDragging = true;

        _originalPreviewLeft = Canvas.GetLeft(_originalPreviewElement );
        _originalPreviewTop = Canvas.GetTop(_originalPreviewElement );

        _overlayPreviewElement = new SelectionAdorner(_originalPreviewElement)
        {
            [AdornerLayer.AdornedElementProperty] = _myPreviewCanvas
        };


        layer.Children.Add(_overlayPreviewElement);
    }

    private void PreviewDragMoved(PointerEventArgs e)
    {
        var currentPosition = e.GetPosition(_myPreviewCanvas);


        if (currentPosition.X >= 0 && currentPosition.X <= _myPreviewCanvas.Bounds.Width && currentPosition.Y >= 0 &&
            currentPosition.Y <= _myPreviewCanvas.Bounds.Height)
        {
            var leftOsset = currentPosition.X - _startPreviewPoint.X;
            var topOsset = currentPosition.Y - _startPreviewPoint.Y;

            var destHor = _originalPreviewLeft + leftOsset;
            if (destHor < 0 || destHor + (_originalPreviewElement )?.Bounds.Width > _myPreviewCanvas.Bounds.Width)
            {
                return;
            }

            var destVer = _originalPreviewTop + topOsset;
            if (destVer < 0 || destVer + (_originalPreviewElement)?.Bounds.Height > _myPreviewCanvas.Bounds.Height)
            {
                return;
            }

            _overlayPreviewElement.Left = leftOsset;
            _overlayPreviewElement.Top = topOsset;
            //VisTest(currentPosition);
        }
        else
        {
            //在外面向内可以

            var leftOsset = currentPosition.X - _startPreviewPoint.X;
            var topOsset = currentPosition.Y - _startPreviewPoint.Y;
            var centerX = _myPreviewCanvas.Bounds.Width / 2;
            var centerY = _myPreviewCanvas.Bounds.Height / 2;
            if (((currentPosition.X > centerX && leftOsset <= 0) ||
                 (currentPosition.X < centerX && leftOsset >= 0)) &&
                ((currentPosition.Y < centerY && topOsset >= 0) || (currentPosition.Y > centerY && topOsset <= 0)))
            {
                _overlayPreviewElement.Left = leftOsset;
                _overlayPreviewElement.Top = topOsset;
                //VisTest(currentPosition);
            }


        }

    }

    #endregion

}
public class Utils
{
    public static T FindVisualParent<T>(StyledElement obj) where T : class
    {
        while (obj != null)
        {
            if (obj is T)
                return obj as T;

            obj = obj.Parent;
        }

        return null;
    }
  
}
