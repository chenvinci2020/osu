using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Screens.Play;

namespace osu.Game.Screens.Mvis
{
    public class BackgroundStoryBoard : Container
    {
        private const float DURATION = 750;
        public Container sbContainer;
        public ClockContainer sbClock;
        private CancellationTokenSource ChangeSB;
        private ScheduledDelegate scheduledDisplaySB;
        private DimmableStoryboard dimmableStoryboard;
        private BindableBool EnableSB = new BindableBool();
        ///<summary>
        ///用于内部确定故事版是否已加载
        ///</summary>
        private BindableBool SBLoaded = new BindableBool();
        ///<summary>
        ///用于对外提供该BindableBool用于检测故事版功能是否已经准备好了
        ///</summary>
        public readonly BindableBool IsReady = new BindableBool();
        public readonly BindableBool storyboardReplacesBackground = new BindableBool();

        /// <summary>
        /// This will log which beatmap's storyboard we are loading
        /// </summary>
        private Task LogTask;

        /// <summary>
        /// This will invoke LoadSBTask and run asyncly
        /// </summary>
        private Task LoadSBAsyncTask;

        /// <summary>
        /// This will be invoked by LoadSBAsyncTask and loads the current beatmap's storyboard
        /// </summary>
        private Task LoadSBTask;

        [Resolved]
        private IBindable<WorkingBeatmap> b { get; set; }

        public BackgroundStoryBoard()
        {
            RelativeSizeAxes = Axes.Both;
            Child = sbContainer = new Container
            {
                RelativeSizeAxes = Axes.Both
            };
        }

        [BackgroundDependencyLoader]
        private void load(MfConfigManager config)
        {
            config.BindWith(MfSetting.MvisEnableStoryboard, EnableSB);
        }

        protected override void LoadComplete()
        {
            EnableSB.ValueChanged += _ => UpdateVisuals();
            dimmableStoryboard?.StoryboardReplacesBackground.BindTo(storyboardReplacesBackground);
        }

        protected override void Update()
        {
            if ( IsReady.Value )
                sbClock?.Seek(b.Value.Track.CurrentTime);
        }

        public void UpdateVisuals()
        {
            if ( EnableSB.Value )
            {
                if ( !SBLoaded.Value )
                    UpdateStoryBoardAsync();
                else
                    storyboardReplacesBackground.Value = b.Value.Storyboard.ReplacesBackground && b.Value.Storyboard.HasDrawable;;

                sbClock?.FadeIn(DURATION, Easing.OutQuint);
            }
            else
            {
                storyboardReplacesBackground.Value = false;
                sbClock?.FadeOut(DURATION, Easing.OutQuint);
            }
        }

        public bool UpdateComponent()
        {
            try
            {
                LoadSBTask = LoadComponentAsync(new ClockContainer(b.Value, 0)
                {
                    Name = "ClockContainer",
                    Alpha = 0,
                    Child = dimmableStoryboard = new DimmableStoryboard(b.Value.Storyboard)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Name = "Storyboard"
                    }
                }, newsbClock =>
                {
                    sbClock?.FadeOut(DURATION, Easing.OutQuint);
                    sbClock?.Expire();

                    sbClock = newsbClock;

                    dimmableStoryboard.IgnoreUserSettings.Value = true;

                    sbContainer.Add(sbClock);

                    if ( b.Value.Track.IsRunning == true )
                        sbClock.Start();
                    else
                        sbClock.Stop();

                    sbClock.Seek(b.Value.Track.CurrentTime);

                    SBLoaded.Value = true;
                    IsReady.Value = true;
                }, (ChangeSB = new CancellationTokenSource()).Token);
            }
            catch (Exception e)
            {
                Logger.Error(e, "加载Storyboard时出现错误! 请检查你的谱面!");
                return false;
            }

            return true;
        }

        public void CancelAllTasks()
        {
            scheduledDisplaySB?.Cancel();
            scheduledDisplaySB = null;

            ChangeSB?.Cancel();
            ChangeSB = new CancellationTokenSource();

            LoadSBTask = null;
            LoadSBAsyncTask = null;
            LogTask = null;
        }

        private void displayWhenLoaded()
        {
            try
            {
                if ( !IsReady.Value )
                {
                    scheduledDisplaySB?.Cancel();
                    scheduledDisplaySB = null;
                    return;
                }

                if ( scheduledDisplaySB != null )
                    return;

                scheduledDisplaySB = Scheduler.AddDelayed( () => UpdateVisuals() , 0);
            }
            finally
            {
                Schedule(displayWhenLoaded);
            }
        }

        public void UpdateStoryBoardAsync( float displayDelay = 0 )
        {
            if ( b == null )
                return;

            IsReady.Value = false;
            SBLoaded.Value = false;

            Schedule(() =>
            {
                CancelAllTasks();

                var lastdimmableSB = dimmableStoryboard;

                lastdimmableSB?.FadeOut(DURATION, Easing.OutQuint);
                sbClock?.FadeOut(DURATION, Easing.OutQuint);

                lastdimmableSB?.Expire();
                sbClock?.Expire();

                if ( !EnableSB.Value )
                {
                    IsReady.Value = true;
                    return;
                }

                LoadSBAsyncTask = Task.Run( async () =>
                {
                    Logger.Log($"Loading Storyboard for Beatmap \"{b.Value.BeatmapSetInfo}\"...");

                    storyboardReplacesBackground.Value = false;

                    this.Delay(displayDelay).Schedule(displayWhenLoaded);

                    LogTask = Task.Run( () => 
                    {
                        UpdateComponent();
                    });

                    await LogTask;
                });
            });
        }
    }
}