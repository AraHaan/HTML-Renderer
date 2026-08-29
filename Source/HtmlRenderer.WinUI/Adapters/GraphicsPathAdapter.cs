// "Therefore those skilled at the unorthodox
// are infinite as heaven and earth,
// inexhaustible as the great rivers.
// When they come to an end,
// they begin again,
// like the days and months;
// they die and are reborn,
// like the four seasons."
//
// - Sun Tsu,
// "The Art of War"

using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using TheArtOfDev.HtmlRenderer.Adapters;

namespace TheArtOfDev.HtmlRenderer.WinUI.Adapters
{
    /// <summary>
    /// Adapter for Win2D graphics path object for core.
    /// </summary>
    internal sealed class GraphicsPathAdapter : RGraphicsPath
    {
        /// <summary>
        /// The Win2D path builder used to accumulate the figure.
        /// </summary>
        private readonly CanvasPathBuilder _pathBuilder;

        /// <summary>
        /// The closed geometry, built lazily on first <see cref="GetClosedGeometry"/> call.
        /// </summary>
        private CanvasGeometry _geometry;

        /// <summary>
        /// whether <see cref="Start"/> has been called (a figure is open).
        /// </summary>
        private bool _figureOpen;

        public GraphicsPathAdapter(ICanvasResourceCreator resourceCreator)
        {
            _pathBuilder = new CanvasPathBuilder(resourceCreator);
        }

        public override void Start(double x, double y)
        {
            _pathBuilder.BeginFigure((float)x, (float)y);
            _figureOpen = true;
        }

        public override void LineTo(double x, double y)
        {
            _pathBuilder.AddLine((float)x, (float)y);
        }

        public override void ArcTo(double x, double y, double radiusX, double radiusY, Corner corner)
        {
            _pathBuilder.AddArc(new Vector2((float)x, (float)y), (float)radiusX, (float)radiusY, 0f, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
        }

        /// <summary>
        /// Close the geometry so no more path adding is allowed and return the instance so it can be rendered.
        /// </summary>
        public CanvasGeometry GetClosedGeometry()
        {
            if (_geometry == null)
            {
                if (_figureOpen)
                {
                    _pathBuilder.EndFigure(CanvasFigureLoop.Closed);
                    _figureOpen = false;
                }
                _geometry = CanvasGeometry.CreatePath(_pathBuilder);
            }
            return _geometry;
        }

        public override void Dispose()
        {
            _geometry?.Dispose();
            _pathBuilder.Dispose();
        }
    }
}
