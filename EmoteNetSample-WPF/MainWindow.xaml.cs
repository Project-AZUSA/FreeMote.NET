﻿/*
 *  Project AZUSA © 2015-2017 ( https://github.com/Project-AZUSA )
 *  Emote.NET Sample for WPF
 *  LICENSE:CC BY-NC-SA 4.0
 *  AUTHOR:	Ulysses (wdwxy12345@gmail.com)
 */

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

/*      WindowStyle="None"
        AllowsTransparency="True"
        两者需要一起使用
*/

namespace FreeMote.Tools.Viewer
{
    enum D3DResult : uint
    {
        DEVICE_LOST = 0x88760868,
        DEVICE_NOTRESET = 0x88760869,
        INVAILDCALL = 0x8876086C,
        OK = 0x0,
        UNKNOWN_ERROR = 0xFFFFFFFF
    }

    /// <summary>
    ///     MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        const float RefreshRate = 1000.0f / 60.0f; // 1/60秒カウントをmsへ変換。
        private const int Movement = 10;

        private static double _lastX, _lastY;
        private static bool _leftMouseDown;
        private static bool _rightMouseDown = false;
        private static double _midX, _midY;
        private bool _mouseTrack = false;
        private D3DImage _di;
        private Emote _emote;
        private WindowInteropHelper _helper;
        private EmotePlayer _player;
        private IntPtr _scene;
        private string _psbPath;
        private string[] _psbPaths;
        private PreciseTimer _timer;

        private double _deltaX, _deltaY;
        private double _elapsedTime;
        private bool _measureMode = false;

        public MainWindow()
        {
            string[] args = Environment.GetCommandLineArgs();

            if (args.Length <= 1)
            {
                _psbPath = "emote_test.pure.psb";
            }
            else
            {
                _psbPath = args[1];

                if (args.Length > 2)
                {
                    _psbPaths = args.Skip(1).ToArray();
                }
            }
            if (!File.Exists(_psbPath))
            {
                MessageBox.Show("No file input.");
                Application.Current.Shutdown();
                return;
            }

            _helper = new WindowInteropHelper(this);

            // create a D3DImage to host the scene and
            // monitor it for changes in front buffer availability
            _di = new D3DImage();
            _di.IsFrontBufferAvailableChanged
                += OnIsFrontBufferAvailableChanged;

            MouseMove += MainWindow_MouseMove;
            MouseWheel += MainWindow_MouseWheel;
            MouseDoubleClick += MainWindow_MouseDoubleClick;

            KeyDown += OnKeyDown;

            // make a brush of the scene available as a resource on the window
            Resources["NekoHacksScene"] = new ImageBrush(_di);

            //double x = SystemParameters.WorkArea.Width; //得到屏幕工作区域宽度
            //double y = SystemParameters.WorkArea.Height; //得到屏幕工作区域高度
            //double x1 = SystemParameters.PrimaryScreenWidth; //得到屏幕整体宽度
            //double y1 = SystemParameters.PrimaryScreenHeight; //得到屏幕整体高度

            //WindowStartupLocation = WindowStartupLocation.Manual;
            //Left = x1 - 800;
            //Top = y1 - 600;
            // parse the XAML
            InitializeComponent();
            //Topmost = true;
            //Width = 800;
            //Height = 600;
            _midX = Width / 2;
            _midY = Height / 2;
            CenterMark.Visibility = Visibility.Hidden;
            CharaCenterMark.Visibility = Visibility.Hidden;
            
            _emote = new Emote(_helper.EnsureHandle(), (int)Width, (int)Height, true);
            _emote.EmoteInit();

            if (_psbPaths != null)
            {
                _player = _emote.CreatePlayer("CombinedChara1", _psbPaths);
            }
            else
            {
                _player = _emote.CreatePlayer("Chara1", _psbPath);
            }
            //_player.SetScale(1, 0, 0);
            //_player.SetCoord(0, 0);
            //_player.SetVariable("fade_z", 256);
            _player.SetSmoothing(true);
            _player.Show();
            // begin rendering the custom D3D scene into the D3DImage
            BeginRenderingScene();
        }

        private void MainWindow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var p = e.GetPosition(this);
            Console.WriteLine(WindowWorldToCharacterWorld(p.X, p.Y));
        }

        private async void OnKeyDown(object sender, KeyEventArgs keyEventArgs)
        {
            if (keyEventArgs.Key == Key.Up)
            {
                _player.OffsetCoord(0, Movement);
            }
            if (keyEventArgs.Key == Key.Down)
            {
                _player.OffsetCoord(0, -Movement);
            }
            if (keyEventArgs.Key == Key.Left)
            {
                _player.OffsetCoord(Movement, 0);
            }
            if (keyEventArgs.Key == Key.Right)
            {
                _player.OffsetCoord(-Movement, 0);
            }

            if (keyEventArgs.Key == Key.LeftCtrl)
            {
                if (keyEventArgs.IsDown)
                {
                    _measureMode = !_measureMode;
                }
                if (_measureMode)
                {
                    _player.SetScale(1);
                    _player.SetCoord(0, 0);
                    CenterMark.Visibility = Visibility.Visible;
                    CharaCenterMark.Visibility = Visibility.Visible;
                }
                else
                {
                    CenterMark.Visibility = Visibility.Hidden;
                    CharaCenterMark.Visibility = Visibility.Hidden;
                }
            }

            await Task.Delay(20);
            UpdatePosition();
        }

        void MainWindow_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_measureMode)
            {
                _player.SetScale(1f);
            }
            else
            {
                _player.OffsetScale(1 + ConvertDelta(e.Delta));
            }
        }

        void MainWindow_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && e.GetPosition(MotionPanel).X < 0)
            {
                var ex = e.GetPosition(this);
                _player.OffsetCoord((int)(ex.X - _lastX), (int)(ex.Y - _lastY));
                _lastX = ex.X;
                _lastY = ex.Y;
            }
            else
            {
                var ex2 = e.GetPosition(this);
                _lastX = ex2.X;
                _lastY = ex2.Y;
                if (!_measureMode)
                {
                    if (_mouseTrack)
                    {
                        var ex = e.GetPosition(this);
                        _deltaX = (ex.X - _midX) / _midX * 64;
                        _deltaY = (ex.Y - _midY) / _midY * 64;

                        float frameCount = 0f;
                        //float frameCount = 50f;
                        float easing = 0f;
                        _player.SetVariable("head_UD", (float)_deltaY, frameCount, easing);
                        _player.SetVariable("head_LR", (float)_deltaX, frameCount, easing);
                        _player.SetVariable("body_UD", (float)_deltaY, frameCount, easing);
                        _player.SetVariable("body_LR", (float)_deltaX, frameCount, easing);
                        _player.SetVariable("face_eye_UD", (float)_deltaY, frameCount, easing);
                        _player.SetVariable("face_eye_LR", (float)_deltaX, frameCount, easing);
                    }
                }
            }
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            var p = Mouse.GetPosition(this);
            var (mx, my) = WindowWorldToCharacterWorld(p.X, p.Y);
            _player.GetCoord(out float cx, out float cy);
            var (wx, wy) = CharacterWorldToWindowWorld(cx, cy);
            UpdateCharaMark(wx, wy);
            Title = $"Project AZUSA © FreeMote Viewer - Center: {-cx:F2},{-cy:F2} Mouse: {mx:F2},{my:F2}";
        }

        private void UpdateCharaMark(double wx, double wy)
        {
            var width = CharaCenterMark.ActualWidth;
            var height = CharaCenterMark.ActualHeight;
            var margin = CharaCenterMark.Margin;
            margin.Left = wx - width / 2.0;
            margin.Top = wy - height / 2.0;
            CharaCenterMark.Margin = margin;
        }

        private (float x, float y) ConvertToEmoteWorld(double x, double y)
        {
            var centerX = Width / 2.0;
            var centerY = Height / 2.0;
            float ex = (float)(x - centerX);
            float ey = (float)(y - centerY);
            var scale = _player.GetScale();
            return (ex / scale, ey / scale);
        }

        private (float x, float y) WindowWorldToCharacterWorld(double x, double y)
        {
            var centerX = Width / 2.0;
            var centerY = Height / 2.0;
            float ex = (float)(x - centerX);
            float ey = (float)(y - centerY);
            var scale = _player.GetScale();
            _player.GetCoord(out float cx, out float cy);
            return (-(cx - ex / scale), -(cy - ey / scale));
        }

        private (double x, double y) CharacterWorldToWindowWorld(float x, float y)
        {
            _player.GetCoord(out float cx, out float cy);
            float ex = x - cx;
            float ey = y - cy;
            var centerX = Width / 2.0;
            var centerY = Height / 2.0;
            var scale = _player.GetScale();
            return (cx * scale + centerX + ex * scale, cy * scale + centerY + ey * scale);
        }

        private static float ConvertDelta(int delta)
        {
            return delta / 120.0f / 50.0f;
        }

        private void OnIsFrontBufferAvailableChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // if the front buffer is available, then WPF has just created a new
            // D3D device, so we need to start rendering our custom scene
            if (_di.IsFrontBufferAvailable)
            {
                BeginRenderingScene();
            }
            else
            {
                // If the front buffer is no longer available, then WPF has lost its
                // D3D device so there is no reason to waste cycles rendering our
                // custom scene until a new device is created.
                StopRenderingScene();
            }
        }

        private void BeginRenderingScene()
        {
            if (_di.IsFrontBufferAvailable)
            {
                // create a custom D3D scene and get a pointer to its surface
                _scene = _emote.D3DSurface;

                // set the back buffer using the new scene pointer
                _di.Lock();
                _di.SetBackBuffer(D3DResourceType.IDirect3DSurface9, _scene);
                _di.Unlock();

                _timer = new PreciseTimer();
                // leverage the Rendering event of WPF's composition target to
                // update the custom D3D scene
                CompositionTarget.Rendering += OnRendering;
            }
        }

        private void StopRenderingScene()
        {
            // This method is called when WPF loses its D3D device.
            // In such a circumstance, it is very likely that we have lost 
            // our custom D3D device also, so we should just release the scene.
            // We will create a new scene when a D3D device becomes 
            // available again.
            CompositionTarget.Rendering -= OnRendering;
            _scene = IntPtr.Zero;

            _emote.OnDeviceLost();
            while (_emote.D3DTestCooperativeLevel() == (uint)D3DResult.DEVICE_LOST)
            {
                Thread.Sleep(5);
            }
            if (_emote.D3DTestCooperativeLevel() == (uint)D3DResult.DEVICE_NOTRESET)
            {
                _emote.D3DReset();
                _emote.OnDeviceReset();
                //_emote.D3DInitRenderState();
                //var hr = _emote.D3DTestCooperativeLevel();
            }
            else
            {
                Console.WriteLine("{0:x8}", _emote.D3DTestCooperativeLevel());
            }
        }

        private void OnRendering(object sender, EventArgs e)
        {
            _elapsedTime += _timer.GetElaspedTime() * 1000;

            if (_elapsedTime < RefreshRate)
            {
                return;
            }

            // when WPF's composition target is about to render, we update our 
            // custom render target so that it can be blended with the WPF target
            UpdateScene(_elapsedTime);

            _elapsedTime = 0;
        }

        private void UpdateScene(double elasped)
        {
            if (_di.IsFrontBufferAvailable && _scene != IntPtr.Zero)
            {
                _emote.Update((float)elasped);
                // lock the D3DImage
                _di.Lock();
                // update the scene (via a call into our custom library)
                _emote.D3DBeginScene();
                _emote.Draw();
                _emote.D3DEndScene();
                // invalidate the updated region of the D3DImage (in this case, the whole image)
                _di.AddDirtyRect(new Int32Rect(0, 0, _emote.SurfaceWidth, _emote.SurfaceHeight));
                // unlock the D3DImage
                _di.Unlock();
            }
        }

        private void GetTimelines(object sender, RoutedEventArgs e)
        {
            if (MotionPanel.Children.Count > 0)
            {
                if (MotionPanel.Visibility == Visibility.Visible)
                {
                    MotionPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    MotionPanel.Visibility = Visibility.Visible;
                }
                return;
            }
            var count = _player.CountMainTimelines();
            for (uint i = 0; i < count; i++)
            {
                //Console.WriteLine(_player.GetDiffTimelineLabelAt(i));
                Button btn = new Button
                {
                    //Name = _player.GetDiffTimelineLabelAt(i),
                    Content = _player.GetMainTimelineLabelAt(i),
                    Width = 180,
                    Tag = "main",
                    Margin = new Thickness(0,0,5,5),
                    Background = Brushes.Transparent,
                    Foreground = Brushes.DarkOrange,
                };
                btn.Click += PlayTimeline;
                MotionPanel.Children.Add(btn);
            }

            if (count > 0)
            {
                MotionPanel.Children.Add(new Separator());
            }

            count = _player.CountDiffTimelines();
            for (uint i = 0; i < count; i++)
            {
                //Console.WriteLine(_player.GetDiffTimelineLabelAt(i));
                Button btn = new Button
                {
                    //Name = _player.GetDiffTimelineLabelAt(i),
                    Content = _player.GetDiffTimelineLabelAt(i),
                    Width = 180,
                    Tag = "diff",
                    Margin = new Thickness(0, 0, 5, 5),
                    Background = Brushes.Transparent,
                    Foreground = Brushes.DarkOrange,
                };
                btn.Click += PlayTimeline;
                MotionPanel.Children.Add(btn);
            }
        }

        private void PlayTimeline(object sender, RoutedEventArgs e)
        {
            _player.PlayTimeline(((Button) sender).Content.ToString(),
                ((Button) sender).Tag.ToString() == "diff"
                    ? TimelinePlayFlags.TIMELINE_PLAY_DIFFERENCE
                    : TimelinePlayFlags.NONE);
        }

        private void Stop(object sender, RoutedEventArgs e)
        {
            _player.StopTimeline("");
            _player.Skip();
            _player.SetVariable("fade_z", 256);
        }

        private void Clear(object sender, RoutedEventArgs e)
        {
            for (uint i = 0; i < _player.CountVariables(); i++)
            {
                _player.SetVariable(_player.GetVariableLabelAt(i), 0);
            }
            _player.SetVariable("fade_z", 256);
        }
    }
}