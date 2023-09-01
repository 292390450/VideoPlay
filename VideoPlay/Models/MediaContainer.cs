using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using FFmpeg.AutoGen;

namespace VideoPlay.Models
{
    public unsafe class MediaContainer:IDisposable,INotifyPropertyChanged
    {
        private Stopwatch _stopwatch;
        private double _playInterval;
        public WriteableBitmap WriteableBitmap { get; private set; }
        private DBBNAudioRenderer audioRenderer;
        public TimeSpan Duration { get; private set; }
        public long CurrentTime { get; private set; }
        public TimeSpan VideoFrameDuration { get; private set; }
        public PixelSize FrameSize { get; private set; }
        /// <summary>
        /// 核心上下文
        /// </summary>
        AVFormatContext* fmt_ctx = null;

        private long maxInter;
        private int videoIndex;
        private int audioIndex;

        private AVCodecContext* videoCodecContext;
        private AVCodecContext* audioCodecContext;
        private AVRational videoTimebase;
        private AVFrame* recieveframe;
        private AVFrame* pFrame;
        private AVPacket* pPacket;

        private int sample_rate;
        private SwrContext* audio_convert_ctx;
        int out_nb_channels;
        private int out_sample_rate = 44100;
        AVSampleFormat out_sample_fmt = AVSampleFormat.AV_SAMPLE_FMT_S16;//输出的采样格式 16bit PCM
        ulong out_ch_layout = ffmpeg.AV_CH_LAYOUT_STEREO;//输出的声道布局：立体声
        private int out_sanmple_perchannel = 0;
        int AudioBufferPadding => 256;
        //输出缓存大小
        int audio_out_buffer_size;
        //输出缓存
        byte* audio_out_buffer;
        public VideoFrameConverter VideoFrameConverter { get; private set; }
        public AVFrame CurrentVideoFrame { get; private set; }
        private bool isOpen;
        private bool isDisposable;
        public string FileName { get; private set; }

        public bool IsPlay { get; private set; }

        /// <summary>
        /// 传一个时钟进来
        /// </summary>
        /// <param name="stopwatch"></param>
        public MediaContainer(Stopwatch stopwatch)
        {
            _stopwatch = stopwatch;
            ParseAllFrame();
        }
        public void OpenFile(string path)
        {
            if (isOpen)
            {
                return;
            }

            isOpen = true;
            FileName = System.IO.Path.GetFileName(path);
            //解析视频，获取第一真
            fmt_ctx =ffmpeg.avformat_alloc_context();  //申请上下文
            var fmt_ctxP = fmt_ctx;
            var fmt = fmt_ctxP;
            //传输层类型
            AVDictionary* opts = null;
            // FFmpeg.AutoGen.ffmpeg.av_dict_set(&opts, "rtsp_transport", "tcp", 0); // here "udp" can replaced by "tcp"
            ffmpeg.avformat_open_input(&fmt, path, null, null).ThrowExceptionIfError();
            //查找流
           ffmpeg.avformat_find_stream_info(fmt_ctx, null).ThrowExceptionIfError();
           Duration =TimeSpan.FromMilliseconds( fmt_ctx->duration/1000.0);
           AVCodec* video_codec = null;
           AVCodec* audio_codec = null;
            //找到流队列中，视频流所在位置
            try
            {
                videoIndex = FFmpeg.AutoGen.ffmpeg.av_find_best_stream(fmt_ctx, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &video_codec, 0).ThrowExceptionIfError();
                videoCodecContext = ffmpeg.avcodec_alloc_context3(video_codec);

                ffmpeg.av_hwdevice_ctx_create(&videoCodecContext->hw_device_ctx, AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2, null, null, 0).ThrowExceptionIfError();

                ffmpeg.avcodec_parameters_to_context(videoCodecContext, fmt->streams[videoIndex]->codecpar).ThrowExceptionIfError();
                videoTimebase = fmt->streams[videoIndex]->time_base;
                VideoFrameDuration = TimeSpan.FromSeconds(1.0 / fmt_ctx->streams[videoIndex]->avg_frame_rate.num);
                ffmpeg.avcodec_open2(videoCodecContext, video_codec, null).ThrowExceptionIfError();


                audioIndex= FFmpeg.AutoGen.ffmpeg.av_find_best_stream(fmt_ctx, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, &audio_codec, 0).ThrowExceptionIfError();
                if (audioIndex>=0)
                {
                    audioCodecContext = ffmpeg.avcodec_alloc_context3(audio_codec);
                    ffmpeg.avcodec_parameters_to_context(audioCodecContext, fmt_ctx->streams[audioIndex]->codecpar).ThrowExceptionIfError();
                    ffmpeg.avcodec_open2(audioCodecContext, audio_codec, null).ThrowExceptionIfError();
                    //======音频转码准备======start======
                    var in_sample_fmt = audioCodecContext->sample_fmt;//输入的采样格式
                    sample_rate = audioCodecContext->sample_rate;//输入的采样率
                    var in_ch_layout = audioCodecContext->channel_layout;//输入的声道布局
                    audio_convert_ctx = ffmpeg.swr_alloc(); //申请转换器上下文
                    ffmpeg.swr_alloc_set_opts(audio_convert_ctx, (long)out_ch_layout, out_sample_fmt, out_sample_rate, (long)in_ch_layout, in_sample_fmt, sample_rate, 0, null);
                    ffmpeg.swr_init(audio_convert_ctx);  //初始化
                    out_nb_channels = ffmpeg.av_get_channel_layout_nb_channels((ulong)out_ch_layout);//获取声道个数
                    //======音频转码准备======end======
                    audioRenderer = new DBBNAudioRenderer();
                    audioRenderer.Init(out_sample_rate, 16, out_nb_channels, 10);
                }

                recieveframe = ffmpeg.av_frame_alloc();
                pPacket = ffmpeg.av_packet_alloc();
                pFrame = ffmpeg.av_frame_alloc();
                var par = fmt_ctx->streams[videoIndex]->codecpar;

                FrameSize = new PixelSize(par->width, par->height);
                WriteableBitmap =
                    new WriteableBitmap(FrameSize,new Vector(96,96), PixelFormats.Bgra8888);
                //创建解码
                VideoFrameConverter = new VideoFrameConverter(FrameSize, AVPixelFormat.AV_PIX_FMT_NV12, FrameSize,
                    AVPixelFormat.AV_PIX_FMT_BGRA);
                maxInter=(long)(0.5* videoTimebase.den / videoTimebase.num);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public bool TryGetFrame(TimeSpan time)
        {
            if (!isOpen)
            {
                return false;
            }
            if (time>Duration)
            {
                time = Duration;
            }
            var timesanpe = (long)(time.TotalSeconds * videoTimebase.den / videoTimebase.num);
            bool success = false;
            if (timesanpe > CurrentTime&& timesanpe < (CurrentTime+ maxInter))
            {
                success = TryDecodeNextFrame();
                return success;
            }
            else if (timesanpe != CurrentTime)
            {
                if (timesanpe <= (CurrentTime - maxInter)|| timesanpe >= (CurrentTime + maxInter))
                {
                    //跳转

                    ffmpeg.av_seek_frame(fmt_ctx, videoIndex, (long)(timesanpe), ffmpeg.AVSEEK_FLAG_BACKWARD|ffmpeg.AVSEEK_FLAG_FRAME);
                    ffmpeg.avcodec_flush_buffers(videoCodecContext);
                }
                //读到时间为止
                do
                {
                    success= TryDecodeNextFrame();
                } while (CurrentTime < timesanpe);
            }
            else
            {
                
            }
            //
            Render(CurrentVideoFrame);
            return success;
        }
        public bool TryDecodeNextFrame()
        {
            if (!isOpen)
            {
                return false;
            }
            ffmpeg.av_frame_unref(pFrame);
            ffmpeg.av_frame_unref(recieveframe);
            int error;
            do
            {
                try
                {
                    do
                    {
                        error = ffmpeg.av_read_frame(fmt_ctx, pPacket);
                        if (error == ffmpeg.AVERROR_EOF)
                        {
                            CurrentTime = (long)(Duration.TotalSeconds * videoTimebase.den / videoTimebase.num);
                            CurrentVideoFrame = *pFrame;
                            return false;
                        }

                        error.ThrowExceptionIfError();
                    } while (pPacket->stream_index != videoIndex);
                    var res=  ffmpeg.avcodec_send_packet(videoCodecContext, pPacket);
                  
                }
                finally
                {
                    ffmpeg.av_packet_unref(pPacket);
                }
                error = ffmpeg.avcodec_receive_frame(videoCodecContext, pFrame);
            } while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN));
            error.ThrowExceptionIfError();
            if (videoCodecContext->hw_device_ctx != null)
            {
                ffmpeg.av_hwframe_transfer_data(recieveframe, pFrame, 0).ThrowExceptionIfError();
                recieveframe->pkt_duration = pFrame->pkt_duration;
                recieveframe->pkt_pos = pFrame->pkt_pos;
                recieveframe->pkt_dts = pFrame->pkt_dts;
                recieveframe->pkt_size = pFrame->pkt_size;
                recieveframe->pts = pFrame->pts;
                //_receivedFrame->best_effort_timestamp = _pFrame->best_effort_timestamp;
                CurrentVideoFrame = *recieveframe;
            }
            else
            {
                CurrentVideoFrame = *pFrame;
            }
            CurrentTime = pFrame->pts;
            //渲染
            return true;
        }

     
        public void ParseAllFrame()
        {
            Task.Run((() =>
            {
                AVFrame* original_audio_frame = ffmpeg.av_frame_alloc();
                AVFrame* original_video_frame = ffmpeg.av_frame_alloc();
                AVFrame* receive_video_frame = ffmpeg.av_frame_alloc();
                while (!isDisposable)
                {
                    int error = 0;
                    if(IsPlay)
                    {
                        try
                        {
                            do
                            {
                                error = ffmpeg.av_read_frame(fmt_ctx, pPacket);
                                if (error == ffmpeg.AVERROR_EOF) break;
                                if (error < 0) error.ThrowExceptionIfError();
                                if (pPacket->stream_index == videoIndex) //视频帧
                                {
                                    // 解码
                                    error = ffmpeg.avcodec_send_packet(videoCodecContext, pPacket);
                                    if (error < 0) error.ThrowExceptionIfError();
                                    // 解码输出解码数据
                                    error = ffmpeg.avcodec_receive_frame(videoCodecContext, original_video_frame);
                                }

                                if (pPacket->stream_index == audioIndex) //声音帧
                                {
                                    // 解码
                                    error = ffmpeg.avcodec_send_packet(audioCodecContext, pPacket);
                                    if (error < 0) error.ThrowExceptionIfError();
                                    // 解码输出解码数据
                                    error = ffmpeg.avcodec_receive_frame(audioCodecContext, original_audio_frame);
                                }
                            } while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN));

                            if (!IsPlay)
                            {
                                //停止了
                                continue;
                            }
                            if (error == ffmpeg.AVERROR_EOF)
                            {
                                CurrentTime = (long)(Duration.TotalSeconds * videoTimebase.den / videoTimebase.num);
                                IsPlay = false;
                                continue;
                            }
                            if (error < 0) error.ThrowExceptionIfError();
                            if (pPacket->stream_index == videoIndex)
                            {
                                var times = GetCurrentTimeSpan();
                                var inter = _stopwatch.ElapsedMilliseconds - times.TotalMilliseconds;
                                if (videoCodecContext->hw_device_ctx != null)
                                {
                                    if (!IsPlay)
                                    {
                                        //停止了
                                        continue;
                                    }
                                    ffmpeg.av_hwframe_transfer_data(receive_video_frame, original_video_frame, 0).ThrowExceptionIfError();
                                    receive_video_frame->pkt_duration = pFrame->pkt_duration;
                                    receive_video_frame->pkt_pos = pFrame->pkt_pos;
                                    receive_video_frame->pkt_dts = pFrame->pkt_dts;
                                    receive_video_frame->pkt_size = pFrame->pkt_size;
                                    receive_video_frame->pts = pFrame->pts;
                                    //_receivedFrame->best_effort_timestamp = _pFrame->best_effort_timestamp;
                                    CurrentVideoFrame = *receive_video_frame;
                                }
                                else
                                {
                                    CurrentVideoFrame = *original_video_frame;
                                }
                                if (_playInterval <= inter)// 变大了
                                {

                                }
                                else
                                {
                                    Thread.Sleep((int)((_playInterval - inter)));
                                    if (!IsPlay)
                                    {
                                        //停止了
                                        continue;
                                    }
                                }

                                if (videoCodecContext->hw_device_ctx != null)
                                {
                                    if (receive_video_frame->width>0&& receive_video_frame->height>0)
                                    {
                                        Render(*receive_video_frame);
                                    }
                                   
                                }
                                else
                                {
                                    Render(*original_video_frame);
                                }
                              
                                CurrentTime = pFrame->pts;
                            }

                            if (pPacket->stream_index == audioIndex)
                            {
                                if (out_sanmple_perchannel == 0)
                                {

                                    out_sanmple_perchannel = Convert.ToInt32(Convert.ToDouble(original_audio_frame->nb_samples) * out_sample_rate / sample_rate);
                                    audio_out_buffer_size = ffmpeg.av_samples_get_buffer_size(null, out_nb_channels,
                                        out_sanmple_perchannel + AudioBufferPadding,
                                        out_sample_fmt, 0);
                                    audio_out_buffer = (byte*)ffmpeg.av_malloc((ulong)audio_out_buffer_size);//存储pcm数据
                                }

                                var audio_out = audio_out_buffer;

                                //音频格式转换
                                var count = ffmpeg.swr_convert(audio_convert_ctx,//音频转换上下文
                                    &audio_out,//输出缓存
                                    out_sanmple_perchannel,//每次输出大小
                                    original_audio_frame->extended_data,//输入数据
                                    original_audio_frame->nb_samples);//输入
                                var outputBufferLength =
                                    ffmpeg.av_samples_get_buffer_size(null, out_nb_channels, count, out_sample_fmt, 1);
                                Span<byte> buff = new Span<byte>(audio_out_buffer, outputBufferLength);
                                audioRenderer.Render(buff.ToArray());

                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                        finally
                        {
                            ffmpeg.av_packet_unref(pPacket);//释放数据包对象引用
                        }
                    }else
                    {
                        Thread.Sleep(10);
                    }
                }
                ffmpeg.av_frame_unref(original_audio_frame);
                ffmpeg.av_free(original_audio_frame);
                ffmpeg.av_frame_unref(original_video_frame);
                ffmpeg.av_free(original_video_frame);
                ffmpeg.av_frame_unref(receive_video_frame);
                ffmpeg.av_free(receive_video_frame);
            }));
        }

        public void Play()
        {
            if (IsPlay)
            {
                return;
            }
            var times = GetCurrentTimeSpan();
            _playInterval = _stopwatch.ElapsedMilliseconds - times.TotalMilliseconds;
            IsPlay = true;
        
        }

        public void Stop()
        {
            IsPlay = false;
        }
       
        private void Render(AVFrame yuvFrame)
        {
            var rgba = this.VideoFrameConverter.Convert(yuvFrame);
           
            if (Dispatcher.UIThread.CheckAccess())
            {
               
                using (var buff = WriteableBitmap.Lock())
                {
                    Unsafe.CopyBlock(buff.Address.ToPointer(), rgba.data[0], (uint)VideoFrameConverter.BuffSize);
                }
                //通知很奇怪
                OnPropertyChanged(nameof(WriteableBitmap));
            }
            else
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    using (var buff = WriteableBitmap.Lock())
                    {
                        Unsafe.CopyBlock(buff.Address.ToPointer(), rgba.data[0], (uint)VideoFrameConverter.BuffSize);
                    }
                    //通知很奇怪
                    OnPropertyChanged(nameof(WriteableBitmap));
                });
            }
          
        }

        public TimeSpan GetCurrentTimeSpan()
        {
           return  TimeSpan.FromSeconds(CurrentTime*1.0 * videoTimebase.num / videoTimebase.den);
        }
        public void Dispose()
        {
            if (!isOpen)
            {
                return;
            }

            if (isDisposable)
            {
                return;
            }

            isDisposable = true;
            ffmpeg.av_frame_unref(pFrame);
            ffmpeg.av_free(pFrame);
            ffmpeg.av_frame_unref(recieveframe);
            ffmpeg.av_free(recieveframe);
            ffmpeg.av_packet_unref(pPacket);
            ffmpeg.av_free(pPacket);

            ffmpeg.avcodec_close(videoCodecContext);
            var pFormatContext = fmt_ctx;
            ffmpeg.avformat_close_input(&pFormatContext);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
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
    internal static class FFmpegHelper
    {
        public static unsafe string av_strerror(int error)
        {
            var bufferSize = 1024;
            var buffer = stackalloc byte[bufferSize];
            FFmpeg.AutoGen.ffmpeg.av_strerror(error, buffer, (ulong)bufferSize);
            var message = Marshal.PtrToStringAnsi((IntPtr)buffer);
            return message;
        }

        public static int ThrowExceptionIfError(this int error)
        {
            if (error < 0) throw new ApplicationException(av_strerror(error));
            return error;
        }
    }
}
