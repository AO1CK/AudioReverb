using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Numerics;

namespace AudioReverb
{
    public class SpectrumAnalyzer : Control
    {
        private float[] spectrumData;
        private readonly Pen spectrumPen;
        private readonly BufferedGraphicsContext context;
        private BufferedGraphics grafx;
        private const int FFT_SIZE = 4096;  // 增加FFT大小以获得更好的频率分辨率
        private float[] previousSpectrum;
        private const float SMOOTHING = 0.6f;  // 增加平滑系数

        public SpectrumAnalyzer()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | 
                    ControlStyles.UserPaint | 
                    ControlStyles.OptimizedDoubleBuffer, true);
            spectrumPen = new Pen(Color.Lime, 3);  // 增加线条宽度
            context = BufferedGraphicsManager.Current;
            UpdateGraphics();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            UpdateGraphics();
            base.OnSizeChanged(e);
        }

        private void UpdateGraphics()
        {
            if (Width > 0 && Height > 0)
            {
                grafx?.Dispose();
                grafx = context.Allocate(CreateGraphics(), ClientRectangle);
            }
        }

        public void UpdateSpectrum(float[] audioData)
        {
            if (audioData == null || audioData.Length == 0) return;

            // 准备FFT数据
            Complex[] fftBuffer = new Complex[FFT_SIZE];
            for (int i = 0; i < Math.Min(audioData.Length, FFT_SIZE); i++)
            {
                // 应用汉宁窗
                double window = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (FFT_SIZE - 1)));
                fftBuffer[i] = new Complex(audioData[i] * window, 0);
            }

            // 执行FFT
            FFT(fftBuffer);

            // 计算频谱
            float[] newSpectrum = new float[FFT_SIZE / 2];
            for (int i = 0; i < FFT_SIZE / 2; i++)
            {
                // 计算幅度并进行对数缩放，调整增益以匹配170-200dB范围
                float magnitude = (float)fftBuffer[i].Magnitude;
                newSpectrum[i] = (float)(20 * Math.Log10(magnitude * 200000 + 1e-10));  // 调整增益以匹配新的分贝范围
            }

            // 应用平滑
            if (previousSpectrum == null)
            {
                spectrumData = newSpectrum;
            }
            else
            {
                spectrumData = new float[FFT_SIZE / 2];
                for (int i = 0; i < FFT_SIZE / 2; i++)
                {
                    spectrumData[i] = SMOOTHING * previousSpectrum[i] + (1 - SMOOTHING) * newSpectrum[i];
                }
            }
            previousSpectrum = spectrumData;

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (grafx == null || spectrumData == null) return;

            grafx.Graphics.Clear(BackColor);
            
            // 绘制频谱
            int barCount = 32;
            int barWidth = Width / barCount;
            float maxDb = 200;    // 最大分贝值设为200dB
            float minDb = 170;    // 最小分贝值设为170dB
            int gap = 2;
            float maxHeightPercent = 0.9f;  // 最大高度限制为90%

            // 修改频率范围
            double minFreq = 20;     // 最小频率20Hz
            double maxFreq = 12000;  // 最大频率12kHz
            double sampleRate = 44100; // 标准采样率

            for (int i = 0; i < barCount; i++)
            {
                // 使用对数分布计算每个频段的起始和结束频率
                double freqStart = minFreq * Math.Pow(maxFreq / minFreq, (double)i / barCount);
                double freqEnd = minFreq * Math.Pow(maxFreq / minFreq, (double)(i + 1) / barCount);

                // 将频率转换为FFT bin索引
                int startBin = (int)Math.Round(freqStart * FFT_SIZE / sampleRate);
                int endBin = (int)Math.Round(freqEnd * FFT_SIZE / sampleRate);
                
                // 确保bin索引在有效范围内
                startBin = Math.Max(1, Math.Min(startBin, FFT_SIZE / 2 - 1));
                endBin = Math.Max(1, Math.Min(endBin, FFT_SIZE / 2 - 1));

                // 计算该频段的平均能量
                float sum = 0;
                int count = 0;
                for (int j = startBin; j < endBin; j++)
                {
                    sum += spectrumData[j];
                    count++;
                }
                float average = count > 0 ? sum / count : 0;

                // 修改高度计算，限制最大高度为90%
                float height = ((average - minDb) / (maxDb - minDb)) * Height * maxHeightPercent;
                height = Math.Max(0, Math.Min(height, Height * maxHeightPercent));  // 确保不超过90%高度

                // 使用渐变色绘制频谱条
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    new Point(0, Height),
                    new Point(0, (int)(Height * (1 - maxHeightPercent))),  // 调整渐变终点到90%高度
                    Color.FromArgb(0, 255, 0),     // 底部颜色（绿色）
                    Color.FromArgb(255, 255, 0)))   // 顶部颜色（黄色）
                {
                    grafx.Graphics.FillRectangle(brush,
                        i * barWidth + gap,
                        Height - height,
                        barWidth - gap * 2,
                        height);
                }

                // 在频谱条顶部添加小方块作为峰值指示器
                using (SolidBrush peakBrush = new SolidBrush(Color.Red))
                {
                    grafx.Graphics.FillRectangle(peakBrush,
                        i * barWidth + gap,
                        Height - height - 2,
                        barWidth - gap * 2,
                        2);
                }
            }

            grafx.Render(e.Graphics);
        }

        // FFT实现
        private void FFT(Complex[] buffer)
        {
            int bits = (int)Math.Log(buffer.Length, 2);
            for (int j = 1; j < buffer.Length / 2; j++)
            {
                int swapPos = BitReverse(j, bits);
                if (swapPos > j)
                {
                    var temp = buffer[j];
                    buffer[j] = buffer[swapPos];
                    buffer[swapPos] = temp;
                }
            }

            for (int N = 2; N <= buffer.Length; N <<= 1)
            {
                for (int i = 0; i < buffer.Length; i += N)
                {
                    for (int k = 0; k < N / 2; k++)
                    {
                        int evenIndex = i + k;
                        int oddIndex = i + k + (N / 2);
                        var even = buffer[evenIndex];
                        var odd = buffer[oddIndex];

                        double term = -2 * Math.PI * k / N;
                        Complex exp = new Complex(Math.Cos(term), Math.Sin(term)) * odd;

                        buffer[evenIndex] = even + exp;
                        buffer[oddIndex] = even - exp;
                    }
                }
            }
        }

        private int BitReverse(int n, int bits)
        {
            int reversedN = n;
            int count = bits - 1;

            n >>= 1;
            while (n > 0)
            {
                reversedN = (reversedN << 1) | (n & 1);
                count--;
                n >>= 1;
            }

            return ((reversedN << count) & ((1 << bits) - 1));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                spectrumPen.Dispose();
                grafx?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
} 