using NAudio.Wave;
using System;

namespace AudioReverb
{
    public class ReverbEffect : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly float[] delayBuffer;
        private int delayBufferPosition;
        private readonly float decay;
        
        // 添加波形数据事件
        public event EventHandler<float[]> WaveformDataAvailable;

        public ReverbEffect(ISampleProvider source, int delayMilliseconds, float decay)
        {
            this.source = source;
            this.decay = decay;  // decay直接表示混响强度（0-1）

            // 使用固定大小的缓冲区，不考虑延迟时间
            delayBuffer = new float[source.WaveFormat.SampleRate / 10];  // 100ms的采样点数
            delayBufferPosition = 0;
        }

        public WaveFormat WaveFormat => source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = source.Read(buffer, offset, count);

            // 计算混响效果
            for (int i = 0; i < samplesRead; i++)
            {
                float currentSample = buffer[offset + i];
                
                // 从延迟缓冲区读取样本
                float delaySample = delayBuffer[delayBufferPosition];
                
                // 将当前样本和延迟样本混合，使用decay控制混响强度
                float reverbSample = currentSample + (delaySample * decay);
                
                // 更新延迟缓冲区
                delayBuffer[delayBufferPosition] = currentSample;  // 存储原始样本而不是混响后的样本
                delayBufferPosition = (delayBufferPosition + 1) % delayBuffer.Length;
                
                // 将混响后的样本写回缓冲区
                buffer[offset + i] = reverbSample;
            }

            // 触发波形数据事件
            if (samplesRead > 0)
            {
                float[] waveformData = new float[samplesRead];
                Array.Copy(buffer, offset, waveformData, 0, samplesRead);
                WaveformDataAvailable?.Invoke(this, waveformData);
            }

            return samplesRead;
        }
    }
} 