using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using NAudio.Wave;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ChaosAssetManager.Controls.PreviewControls;

public sealed partial class AudioPlayer : IDisposable
{
    private readonly Stream AudioStream;
    private readonly bool FinishedLoading;
    private readonly MediaFoundationReader MediaReader;
    private readonly Lock Sync;
    private readonly PeriodicTimer Timer;
    private readonly IWavePlayer WaveOutDevice;
    private int Disposed;
    private long StartTimeStamp;

    // ReSharper disable once NotAccessedField.Local
    private Task TimerTask;

    public AudioPlayer(Stream audioStream)
    {
        InitializeComponent();

        Sync = new Lock();
        AudioStream = audioStream;
        MediaReader = new StreamMediaFoundationReader(AudioStream);
        WaveOutDevice = new WaveOutEvent();
        WaveOutDevice.Init(MediaReader);
        WaveOutDevice.Volume = (float)VolumeSlider.Value;
        WaveOutDevice.PlaybackStopped += WaveOutDeviceOnPlaybackStopped;
        FinishedLoading = true;
        Timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1));

        TimerTask = UpdateProgress();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        using var @lock = Sync.EnterScope();

        if (Interlocked.CompareExchange(ref Disposed, 1, 0) == 1)
            return;

        AudioStream.Dispose();
        WaveOutDevice.Dispose();
        MediaReader.Dispose();
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        using var @lock = Sync.EnterScope();

        WaveOutDevice.Play();
        StartTimeStamp = Stopwatch.GetTimestamp() + MediaReader.CurrentTime.Ticks;
    }

    private void ProgressSlider_OnPreviewKeyDown(object sender, KeyEventArgs e) => e.Handled = true;

    private void ProgressSlider_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        using var @lock = Sync.EnterScope();

        WaveOutDevice.Stop();
    }

    private async Task UpdateProgress()
    {
        while (true)
        {
            await Timer.WaitForNextTickAsync();

            using var @lock = Sync.EnterScope();

            if (!FinishedLoading)
                continue;

            if (Disposed == 1)
                return;

            if (WaveOutDevice.PlaybackState != PlaybackState.Playing)
                continue;

            ProgressSlider.Value = Math.Clamp(
                Stopwatch.GetElapsedTime(StartTimeStamp)
                         .TotalMilliseconds
                / MediaReader.TotalTime.TotalMilliseconds
                * 100,
                0,
                100);
        }
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!FinishedLoading)
            return;

        using var @lock = Sync.EnterScope();

        WaveOutDevice.Volume = (float)VolumeSlider.Value;
    }

    private void WaveOutDeviceOnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        using var @lock = Sync.EnterScope();

        ProgressSlider.Value = 0;
        MediaReader.Seek(0, SeekOrigin.Begin);
    }
}