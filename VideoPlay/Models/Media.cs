using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;

namespace VideoPlay.Models
{
    public class Media:INotifyPropertyChanged
    {
        #region 预览相关

        public bool IsVisibe { get; set; }
        public Rect Position { get; set; }

        /// <summary>
        /// 比例的矩形
        /// </summary>
        public Rect ScaleRect { get; set; }

        #endregion
        public MediaContainer  MediaContainer { get; set; }

       public int LevelIndex { get; set; }
       public double TotalWidth { get; set; }
       public double ActualWidth { get; set; }
       public double Start { get; set; }
       /// <summary>
       /// 开始时间
       /// </summary>
       public TimeSpan StartTimeSpan { get; set; }

       public TimeSpan EndTimeSpan
       {
           get;
           set;
       }

       /// <summary>
       /// 持续时间，最大为视频时长
       /// </summary>
       public TimeSpan Duration { get; set; }
       /// <summary>
       /// 从视频源的起始时间，不能小于0
       /// </summary>
       public TimeSpan InternalStartTime { get; set; }
       public TimeSpan InternalEndTimeSpan
        {
           get;
           set;
       }

        public event PropertyChangedEventHandler? PropertyChanged;

       public virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
       {
           PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
       }

       protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
       {
           if (EqualityComparer<T>.Default.Equals(field, value)) return false;
           field = value;
           OnPropertyChanged(propertyName);
           return true;
       }
    }
}
