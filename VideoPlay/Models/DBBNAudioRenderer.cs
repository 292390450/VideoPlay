using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VideoPlay.Models
{
    public class DBBNAudioRenderer 
    {
        private bool _isInited = false;
        private IWavePlayer waveOut;
        private BufferedWaveProvider bufferedWaveProvider;
        private SampleChannel sampleChannel;
        TimeSpan audioMaxBufferedDuration = new TimeSpan(0, 0, 0, 0, 600); //音频最大缓冲时间

        public int SampleRate { get; }

        public int Bits { get; }

        public int Channels { get; }
        public Action<float[]> PreVolumeMeter { get; set; }
        public void Close()
        {
           
        }

        public void Dispose()
        {
            waveOut.Stop();
            bufferedWaveProvider.ClearBuffer();
            waveOut.Dispose();
        }

        public void Init(int sampleRate, int bitsPerSample, int channels,double defaultVolume)
        {

            waveOut = new DirectSoundOut();
            WaveFormat format = null;
            if (sampleRate!=0)
            {
                format=new WaveFormat(sampleRate,channels);
            }
            else
            {
                format=new WaveFormat();
            }
            bufferedWaveProvider = new BufferedWaveProvider(format);
            sampleChannel = new SampleChannel(bufferedWaveProvider, false);
            sampleChannel.Volume = (float)(defaultVolume/10.0);
            sampleChannel.PreVolumeMeter += SampleChannel_PreVolumeMeter;
            waveOut.Init(sampleChannel);
            waveOut.Play();
            _isInited = true;
        }

        private void SampleChannel_PreVolumeMeter(object sender, StreamVolumeEventArgs e)
        {
            PreVolumeMeter?.Invoke(e.MaxSampleValues);
        }

        public void Render(byte[] buff)
        {
            if (!_isInited)
            {
                return;
            }
            if (bufferedWaveProvider.BufferedDuration.CompareTo(audioMaxBufferedDuration) > 0)
            {
                bufferedWaveProvider.ClearBuffer();
            }
            bufferedWaveProvider.AddSamples(buff, 0, buff.Length);//向缓存中添加音频样本
        }

        public void VolumeChange(double volume)
        {
            sampleChannel.Volume =(float) (volume/10.0);
        }
    }
}
