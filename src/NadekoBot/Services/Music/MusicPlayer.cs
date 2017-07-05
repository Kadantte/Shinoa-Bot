﻿using Discord;
using Discord.Audio;
using System;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using System.Linq;
using System.Collections.Concurrent;
using NadekoBot.Extensions;
using System.Diagnostics;

namespace NadekoBot.Services.Music
{
    public enum StreamState
    {
        Resolving,
        Queued,
        Playing,
        Completed
    }
    public class MusicPlayer
    {
        private readonly Task _player;
        public IVoiceChannel VoiceChannel { get; private set; }
        private readonly Logger _log;

        private MusicQueue Queue { get; } = new MusicQueue();

        public bool Exited { get; set; } = false;
        public bool Stopped { get; private set; } = false;
        public float Volume { get; private set; } = 1.0f;
        public bool Paused => pauseTaskSource != null;
        private TaskCompletionSource<bool> pauseTaskSource { get; set; } = null;

        public string PrettyVolume => $"🔉 {(int)(Volume * 100)}%";
        public string PrettyCurrentTime
        {
            get
            {
                var time = CurrentTime.ToString(@"mm\:ss");
                var hrs = (int)CurrentTime.TotalHours;

                if (hrs > 0)
                    return hrs + ":" + time;
                else
                    return time;
            }
        }
        public string PrettyFullTime => PrettyCurrentTime + " / " + (Queue.Current.Song?.PrettyTotalTime ?? "?");
        private CancellationTokenSource SongCancelSource { get; set; }
        public ITextChannel OutputTextChannel { get; set; }
        public (int Index, SongInfo Current) Current
        {
            get
            {
                if (Stopped)
                    return (0, null);
                return Queue.Current;
            }
        }

        public bool RepeatCurrentSong { get; private set; }
        public bool Shuffle { get; private set; }
        public bool Autoplay { get; private set; }
        public bool RepeatPlaylist { get; private set; } = true;
        public uint MaxQueueSize
        {
            get => Queue.MaxQueueSize;
            set { lock (locker) Queue.MaxQueueSize = value; }
        }
        private bool _fairPlay;
        public bool FairPlay
        {
            get => _fairPlay;
            set
            {
                if (value)
                {
                    var cur = Queue.Current;
                    if (cur.Song != null)
                        RecentlyPlayedUsers.Add(cur.Song.QueuerName);
                }
                else
                {
                    RecentlyPlayedUsers.Clear();
                }

                _fairPlay = value;
            }
        }
        public uint MaxPlaytimeSeconds { get; set; }


        const int _frameBytes = 3840;
        const float _miliseconds = 20.0f;
        public TimeSpan CurrentTime => TimeSpan.FromSeconds(_bytesSent / (float)_frameBytes / (1000 / _miliseconds));

        private int _bytesSent = 0;

        private IAudioClient _audioClient;
        private readonly object locker = new object();
        private MusicService _musicService;

        #region events
        public event Action<MusicPlayer, (int Index, SongInfo Song)> OnStarted;
        public event Action<MusicPlayer, SongInfo> OnCompleted;
        public event Action<MusicPlayer, bool> OnPauseChanged;
        #endregion


        private bool manualSkip = false;
        private bool manualIndex = false;
        private bool newVoiceChannel = false;
        private readonly IGoogleApiService _google;

        private ConcurrentHashSet<string> RecentlyPlayedUsers { get; } = new ConcurrentHashSet<string>();
        public TimeSpan TotalPlaytime
        {
            get
            {
                var songs = Queue.ToArray().Songs;
                return songs.Any(s => s.TotalTime == TimeSpan.MaxValue)
                    ? TimeSpan.MaxValue
                    : new TimeSpan(songs.Sum(s => s.TotalTime.Ticks));
            }
        }
            

        public MusicPlayer(MusicService musicService, IGoogleApiService google, IVoiceChannel vch, ITextChannel output, float volume)
        {
            _log = LogManager.GetCurrentClassLogger();
            this.Volume = volume;
            this.VoiceChannel = vch;
            this.SongCancelSource = new CancellationTokenSource();
            this.OutputTextChannel = output;
            this._musicService = musicService;
            this._google = google;

            _player = Task.Run(async () =>
             {
                 while (!Exited)
                 {
                     _bytesSent = 0;
                     CancellationToken cancelToken;
                     (int Index, SongInfo Song) data;
                     lock (locker)
                     {
                         data = Queue.Current;
                         cancelToken = SongCancelSource.Token;
                         manualSkip = false;
                         manualIndex = false;
                     }
                     if (data.Song == null)
                         continue;

                     _log.Info("Starting");
                     using (var b = new SongBuffer(data.Song.Uri, ""))
                     {
                         AudioOutStream pcm = null;
                         try
                         {
                             var bufferTask = b.StartBuffering(cancelToken);
                             var timeout = Task.Delay(10000);
                             if (Task.WhenAny(bufferTask, timeout) == timeout)
                             {
                                 _log.Info("Buffering failed due to a timeout.");
                                 continue;
                             }
                             else if (!bufferTask.Result)
                             {
                                 _log.Info("Buffering failed due to a cancel or error.");
                                 continue;
                             }

                             var ac = await GetAudioClient();
                             if (ac == null)
                             {
                                 await Task.Delay(900, cancelToken);
                                 // just wait some time, maybe bot doesn't even have perms to join that voice channel, 
                                 // i don't want to spam connection attempts
                                 continue;
                             }
                             pcm = ac.CreatePCMStream(AudioApplication.Music, bufferMillis: 500);
                             OnStarted?.Invoke(this, data);

                             byte[] buffer = new byte[3840];
                             int bytesRead = 0;

                             while ((bytesRead = b.Read(buffer, 0, buffer.Length)) > 0
                                && (MaxPlaytimeSeconds <= 0 || MaxPlaytimeSeconds >= CurrentTime.TotalSeconds))
                             {
                                 //AdjustVolume(buffer, Volume);
                                 await pcm.WriteAsync(buffer, 0, bytesRead, cancelToken).ConfigureAwait(false);
                                 unchecked { _bytesSent += bytesRead; }

                                 await (pauseTaskSource?.Task ?? Task.CompletedTask);
                             }
                         }
                         catch (OperationCanceledException)
                         {
                             _log.Info("Song Canceled");
                         }
                         catch (Exception ex)
                         {
                             _log.Warn(ex);
                         }
                         finally
                         {
                             if (pcm != null)
                             {
                                 // flush is known to get stuck from time to time, 
                                 // just skip flushing if it takes more than 1 second
                                 var flushCancel = new CancellationTokenSource();
                                 var flushToken = flushCancel.Token;
                                 var flushDelay = Task.Delay(1000, flushToken);
                                 await Task.WhenAny(flushDelay, pcm.FlushAsync(flushToken));
                                 flushCancel.Cancel();
                             }

                             OnCompleted?.Invoke(this, data.Song);
                         }
                     }
                     try
                     {
                         //if repeating current song, just ignore other settings, 
                         // and play this song again (don't change the index)
                         // ignore rcs if song is manually skipped

                         int queueCount;
                         lock (locker)
                             queueCount = Queue.Count;

                         if (!manualIndex && (!RepeatCurrentSong || manualSkip))
                         {
                             if (Shuffle)
                             {
                                 _log.Info("Random song");
                                 Queue.Random(); //if shuffle is set, set current song index to a random number
                             }
                             else
                             {
                                 //if last song, and autoplay is enabled, and if it's a youtube song
                                 // do autplay magix
                                 if (queueCount - 1 == data.Index && Autoplay && data.Song?.ProviderType == Database.Models.MusicType.YouTube)
                                 {
                                     try
                                     {
                                         _log.Info("Loading related song");
                                         await _musicService.TryQueueRelatedSongAsync(data.Song.Query, OutputTextChannel, VoiceChannel);
                                         Queue.Next();
                                     }
                                     catch
                                     {
                                         _log.Info("Loading related song failed.");
                                     }
                                 }
                                 else if (FairPlay)
                                 {
                                     lock (locker)
                                     {
                                         _log.Info("Next fair song");
                                         var q = Queue.ToArray().Songs.Shuffle().ToArray();

                                         bool found = false;
                                         for (var i = 0; i < q.Length; i++) //first try to find a queuer who didn't have their song played recently
                                         {
                                             var item = q[i];
                                             if (RecentlyPlayedUsers.Add(item.QueuerName)) // if it's found, set current song to that index
                                             {
                                                 Queue.CurrentIndex = i;
                                                 found = true;
                                                 break;
                                             }
                                         }
                                         if (!found) //if it's not
                                         {
                                             RecentlyPlayedUsers.Clear(); //clear all recently played users (that means everyone from the playlist has had their song played)
                                             Queue.Random(); //go to a random song (to prevent looping on the first few songs)
                                             var cur = Current;
                                             if (cur.Current != null) // add newely scheduled song's queuer to the recently played list
                                                 RecentlyPlayedUsers.Add(cur.Current.QueuerName);
                                         }
                                     }
                                 }
                                 else if (queueCount - 1 == data.Index && !RepeatPlaylist && !manualSkip)
                                 {
                                     _log.Info("Stopping because repeatplaylist is disabled");
                                     lock (locker)
                                     {
                                         Stop();
                                     }
                                 }
                                 else
                                 {
                                     _log.Info("Next song");
                                     lock (locker)
                                     {
                                         Queue.Next();
                                     }
                                 }
                             }
                         }
                     }
                     catch (Exception ex)
                     {
                         _log.Error(ex);
                     }
                     do
                     {
                         await Task.Delay(500);
                     }
                     while ((Queue.Count == 0 || Stopped) && !Exited);
                 }
             }, SongCancelSource.Token);
        }

        public void SetIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            lock (locker)
            {
                Queue.CurrentIndex = index;
                manualIndex = true;
                CancelCurrentSong();
            }
        }

        private async Task<IAudioClient> GetAudioClient(bool reconnect = false)
        {
            if (_audioClient == null ||
                _audioClient.ConnectionState != ConnectionState.Connected ||
                reconnect ||
                newVoiceChannel)
                try
                {
                    try
                    {
                        var t = _audioClient?.StopAsync();
                        if (t != null)
                        {
                            await t;
                            _audioClient.Dispose();
                        }
                    }
                    catch
                    {
                    }
                    newVoiceChannel = false;
                    var curUser = await VoiceChannel.Guild.GetCurrentUserAsync();
                    if (curUser.VoiceChannel != null)
                    {
                        var ac = await VoiceChannel.ConnectAsync();
                        await ac.StopAsync();
                        await Task.Delay(1000);
                    }
                    _audioClient = await VoiceChannel.ConnectAsync();
                }
                catch
                {
                    return null;
                }
            return _audioClient;
        }

        public int Enqueue(SongInfo song)
        {
            lock (locker)
            {
                if (Exited)
                    return -1;
                Queue.Add(song);
                return Queue.Count;
            }
        }

        public void Next(int skipCount = 1)
        {
            lock (locker)
            {
                if (Exited)
                    return;
                manualSkip = true;
                // if player is stopped, and user uses .n, it should play current song.  
                // It's a bit weird, but that's the least annoying solution
                if (!Stopped)
                    Queue.Next(skipCount - 1);
                Stopped = false;
                Unpause();
                CancelCurrentSong();
            }
        }

        public void Stop(bool clearQueue = false)
        {
            lock (locker)
            {
                Stopped = true;
                Queue.ResetCurrent();
                if (clearQueue)
                    Queue.Clear();
                Unpause();
                CancelCurrentSong();
            }
        }

        private void Unpause()
        {
            lock (locker)
            {
                if (pauseTaskSource != null)
                {
                    pauseTaskSource.TrySetResult(true);
                    pauseTaskSource = null;
                }
            }
        }

        public void TogglePause()
        {
            lock (locker)
            {
                if (pauseTaskSource == null)
                    pauseTaskSource = new TaskCompletionSource<bool>();
                else
                {
                    Unpause();
                }
            }
            OnPauseChanged?.Invoke(this, pauseTaskSource != null);
        }

        public void SetVolume(int volume)
        {
            if (volume < 0 || volume > 100)
                throw new ArgumentOutOfRangeException(nameof(volume));
            lock (locker)
            {
                Volume = ((float)volume) / 100;
            }
        }

        public SongInfo RemoveAt(int index)
        {
            lock (locker)
            {
                var cur = Queue.Current;
                if (cur.Index == index)
                    Next();
                return Queue.RemoveAt(index);
            }
        }

        private void CancelCurrentSong()
        {
            lock (locker)
            {
                var cs = SongCancelSource;
                SongCancelSource = new CancellationTokenSource();
                cs.Cancel();
            }
        }

        public void ClearQueue()
        {
            lock (locker)
            {
                Queue.Clear();
            }
        }

        public (int CurrentIndex, SongInfo[] Songs) QueueArray()
        {
            lock (locker)
                return Queue.ToArray();
        }

        //aidiakapi ftw
        public static unsafe byte[] AdjustVolume(byte[] audioSamples, float volume)
        {
            if (Math.Abs(volume - 1f) < 0.0001f) return audioSamples;

            // 16-bit precision for the multiplication
            var volumeFixed = (int)Math.Round(volume * 65536d);

            var count = audioSamples.Length / 2;

            fixed (byte* srcBytes = audioSamples)
            {
                var src = (short*)srcBytes;

                for (var i = count; i != 0; i--, src++)
                    *src = (short)(((*src) * volumeFixed) >> 16);
            }

            return audioSamples;
        }

        public bool ToggleRepeatSong()
        {
            lock (locker)
            {
                return RepeatCurrentSong = !RepeatCurrentSong;
            }
        }

        public async Task Destroy()
        {
            _log.Info("Destroying");
            lock (locker)
            {
                Stop();
                Exited = true;
                Unpause();

                OnCompleted = null;
                OnPauseChanged = null;
                OnStarted = null;
            }
            var ac = _audioClient;
            if (ac != null)
                await ac.StopAsync();
        }

        public bool ToggleShuffle()
        {
            lock (locker)
            {
                return Shuffle = !Shuffle;
            }
        }

        public bool ToggleAutoplay()
        {
            lock (locker)
            {
                return Autoplay = !Autoplay;
            }
        }

        public bool ToggleRepeatPlaylist()
        {
            lock (locker)
            {
                return RepeatPlaylist = !RepeatPlaylist;
            }
        }

        public async Task SetVoiceChannel(IVoiceChannel vch)
        {
            lock (locker)
            {
                if (Exited)
                    return;
                VoiceChannel = vch;
            }
            _audioClient = await vch.ConnectAsync();
        }

        public async Task UpdateSongDurationsAsync()
        {
            var sw = Stopwatch.StartNew();
            var (_, songs) = Queue.ToArray();
            var toUpdate = songs
                .Where(x => x.ProviderType == Database.Models.MusicType.YouTube
                    && x.TotalTime == TimeSpan.Zero);

            var vIds = toUpdate.Select(x => x.VideoId);

            sw.Stop();
            _log.Info(sw.Elapsed.TotalSeconds);
            if (!vIds.Any())
                return;

            var durations = await _google.GetVideoDurationsAsync(vIds);

            foreach (var x in toUpdate)
            {
                if (durations.TryGetValue(x.VideoId, out var dur))
                    x.TotalTime = dur;
            }
        }

        public SongInfo MoveSong(int n1, int n2)
            => Queue.MoveSong(n1, n2);

        //// this should be written better
        //public TimeSpan TotalPlaytime => 
        //    _playlist.Any(s => s.TotalTime == TimeSpan.MaxValue) ? 
        //    TimeSpan.MaxValue : 
        //    new TimeSpan(_playlist.Sum(s => s.TotalTime.Ticks));        
    }
}