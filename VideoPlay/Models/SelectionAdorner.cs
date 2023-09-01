using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using PropertyChanged;

namespace VideoPlay.Models
{
    [DoNotNotify]
    public class SelectionAdorner : Control
    {
        private double _left;

        public double Left
        {
            get
            {
                return _left;
            }
            set
            {
                _left = value;
                this.InvalidateVisual();
            }

        }

        private double _top;
        public double Top
        {
            get
            {
                return _top;
            }
            set
            {
                _top = value;
                this.InvalidateVisual();
            }
        }

        private Control _orginControl;
        public SelectionAdorner(Control control)
        {
            _orginControl = control;
        }

        private IBrush Hibrush = Brush.Parse("#22ffffff");
        public override void Render(DrawingContext context)
        {
            var adornedElement = GetValue(AdornerLayer.AdornedElementProperty);
            if (adornedElement is null)
            {
                return;
            }

            var bounds = _orginControl.Bounds;
            var brush = new SolidColorBrush(Colors.White) { Opacity = 0.5 };
            var pen = new Pen(new SolidColorBrush(Colors.Black), 1.5);
            var r = 3.0;
            var topLeft = new EllipseGeometry(new Rect(-r + Left + bounds.Left, -r + Top + bounds.Top, r + r, r + r));
            var topRight = new EllipseGeometry(new Rect(-r + Left + bounds.Left, Top + bounds.Height - r + bounds.Top, r + r, r + r));
            var bottomLeft = new EllipseGeometry(new Rect(bounds.Width - r + Left + bounds.Left, -r + Top + bounds.Top, r + r, r + r));
            var bottomRight = new EllipseGeometry(new Rect(bounds.Width - r + Left + bounds.Left, Top + bounds.Height - r + bounds.Top, r + r, r + r));
           // var orginBrush = new VisualBrush(_orginControl);
            context.DrawRectangle(Hibrush, pen, new Rect(Left + bounds.Left, Top + bounds.Top, bounds.Width, bounds.Height));
            context.DrawGeometry(brush, pen, topLeft);
            context.DrawGeometry(brush, pen, topRight);
            context.DrawGeometry(brush, pen, bottomLeft);
            context.DrawGeometry(brush, pen, bottomRight);
        }
    }
}
