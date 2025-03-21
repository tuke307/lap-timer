﻿using LapTimer.Core.Models;
using LapTimer.Core.Services;
using LapTimer.SkiaSharp.Helpers;
using LapTimer.SkiaSharp.Models;
using LapTimer.SkiaSharp.Presentation.ViewModels.SessionMap;
using LapTimer.SkiaSharp.SkiaSharp;
using Microsoft.Extensions.Logging;
using MvvmCross;
using MvvmCross.Logging;
using MvvmCross.Plugin.Messenger;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Xamarin.Forms;
using Xamarin.Forms.GoogleMaps;
using SKSvg = SkiaSharp.Extended.Svg.SKSvg;

namespace LapTimer.SkiaSharp.Presentation.Views.SessionMap
{
    /// <summary>
    /// SessionMap.
    /// </summary>
    /// <seealso cref="Xamarin.Forms.ContentView" />
    public partial class SessionMap : ContentView
    {
        #region BindableProperties

        /// <summary>
        /// The information color property
        /// </summary>
        public static readonly BindableProperty InfoColorProperty = BindableProperty.Create(
            nameof(InfoColor),
            typeof(Color),
            typeof(SessionMap),
            defaultValue: Color.Default);

        /// <summary>
        /// The map type property
        /// </summary>
        public static readonly BindableProperty MapTypeProperty = BindableProperty.Create(
            nameof(MapType),
            typeof(MapType),
            typeof(SessionMap),
            defaultValue: MapType.Satellite);

        /// <summary>
        /// The maximum time property
        /// </summary>
        public static readonly BindableProperty MaxTimeProperty = BindableProperty.Create(
            nameof(MaxTime),
            typeof(TimeSpan),
            typeof(SessionMap),
            defaultValue: TimeSpan.MaxValue);

        /// <summary>
        /// My location enabled property
        /// </summary>
        public static readonly BindableProperty MyLocationEnabledProperty = BindableProperty.Create(
           nameof(MyLocationEnabled),
           typeof(bool),
           typeof(SessionMap),
           defaultValue: false);

        /// <summary>
        /// The path thickness property
        /// </summary>
        public static readonly BindableProperty PathThicknessProperty = BindableProperty.Create(
                    nameof(PathThickness),
            typeof(int),
            typeof(SessionMap),
            defaultValue: 2,
            propertyChanged: InvalidateSurface);

        /// <summary>
        /// The route drawing property
        /// </summary>
        public static readonly BindableProperty RouteDrawingProperty = BindableProperty.Create(
           nameof(RouteDrawing),
           typeof(bool),
           typeof(SessionMap),
           defaultValue: false);

        /// <summary>
        /// The session map information property
        /// </summary>
        public static readonly BindableProperty SessionMapInfoProperty = BindableProperty.Create(
            nameof(SessionMapInfo),
            typeof(ViewModels.SessionMap.SessionMap),
            typeof(SessionMap),
            propertyChanged: SessionMapInfoChanged);

        /// <summary>
        /// Gets or sets the color of the information.
        /// </summary>
        /// <value>
        /// The color of the information.
        /// </value>
        public Color InfoColor
        {
            get => (Color)GetValue(InfoColorProperty);
            set => SetValue(InfoColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the type of the map.
        /// </summary>
        /// <value>
        /// The type of the map.
        /// </value>
        public MapType MapType
        {
            get => (MapType)GetValue(MapTypeProperty);
            set => SetValue(MapTypeProperty, value);
        }

        /// <summary>
        /// Gets or sets the maximum time.
        /// </summary>
        /// <value>
        /// The maximum time.
        /// </value>
        public TimeSpan MaxTime
        {
            get => (TimeSpan)GetValue(MaxTimeProperty);
            set => SetValue(MaxTimeProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether [my location enabled].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [my location enabled]; otherwise, <c>false</c>.
        /// </value>
        public bool MyLocationEnabled
        {
            get => (bool)GetValue(MyLocationEnabledProperty);
            set => SetValue(MyLocationEnabledProperty, value);
        }

        /// <summary>
        /// Gets or sets the path thickness.
        /// </summary>
        /// <value>
        /// The path thickness.
        /// </value>
        public int PathThickness
        {
            get => (int)GetValue(PathThicknessProperty);
            set => SetValue(PathThicknessProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether [route drawing].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [route drawing]; otherwise, <c>false</c>.
        /// </value>
        public bool RouteDrawing
        {
            get => (bool)GetValue(RouteDrawingProperty);
            set => SetValue(RouteDrawingProperty, value);
        }

        /// <summary>
        /// Gets or sets the session map information.
        /// </summary>
        /// <value>
        /// The session map information.
        /// </value>
        public ViewModels.SessionMap.SessionMap SessionMapInfo
        {
            get => (ViewModels.SessionMap.SessionMap)GetValue(SessionMapInfoProperty);
            set => SetValue(SessionMapInfoProperty, value);
        }

        #endregion BindableProperties

        #region PrivateProperties

        private readonly ILogger _log;
        private LatLong _bottomRightPosition;
        private LatLong _centerPosition;
        private bool _drawingMode = false;

        //private bool _isCameraInitialized;
        private ILocationService _locationService;

        private IMvxMessenger _messenger;
        private float _pictureSize;
        private PositionConverter _positionConverter;
        private SKPoint _previousCenter;
        private double _previousTopLeftBottomRightSquareDistance;
        private MvxSubscriptionToken _token;

        #region Images

        private Position _currentPosition;
        private SKSvg _endImage;
        private SKColor _firstImageColor;
        private bool _forceInvalidation;
        private SKPaint _gradientPathPaint;
        private SKColor _lastImageColor;
        private SKPicture _overlayPicture;
        private SKPaint _positionMarkerPaint;
        private SKSvg _startImage;

        #endregion Images

        private LatLong _topLeftPosition;
        private bool _zoomToMyLocation;

        #endregion PrivateProperties

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionMap" /> class.
        /// </summary>
        public SessionMap()
        {
            InitializeComponent();

            _log = Mvx.IoCProvider.Resolve<ILogger>();
        }

        #region Methods

        /// <summary>
        /// Method that is called when a bound property is changed.
        /// </summary>
        /// <param name="propertyName">The name of the bound property that changed.</param>
        /// <remarks>To be added.</remarks>
        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);

            if (propertyName == nameof(MaxTime))
            {
                _forceInvalidation = true;
                MapOverlay.InvalidateSurface();
            }
        }

        /// <summary>
        /// Invalidates the surface.
        /// </summary>
        /// <param name="bindable">The bindable.</param>
        /// <param name="oldvalue">The oldvalue.</param>
        /// <param name="newvalue">The newvalue.</param>
        private static void InvalidateSurface(BindableObject bindable, object oldvalue, object newvalue)
        {
            ((SessionMap)bindable).MapOverlay.InvalidateSurface();
        }

        /// <summary>
        /// Sessions the map information changed.
        /// </summary>
        /// <param name="bindable">The bindable.</param>
        /// <param name="oldvalue">The oldvalue.</param>
        /// <param name="newvalue">The newvalue.</param>
        private static void SessionMapInfoChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (newvalue != null)
            {
                ((SessionMap)bindable).Initialize();
            }
        }

        /// <summary>
        /// Zeichnet die Zielflagge als LastPoint und die Stoppuhr als FirstPoint.
        /// </summary>
        /// <param name="canvas">The canvas.</param>
        /// <param name="firstSessionPoint">The first session point.</param>
        /// <param name="lastSessionPoint">The last session point.</param>
        /// <returns></returns>
        private bool DrawFirstAndLastMarker(SKCanvas canvas, ISessionDisplayablePoint firstSessionPoint, ISessionDisplayablePoint lastSessionPoint)
        {
            var firstPoint = _positionConverter[firstSessionPoint.Position].ToSKPoint();
            var lastPoint = _positionConverter[lastSessionPoint.Position].ToSKPoint();

            float svgStartMax = Math.Max(_startImage.Picture.CullRect.Width, _startImage.Picture.CullRect.Height);
            float startScale = _pictureSize / svgStartMax;

            var startMatrix = SKMatrix.CreateScaleTranslation(
                startScale,
                startScale,
                firstPoint.X - (_pictureSize / 2),
                firstPoint.Y - (_pictureSize / 2));

            using (var picturePaint = new SKPaint()
            {
                ColorFilter = SKColorFilter.CreateBlendMode(
                        InfoColor == Color.Default ? _firstImageColor : InfoColor.ToSKColor(),
                        SKBlendMode.SrcIn)
            })
            {
                canvas.DrawPicture(_startImage.Picture, ref startMatrix, picturePaint);
            }

            // wenn nicht am Ende der strecke dann wird gerade gescrollt
            if (lastSessionPoint.Time - MaxTime > TimeSpan.FromSeconds(20))
            {
                return false;
            }

            float svgEndMax = Math.Max(_endImage.Picture.CullRect.Width, _endImage.Picture.CullRect.Height);
            float endScale = _pictureSize / svgEndMax;

            var endMatrix = SKMatrix.CreateScaleTranslation(endScale, endScale, lastPoint.X, lastPoint.Y - _pictureSize);

            using (var picturePaint = new SKPaint()
            {
                ColorFilter = SKColorFilter.CreateBlendMode(
                        InfoColor == Color.Default ? _lastImageColor : InfoColor.ToSKColor(),
                        SKBlendMode.SrcIn)
            })
            {
                canvas.DrawPicture(_endImage.Picture, ref endMatrix, picturePaint);
            }

            return true;
        }

        /// <summary>
        /// Zeichnet den Positions-Marker.
        /// </summary>
        /// <param name="canvas">The canvas.</param>
        /// <param name="positionPoint">Der Positions-Punkt.</param>
        private void DrawPositionMarker(SKCanvas canvas, SKPoint positionPoint)
        {
            canvas.DrawCircle(positionPoint.X, positionPoint.Y, SkiaHelper.ToPixel(3), _positionMarkerPaint);
        }

        /// <summary>
        /// Fires when GoogleMap camera changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">
        /// The <see cref="CameraIdledEventArgs" /> instance containing the event data.
        /// </param>
        private void GoogleMapCameraIdled(object sender, CameraIdledEventArgs e)
        {
            _log.LogDebug($"CameraIdled: pos: {e.Position.Target.Latitude}, {e.Position.Target.Longitude}");

            //if (!_isCameraInitialized)
            //{
            //    _isCameraInitialized = true;
            //}

            if (!_drawingMode)
            {
                MapOverlay.InvalidateSurface();
            }
        }

        /// <summary>
        /// Googles the map camera move started.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="CameraMoveStartedEventArgs"/> instance containing the event data.</param>
        private void GoogleMapCameraMoveStarted(object sender, CameraMoveStartedEventArgs e)
        {
            if (e.IsGesture)
            {
                _zoomToMyLocation = false;
            }
        }

        /// <summary>
        /// Fires when GoogleMap MyLocation-Button clicked.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MyLocationButtonClickedEventArgs"/> instance containing the event data.</param>
        /// <exception cref="NotImplementedException"></exception>
        private void GoogleMapMyLocationButtonClicked(object sender, MyLocationButtonClickedEventArgs e)
        {
            _zoomToMyLocation = true;
        }

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        private void Initialize()
        {
            _log.LogDebug($"Initializing {SessionMapInfo.SessionPoints.Count} points");

            _positionConverter = new PositionConverter();

            // direkt auf maximal Zeit legen, damit alles gezeichnet wird
            MaxTime = TimeSpan.FromSeconds(SessionMapInfo.TotalDurationInSeconds);

            var region = SessionMapInfo.Region;
            _centerPosition = new LatLong(region.Center.Latitude, region.Center.Longitude);
            _topLeftPosition = SessionMapInfo.TopRight.ToLatLong();
            _bottomRightPosition = SessionMapInfo.BottomLeft.ToLatLong();

            _previousCenter = SKPoint.Empty;
            _previousTopLeftBottomRightSquareDistance = 1;

            if (MyLocationEnabled)
            {
                _locationService = Mvx.IoCProvider.Resolve<ILocationService>();
                _messenger = Mvx.IoCProvider.Resolve<IMvxMessenger>();
                _token = _messenger.Subscribe<MvxLocationMessage>(this.OnLocationUpdated);
            }

            InitializeSvgImages();

            InitializeMap();
        }

        /// <summary>
        /// Initializes the map.
        /// </summary>
        private void InitializeMap()
        {
            _log.LogDebug($"InitializeMap");

            GoogleMap.MyLocationEnabled = MyLocationEnabled;
            GoogleMap.UiSettings.MyLocationButtonEnabled = MyLocationEnabled;
            GoogleMap.MapType = MapType;

            // events
            GoogleMap.CameraIdled += GoogleMapCameraIdled;
            GoogleMap.CameraMoveStarted += GoogleMapCameraMoveStarted;
            GoogleMap.MyLocationButtonClicked += GoogleMapMyLocationButtonClicked;

            // move to SessionMapInfo region
            if (SessionMapInfo != null)
            {
                Device.StartTimer(TimeSpan.FromMilliseconds(1000), () =>
                {
                    GoogleMap.MoveToRegion(SessionMapInfo.Region, true);
                    return false;
                });
            }

            _log.LogDebug($"END InitializeMap");
        }

        /// <summary>
        /// Initializes the map resources if needed.
        /// </summary>
        private void InitializeMapResourcesIfNeeded()
        {
            if (_gradientPathPaint != null)
            {
                return;
            }

            InitializeSvgImages();

            _gradientPathPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                IsAntialias = true,
                StrokeWidth = SkiaHelper.ToPixel(PathThickness),
                StrokeCap = SKStrokeCap.Butt,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Solid, 2),
            };

            _positionMarkerPaint = new SKPaint
            {
                Color = SKColors.White,
                StrokeWidth = SkiaHelper.ToPixel(PathThickness * 1.5),
                Style = SKPaintStyle.Stroke,
            };
        }

        /// <summary>
        /// Initializes the SVG images.
        /// </summary>
        private void InitializeSvgImages()
        {
            _log.LogDebug($"InitializeSvgImages");

            const string StartImageName = "stopwatch-solid.svg";
            const string EndImageName = "flag-checkered-solid.svg";

            using (var stream = Embedded.Load(StartImageName))
            {
                _startImage = new SKSvg();
                _startImage.Load(stream);
            }

            using (var stream = Embedded.Load(EndImageName))
            {
                _endImage = new SKSvg();
                _endImage.Load(stream);
            }

            _pictureSize = SkiaHelper.ToPixel(20);

            if (SessionMapInfo.SessionPoints.Count > 0)
            {
                _firstImageColor = SessionMapInfo.SessionPoints.First().MapPointColor.ToSKColor();
                _lastImageColor = SessionMapInfo.SessionPoints.Last().MapPointColor.ToSKColor();
            }
            else
            {
                _firstImageColor = Color.Default.ToSKColor();
                _lastImageColor = Color.Default.ToSKColor();
            }

            _log.LogDebug($"END InitializeSvgImages");
        }

        /// <summary>
        /// Maps the on paint surface.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">
        /// The <see cref="SKPaintSurfaceEventArgs" /> instance containing the event data.
        /// </param>
        private void MapOnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            #region Initialization

            _log.LogDebug($"---MapOnPaintSurface");
            _log.LogDebug($"CameraPosition: {GoogleMap.CameraPosition.Target.Latitude}, {GoogleMap.CameraPosition.Target.Longitude}");

            // var info = e.RenderTarget;
            SKImageInfo info = e.Info;
            SKSurface surface = e.Surface;
            SKCanvas surfaceCanvas = surface.Canvas;

            var stopWatch = Stopwatch.StartNew();

            if (SessionMapInfo == null)
            {
                _log.LogDebug($"---RETURNING: SessionMapInfo is null");
                return;
            }

            // already in drawing mode
            if (_drawingMode)
            {
                _log.LogDebug($"---RETURNING: already in drawing mode");
                return;
            }

            _positionConverter.UpdateCamera(GoogleMap, new Size(info.Width, info.Height), SkiaHelper.PixelPerUnit);
            _log.LogDebug($"Camera updated: {_positionConverter.ToString()}");

            var centerPoint = _positionConverter[_centerPosition].ToSKPoint();
            var topLeftPoint = _positionConverter[_topLeftPosition].ToSKPoint();
            var bottomRightPoint = _positionConverter[_bottomRightPosition].ToSKPoint();
            double squaredDistance = SKPoint.DistanceSquared(topLeftPoint, bottomRightPoint);

            if (!_forceInvalidation && _previousCenter == centerPoint && _previousTopLeftBottomRightSquareDistance == squaredDistance)
            {
                // Display view didn't changed
                _log.LogDebug($"---RETURNING: Display view didn't changed");
                return;
            }

            _drawingMode = true;

            _previousCenter = centerPoint;
            _previousTopLeftBottomRightSquareDistance = squaredDistance;

            var pictureRecorder = new SKPictureRecorder();
            var canvas = pictureRecorder.BeginRecording(e.Info.Rect);

            InitializeMapResourcesIfNeeded();

            #endregion Initialization

            #region LineDrawing

            var sessionPoints = SessionMapInfo.SessionPoints;
            _log.LogDebug($"LineDrawing: {sessionPoints.Count} sessionPoints to draw");

            SKPoint previousPoint = SKPoint.Empty;
            SKColor previousColor = SKColor.Empty;

            int linesDrawnCount = 0;
            for (int index = 0; index < sessionPoints.Count; index++)
            {
                ISessionDisplayablePoint sessionPoint = sessionPoints[index];

                if (sessionPoint.Time > MaxTime)
                {
                    break;
                }

                SKPoint pathPoint = sessionPoint.Position != LatLong.Empty
                    ? _positionConverter[sessionPoint.Position].ToSKPoint()
                    : SKPoint.Empty;

                var pointColor = sessionPoint.MapPointColor.ToSKColor();

                // don't draw line between points with a distance < 1 dp
                bool isDistanceEnough =
                    Math.Abs(pathPoint.X - previousPoint.X) > SkiaHelper.ToPixel(4)
                    || Math.Abs(pathPoint.Y - previousPoint.Y) > SkiaHelper.ToPixel(4);

                if (previousPoint != SKPoint.Empty
                    && pathPoint != SKPoint.Empty
                    && isDistanceEnough)
                {
                    using (var shader = SKShader.CreateLinearGradient(
                        previousPoint,
                        pathPoint,
                        new[] { previousColor, pointColor },
                        null,
                        SKShaderTileMode.Clamp))
                    {
                        _gradientPathPaint.Shader = shader;

                        canvas.DrawLine(previousPoint.X, previousPoint.Y, pathPoint.X, pathPoint.Y, _gradientPathPaint);

                        _gradientPathPaint.Shader = null;
                    }

                    linesDrawnCount++;
                }

                if (isDistanceEnough)
                {
                    previousPoint = pathPoint;
                    previousColor = pointColor;
                }
            }

            #endregion LineDrawing

            #region MarkerDrawing

            // Session-Graph
            if (sessionPoints.Count > 0 && !MyLocationEnabled)
            {
                _log.LogDebug($"MarkerDrawing");

                // wenn nutzer gerade im SessionGraph scrollt.
                bool mooving = DrawFirstAndLastMarker(
                    canvas,
                    sessionPoints.First(),
                    sessionPoints.Last());

                // wenn nutzer zum Ende der Strecke gescrollt hat
                if (!mooving)
                {
                    DrawPositionMarker(canvas, previousPoint);
                }
            }

            //if (MyLocationEnabled)
            //{
            //    previousPoint = _positionConverter[_currentPosition.ToLatLong()].ToSKPoint();
            //    DrawPositionMarker(canvas, previousPoint);
            //}

            #endregion MarkerDrawing

            #region Disposing

            ReleaseMapResources();

            _overlayPicture = pictureRecorder.EndRecording();

            surfaceCanvas.Clear();
            surfaceCanvas.DrawPicture(_overlayPicture);

            _overlayPicture.Dispose();
            pictureRecorder.Dispose();

            _forceInvalidation = false;

            stopWatch.Stop();
            _log.LogDebug($"---END OF => MapOnPaintSurface ({stopWatch.Elapsed})");

            _drawingMode = false;

            #endregion Disposing
        }

        /// <summary>
        /// Called when [location updated].
        /// BUG: bei [_zoomToMyLocation] aktiviert, wird die verfolungslinie komisch gezeichnet.
        /// </summary>
        /// <param name="locationMessage">The location message.</param>
        private void OnLocationUpdated(MvxLocationMessage locationMessage)
        {
            _currentPosition = new Position(
                locationMessage.MvxCoordinates.Latitude,
                locationMessage.MvxCoordinates.Longitude);

            if (RouteDrawing)
            {
                SessionMapInfo.Add(new ActivityPoint(
                   DateTime.Now,
                   _currentPosition.ToLatLong(),
                   0,
                   (int)locationMessage.MvxCoordinates.Altitude,
                   locationMessage.MvxCoordinates.Speed));

                // neu zeichnen
                _forceInvalidation = true;
                MapOverlay.InvalidateSurface();
            }

            // nur wenn auf MyLocation gedrückt wurde, wird position verfolgt
            if (_zoomToMyLocation)
            {
                GoogleMap.MoveToRegion(
                    MapSpan.FromCenterAndRadius(
                        _currentPosition,
                        Distance.FromMeters(100)), //GoogleMap.VisibleRegion.Radius
                        true);
            }
        }

        /// <summary>
        /// Releases the map resources.
        /// </summary>
        private void ReleaseMapResources()
        {
            _positionMarkerPaint?.Dispose();
            _positionMarkerPaint = null;

            _gradientPathPaint?.Dispose();
            _gradientPathPaint = null;
        }

        #endregion Methods
    }
}