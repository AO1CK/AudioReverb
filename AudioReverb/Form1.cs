using System;
using System.Windows.Forms;
using NAudio.Wave;
using System.Drawing;
using System.Threading;

namespace AudioReverb
{
    public partial class Form1 : Form
    {
        private IWavePlayer waveOutDevice;
        private AudioFileReader audioFileReader;
        private ReverbEffect reverbEffect;
        private System.Windows.Forms.Timer timer;
        private TrackBar trackProgress;
        private Label lblTime;
        private Label label2;  // 添加label2声明
        private bool isDragging = false;
        private float currentDecay = 0.5f;  // 添加混响强度变量
        private System.Windows.Forms.Button btnIncreaseReverb;
        private System.Windows.Forms.Button btnDecreaseReverb;
        private int reverbValue = 0;
        private Button btnPause;  // 在类的开头添加
        private Button btnSave;  // 在类的开头添加
        private string currentFileName;  // 用于保存当前文件名
        private SpectrumAnalyzer spectrumAnalyzer;
        private float[] audioData = new float[2048];
        private int audioDataIndex = 0;
        private System.Windows.Forms.Timer spectrumTimer;


        public Form1()
        {
            this.Text = "音频混响处理";
            this.Size = new Size(500, 280);  // 增加窗体高度
            this.MaximizeBox = false;  // 禁用最大化按钮
            this.FormBorderStyle = FormBorderStyle.FixedSingle;  // 禁止调整窗体大小
            this.StartPosition = FormStartPosition.CenterScreen;  // 窗体居中显示

            // 创建控件
            Button btnPlay = new Button
            {
                Text = "打开文件",
                Location = new Point(10, 10),
                Size = new Size(80, 30)
            };

            Button btnStop = new Button
            {
                Text = "卸载音频",
                Location = new Point(200, 10),
                Size = new Size(80, 30)
            };

            // 添加暂停按钮
            btnPause = new Button
            {
                Text = "播放/暂停",
                Location = new Point(100, 10),
                Size = new Size(90, 30)
            };

            // 添加保存按钮
            btnSave = new Button
            {
                Text = "保存音频",
                Location = new Point(290, 10),
                Size = new Size(80, 30)
            };


            // 进度条
            trackProgress = new TrackBar
            {
                Location = new Point(10, 60),
                Size = new Size(460, 20),
                Minimum = 0,
                Maximum = 100
            };

            // 时间标签
            lblTime = new Label
            {
                Location = new Point(20, 40),
                AutoSize = true,
                Text = "00:00 / 00:00"
            };


            // 创建混响强度显示标签
            label2 = new Label
            {
                Text = "混响强度: 0%",
                Location = new Point(15, 117),
                AutoSize = true
            };

            // 创建频谱分析器控件
            spectrumAnalyzer = new SpectrumAnalyzer
            {
                Location = new Point(10, 140),
                Size = new Size(460, 60),
                BackColor = Color.Black
            };

            // 添加两个按钮控件
            this.btnIncreaseReverb = new System.Windows.Forms.Button();
            this.btnDecreaseReverb = new System.Windows.Forms.Button();

            // 配置增加按钮
            this.btnIncreaseReverb.Location = new System.Drawing.Point(160, 115);
            this.btnIncreaseReverb.Name = "btnIncreaseReverb";
            this.btnIncreaseReverb.Size = new System.Drawing.Size(30, 23);
            this.btnIncreaseReverb.Text = "+";
            this.btnIncreaseReverb.Click += new System.EventHandler(this.btnIncreaseReverb_Click);
            
            // 配置减少按钮
            this.btnDecreaseReverb.Location = new System.Drawing.Point(130, 115);
            this.btnDecreaseReverb.Name = "btnDecreaseReverb";
            this.btnDecreaseReverb.Size = new System.Drawing.Size(30, 23);
            this.btnDecreaseReverb.Text = "-";
            this.btnDecreaseReverb.Click += new System.EventHandler(this.btnDecreaseReverb_Click);
            
            
            // 将按钮添加到控件集合
            this.Controls.Add(this.btnIncreaseReverb);
            this.Controls.Add(this.btnDecreaseReverb);

            // 添加控件到窗体
            this.Controls.AddRange(new Control[] {
                btnPlay, btnStop, btnPause, btnSave,
                trackProgress, lblTime, label2, spectrumAnalyzer,

            });

            // 添加事件处理
            btnPlay.Click += BtnPlay_Click;
            btnStop.Click += BtnStop_Click;
            btnPause.Click += BtnPause_Click;
            btnSave.Click += BtnSave_Click;


            // 进度条事件
            trackProgress.MouseDown += (s, e) => isDragging = true;
            trackProgress.MouseUp += TrackProgress_MouseUp;

            // 初始化定时器
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 100;
            timer.Tick += Timer_Tick;

            // 初始化频谱更新定时器
            spectrumTimer = new System.Windows.Forms.Timer();
            spectrumTimer.Interval = 100;  // 设置为100毫秒，即0.1秒
            spectrumTimer.Tick += SpectrumTimer_Tick;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (audioFileReader != null && !isDragging)
            {
                double progress = (audioFileReader.Position * 100.0) / audioFileReader.Length;
                trackProgress.Value = (int)progress;

                TimeSpan currentTime = TimeSpan.FromSeconds(audioFileReader.CurrentTime.TotalSeconds);
                TimeSpan totalTime = TimeSpan.FromSeconds(audioFileReader.TotalTime.TotalSeconds);
                lblTime.Text = $"{currentTime:mm\\:ss} / {totalTime:mm\\:ss}";
            }
        }

        private void TrackProgress_MouseUp(object sender, MouseEventArgs e)
        {
            if (audioFileReader != null)
            {
                isDragging = false;
                double position = (trackProgress.Value * audioFileReader.Length) / 100.0;
                audioFileReader.Position = (long)position;
            }
        }

        private void BtnPlay_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "音频文件|*.mp3;*.wav";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    StopAudio();
                    
                    currentFileName = ofd.FileName;
                    audioFileReader = new AudioFileReader(ofd.FileName);
                    reverbEffect = new ReverbEffect(audioFileReader, 100, 0);
                    reverbEffect.WaveformDataAvailable += ReverbEffect_WaveformDataAvailable;
                    audioDataIndex = 0;
                    reverbValue = 0;
                    UpdateReverbLabel();
                    waveOutDevice = new WaveOutEvent();
                    waveOutDevice.Init(reverbEffect);
                    waveOutDevice.Play();

                    timer.Start();
                    spectrumTimer.Start();  // 启动频谱更新定时器
                }
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            StopAudio();
            trackProgress.Value = 0;
            lblTime.Text = "00:00 / 00:00";
        }

        private void BtnPause_Click(object sender, EventArgs e)
        {
            if (waveOutDevice != null)
            {
                if (waveOutDevice.PlaybackState == PlaybackState.Playing)
                {
                    waveOutDevice.Pause();
                    btnPause.Text = "继续";
                }
                else if (waveOutDevice.PlaybackState == PlaybackState.Paused)
                {
                    waveOutDevice.Play();
                    btnPause.Text = "暂停";
                }
            }
        }

        private void StopAudio()
        {
            timer.Stop();
            spectrumTimer.Stop();  // 停止频谱更新定时器

            if (waveOutDevice != null)
            {
                waveOutDevice.Stop();
                waveOutDevice.Dispose();
                waveOutDevice = null;
            }
            if (audioFileReader != null)
            {
                if (reverbEffect != null)
                {
                    reverbEffect.WaveformDataAvailable -= ReverbEffect_WaveformDataAvailable;
                }
                audioFileReader.Dispose();
                audioFileReader = null;
            }
            
            btnPause.Text = "播放/暂停";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            timer.Stop();
            spectrumTimer.Stop();
            StopAudio();
            base.OnFormClosing(e);
        }

        private void btnIncreaseReverb_Click(object sender, EventArgs e)
        {
            if (reverbValue < 100)
            {
                reverbValue += 5;
                UpdateReverbLabel();
                // 更新混响效果
                if (waveOutDevice != null)
                {
                    waveOutDevice.Stop();
                    // 保存当前的事件处理程序
                    var oldEffect = reverbEffect;
                    reverbEffect = new ReverbEffect(audioFileReader, 100, reverbValue / 100f);
                    // 重新绑定事件处理程序
                    reverbEffect.WaveformDataAvailable += ReverbEffect_WaveformDataAvailable;
                    waveOutDevice.Init(reverbEffect);
                    waveOutDevice.Play();
                    // 解除旧的事件绑定并释放资源
                    if (oldEffect != null)
                    {
                        oldEffect.WaveformDataAvailable -= ReverbEffect_WaveformDataAvailable;
                    }
                }
            }
        }

        private void btnDecreaseReverb_Click(object sender, EventArgs e)
        {
            if (reverbValue > 0)
            {
                reverbValue -= 5;
                UpdateReverbLabel();
                // 更新混响效果
                if (waveOutDevice != null)
                {
                    waveOutDevice.Stop();
                    // 保存当前的事件处理程序
                    var oldEffect = reverbEffect;
                    reverbEffect = new ReverbEffect(audioFileReader, 100, reverbValue / 100f);
                    // 重新绑定事件处理程序
                    reverbEffect.WaveformDataAvailable += ReverbEffect_WaveformDataAvailable;
                    waveOutDevice.Init(reverbEffect);
                    waveOutDevice.Play();
                    // 解除旧的事件绑定并释放资源
                    if (oldEffect != null)
                    {
                        oldEffect.WaveformDataAvailable -= ReverbEffect_WaveformDataAvailable;
                    }
                }
            }
        }

        private void UpdateReverbLabel()
        {
            label2.Text = $"混响强度: {reverbValue}%";
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (audioFileReader == null)
            {
                MessageBox.Show("请先打开音频文件！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                string extension = System.IO.Path.GetExtension(currentFileName);
                string fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(currentFileName);
                string defaultFileName = $"{fileNameWithoutExt}_混响{reverbValue}{extension}";

                sfd.FileName = defaultFileName;
                sfd.Filter = "音频文件|*.wav";  // 目前只支持保存为WAV格式

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // 保存当前播放状态
                        bool wasPlaying = waveOutDevice?.PlaybackState == PlaybackState.Playing;
                        long currentPosition = audioFileReader.Position;

                        // 暂停播放
                        if (wasPlaying)
                        {
                            waveOutDevice.Pause();
                        }

                        // 创建新的音频流用于保存
                        audioFileReader.Position = 0;
                        var newEffect = new ReverbEffect(audioFileReader, 100, reverbValue / 100f);
                        WaveFileWriter.CreateWaveFile16(sfd.FileName, newEffect);

                        // 恢复播放状态
                        audioFileReader.Position = currentPosition;
                        if (wasPlaying)
                        {
                            waveOutDevice.Play();
                        }

                        MessageBox.Show("音频保存成功！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"保存失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ReverbEffect_WaveformDataAvailable(object sender, float[] data)
        {
            // 只更新音频数据，不直接更新频谱显示
            for (int i = 0; i < Math.Min(data.Length, audioData.Length); i++)
            {
                audioData[audioDataIndex] = data[i];
                audioDataIndex = (audioDataIndex + 1) % audioData.Length;
            }
        }

        private void SpectrumTimer_Tick(object sender, EventArgs e)
        {
            // 更新频谱显示
            if (spectrumAnalyzer.InvokeRequired)
            {
                spectrumAnalyzer.BeginInvoke(new Action(() => spectrumAnalyzer.UpdateSpectrum(audioData)));
            }
            else
            {
                spectrumAnalyzer.UpdateSpectrum(audioData);
            }
        }


    }
}