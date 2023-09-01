using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using PropertyChanged;
using VideoPlay.Models;

namespace VideoPlay;
[DoNotNotify]
public partial class MediaDuraControl : UserControl
{
    private TimeLine2 _timeLine2;
    public MediaDuraControl(TimeLine2 timeLine2)
    {
        InitializeComponent();
        _timeLine2 = timeLine2;
    }
    private void LeftDrag(object sender, VectorEventArgs e)
    {



        var data = this.DataContext as Media;
        if (data == null)
        {
            return;
        }
        //�������ط���ʱ��
        var rem = e.Vector.X / _timeLine2.BigScale * _timeLine2.Per;
        if (e.Vector.X >= 0)
        {
            //  ���Ҳ���ʹʱ������0
            if (data.Duration >= TimeSpan.FromSeconds(0.5))
            {
                if (data.InternalEndTimeSpan - data.InternalStartTime.Add(rem) < TimeSpan.FromSeconds(0.5))
                {
                    data.InternalStartTime = data.InternalEndTimeSpan - TimeSpan.FromSeconds(0.5);
                    data.Duration = TimeSpan.FromSeconds(0.5);
                    data.StartTimeSpan = data.EndTimeSpan - data.Duration;
                }
                else
                {
                    data.InternalStartTime = data.InternalStartTime.Add(rem);
                    data.Duration = data.InternalEndTimeSpan - data.InternalStartTime;
                    data.StartTimeSpan = data.StartTimeSpan.Add(rem);
                }
                //ʱ�����ϵ�ʱ��

            }
        }
        else
        {

            //�������ڲ���ʼʱ��Ϊ0��
            if (data.InternalStartTime > TimeSpan.Zero)
            {
                if (data.InternalStartTime.Add(rem) < TimeSpan.Zero)
                {
                    data.InternalStartTime = TimeSpan.Zero;
                    data.Duration = data.InternalEndTimeSpan;
                    data.StartTimeSpan = data.EndTimeSpan - data.Duration;
                }
                else
                {
                    data.InternalStartTime = data.InternalStartTime.Add(rem);
                    data.Duration = data.InternalEndTimeSpan - data.InternalStartTime;
                    data.StartTimeSpan = data.StartTimeSpan.Add(rem);
                }


            }
        }
        //������canvas�ϵ�������
        var x = data.StartTimeSpan / _timeLine2.Per * _timeLine2.BigScale;
        var wi = data.Duration / _timeLine2.Per * _timeLine2.BigScale;
        if (x < 0)
        {

        }
        data.Start = x;
        data.ActualWidth = wi;
        Canvas.SetLeft(this, data.Start);
        this.Width = data.ActualWidth;
    }

    private void RightDrag(object sender, VectorEventArgs e)
    {

        var data = this.DataContext as Media;
        if (data == null)
        {
            return;
        }
        //�������ط���ʱ��
        var rem = e.Vector.X / _timeLine2.BigScale * _timeLine2.Per;
        if (e.Vector.X >= 0)
        {
            //����
            if (data.InternalEndTimeSpan < data.MediaContainer.Duration)
            {
                if (data.InternalEndTimeSpan.Add(rem) > data.MediaContainer.Duration)
                {
                    data.InternalEndTimeSpan = data.MediaContainer.Duration;
                    data.Duration = data.InternalEndTimeSpan - data.InternalStartTime;
                    data.EndTimeSpan = data.StartTimeSpan + data.Duration;
                }
                else
                {
                    data.InternalEndTimeSpan = data.InternalEndTimeSpan.Add(rem);
                    data.Duration = data.InternalEndTimeSpan - data.InternalStartTime;
                    data.EndTimeSpan = data.StartTimeSpan + data.Duration;
                }
            }
        }
        else
        {
            //��������ʱ������һ������
            if (data.Duration >= TimeSpan.FromSeconds(0.5))
            {
                if (data.Duration.Add(rem) < TimeSpan.FromSeconds(0.5))
                {
                    data.InternalEndTimeSpan = data.InternalStartTime + TimeSpan.FromSeconds(0.5);
                    data.Duration = TimeSpan.FromSeconds(0.5);
                    data.EndTimeSpan = data.StartTimeSpan + data.Duration;
                }
                else
                {
                    data.InternalEndTimeSpan = data.InternalEndTimeSpan.Add(rem);
                    data.Duration = data.InternalEndTimeSpan - data.InternalStartTime;
                    data.EndTimeSpan = data.StartTimeSpan + data.Duration;
                }
            }
        }

        var wi = data.Duration / _timeLine2.Per * _timeLine2.BigScale;
        data.ActualWidth = wi;
        this.Width = data.ActualWidth;
    }
}