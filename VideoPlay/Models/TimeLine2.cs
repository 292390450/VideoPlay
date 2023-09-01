using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using PropertyChanged;

namespace VideoPlay.Models
{
    [DoNotNotify]
    public class TimeLine2: Control
    {
        public static readonly StyledProperty<double> BigScaleProperty = StyledProperty<double>.Register<TimeLine2,double>(
            nameof(BigScale));

        public double BigScale
        {
            get { return (double)GetValue(BigScaleProperty); }
            set { SetValue(BigScaleProperty, value); }
        }
        public static readonly StyledProperty<TimeSpan> TotalTimeSpanProperty = StyledProperty<TimeSpan>.Register<TimeLine2, TimeSpan>(
            nameof(TotalTimeSpan));

        public TimeSpan TotalTimeSpan
        {
            get { return (TimeSpan)GetValue(TotalTimeSpanProperty); }
            set
            {

                SetValue(TotalTimeSpanProperty, value);
                this.Caculate();
                this.InvalidateVisual();
            }
        }


        public TimeSpan Per { get; private set; }

        private void Caculate()
        {
           
            var count = (long)(this.Bounds.Width / BigScale);
            if (count <= 0)
            {
                return;
            }
            var smalScale = BigScale / 10;
            if (TotalTimeSpan.TotalSeconds > 0)
            {
                var per = (TotalTimeSpan.TotalSeconds / count);
                Per = TimeSpan.FromSeconds(per);
                if (per <= 0)
                {
                    //不足一秒，用毫秒
                    per = (long)(TotalTimeSpan.TotalMilliseconds / count);
                    Per = TimeSpan.FromMilliseconds(per);
                }
                TimeLineChange?.Invoke(this, null);
            }

        }
        public override void Render(DrawingContext drawingContext)
        { 
            var count = (long)(this.Bounds.Width / BigScale);
            var smalScale = BigScale / 10;
            var per = (TotalTimeSpan.TotalSeconds / count);
            Per = TimeSpan.FromSeconds(per);
            if (per <= 0)
            {
                //不足一秒，用毫秒
                per = (long)(TotalTimeSpan.TotalMilliseconds / count);
                Per = TimeSpan.FromMilliseconds(per);
            }
            if (this.Bounds.Width % BigScale > 0)
            {
                //再多绘制一个
                count++;
            }

            Pen linePen = new Pen(Brushes.Black, 1);
            for (long i = 0; i < count; i++)
            {

                drawingContext.DrawLine(linePen, new Point(i * BigScale, 0), new Point(i * BigScale, 10));
                var time0 = Per * i;
                var time0Str = time0.ToString("hh\\:mm\\:ss\\:ff");
                drawingContext.DrawText(new FormattedText(time0Str, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("YaHei"), 10, Brushes.Black), new Point(i * BigScale, 10));

                drawingContext.DrawLine(linePen, new Point(i * BigScale + smalScale, 0), new Point(i * BigScale + smalScale, 5));


                drawingContext.DrawLine(linePen, new Point(i * BigScale + smalScale * 2, 0), new Point(i * BigScale + smalScale * 2, 5));


                drawingContext.DrawLine(linePen, new Point(i * BigScale + smalScale * 3, 0), new Point(i * BigScale + smalScale * 3, 5));



                drawingContext.DrawLine(linePen, new Point(i * BigScale + smalScale * 4, 0), new Point(i * BigScale + smalScale * 4, 5));


                drawingContext.DrawLine(linePen, new Point(i * BigScale + smalScale * 5, 0), new Point(i * BigScale + smalScale * 5, 5));


                drawingContext.DrawLine(linePen, new Point(i * BigScale + smalScale * 6, 0), new Point(i * BigScale + smalScale * 6, 5));


                drawingContext.DrawLine(linePen, new Point(i * BigScale + smalScale * 7, 0), new Point(i * BigScale + smalScale * 7, 5));


                drawingContext.DrawLine(linePen, new Point(i * BigScale + smalScale * 8, 0), new Point(i * BigScale + smalScale * 8, 5));

                drawingContext.DrawLine(linePen, new Point(i * BigScale + smalScale * 9, 0), new Point(i * BigScale + smalScale * 9, 5));
            }
            base.Render(drawingContext);
            TimeLineChange?.Invoke(this, null);
        }
     

        public event EventHandler<EventArgs> TimeLineChange;
    }
}
