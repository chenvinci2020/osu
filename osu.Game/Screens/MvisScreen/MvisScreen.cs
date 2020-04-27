// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.Mvis.UI;
using osu.Game.Screens.Backgrounds;
using osu.Game.Screens.Mvis.UI.Objects;
using osu.Game.Screens.Mvis.Buttons;
using osu.Game.Screens.Mvis.Objects.Helpers;
using osuTK;
using osuTK.Graphics;
using osu.Game.Overlays;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Threading;
using osu.Framework.Bindables;
using osu.Framework.Input.Events;
using osu.Game.Overlays.Music;
using osu.Framework.Audio.Track;
using osu.Game.Input.Bindings;
using osu.Framework.Input.Bindings;
using osu.Game.Configuration;

namespace osu.Game.Screens
{
    /// <summary>
    /// 缝合怪 + 奥利给山警告
    /// </summary>
    public class MvisScreen : OsuScreen, IKeyBindingHandler<GlobalAction>
    {
        private const float DURATION = 750;
        private static readonly Vector2 BOTTOMPANEL_SIZE = new Vector2(TwoLayerButton.SIZE_EXTENDED.X, 50);

        private bool AllowCursor = false;
        private bool AllowBack = false;
        public override bool AllowBackButton => AllowBack;
        public override bool CursorVisible => AllowCursor;
        protected override BackgroundScreen CreateBackground() => new BackgroundScreenBeatmap(Beatmap.Value);

        private bool canReallyHide =>
            // don't hide if the user is hovering one of the panes, unless they are idle.
            (IsHovered || idleTracker.IsIdle.Value)
            // don't hide if a focused overlay is visible, like settings.
            && inputManager?.FocusedDrawable == null;

        [Resolved(CanBeNull = true)]
        private OsuGame game { get; set; }
        [Resolved]
        private MusicController musicController { get; set; }
        [Resolved]
        private BeatmapManager beatmaps { get; set; }

        [Cached]
        private PlaylistOverlay playlist;

        private InputManager inputManager { get; set; }
        private MouseIdleTracker idleTracker;
        private ScheduledDelegate scheduledHideOverlays;
        private ScheduledDelegate scheduledShowOverlays;
        private Box bgBox;
        private BottomBar bottomBar;
        private Container buttons;
        private BeatmapLogo beatmapLogo;
        private HoverCheckContainer hoverCheckContainer;
        private HoverableProgressBarContainer progressBarContainer;
        private ToggleableButton loopToggleButton;
        private ToggleableOverlayLockButton lockButton;
        private Track track;
        private Bindable<float> BgBlur = new Bindable<float>();
        private bool OverlaysHidden = false;

        public MvisScreen()
        {
            InternalChildren = new Drawable[]
            {
                bgBox = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0.3f
                },
                new SpaceParticlesContainer(),
                new ParallaxContainer
                {
                    ParallaxAmount = -0.0025f,
                    Child = beatmapLogo = new BeatmapLogo
                    {
                        Anchor = Anchor.Centre,
                    }
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 400,
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Children = new Drawable[]
                    {
                        playlist = new PlaylistOverlay
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.X,
                        },
                    }
                },
                new FillFlowContainer
                {
                    Name = "Bottom FillFlow",
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(5),
                    Children = new Drawable[]
                    {
                        bottomBar = new BottomBar
                        {
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4Extensions.FromHex("#333")
                                },
                                new Container
                                {
                                    Name = "Base Container",
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    RelativeSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        progressBarContainer = new HoverableProgressBarContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                        },
                                        buttons = new Container
                                        {
                                            Name = "Buttons Container",
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            RelativeSizeAxes = Axes.Both,
                                            Children = new Drawable[]
                                            {
                                                new FillFlowContainer
                                                {
                                                    Name = "Left Buttons FillFlow",
                                                    Anchor = Anchor.CentreLeft,
                                                    Origin = Anchor.CentreLeft,
                                                    AutoSizeAxes = Axes.Both,
                                                    Spacing = new Vector2(5),
                                                    Margin = new MarginPadding { Left = 5 },
                                                    Children = new Drawable[]
                                                    {
                                                        new BottomBarButton()
                                                        {
                                                            ButtonIcon = FontAwesome.Solid.ArrowLeft,
                                                            Action = () => this.Exit(),
                                                            TooltipText = "退出",
                                                        },
                                                    }
                                                },
                                                new FillFlowContainer
                                                {
                                                    Name = "Centre Button FillFlow",
                                                    Anchor = Anchor.Centre,
                                                    Origin = Anchor.Centre,
                                                    AutoSizeAxes = Axes.Both,
                                                    Spacing = new Vector2(5),
                                                    Children = new Drawable[]
                                                    {
                                                        new MusicControlButton()
                                                        {
                                                            ButtonIcon = FontAwesome.Solid.StepBackward,
                                                            Action = () => musicController?.PreviousTrack(),
                                                            TooltipText = "上一首/从头开始",
                                                        },
                                                        new MusicControlButton()
                                                        {
                                                            ButtonIcon = FontAwesome.Solid.Music,
                                                            Action = () => musicController?.TogglePause(),
                                                            TooltipText = "切换暂停",
                                                        },
                                                        new MusicControlButton()
                                                        {
                                                            ButtonIcon = FontAwesome.Solid.StepForward,
                                                            Action = () => musicController?.NextTrack(),
                                                            TooltipText = "下一首",
                                                        },
                                                    }
                                                },
                                                new FillFlowContainer
                                                {
                                                    Name = "Right Buttons FillFlow",
                                                    Anchor = Anchor.CentreRight,
                                                    Origin = Anchor.CentreRight,
                                                    AutoSizeAxes = Axes.Both,
                                                    Spacing = new Vector2(5),
                                                    Margin = new MarginPadding { Right = 5 },
                                                    Children = new Drawable[]
                                                    {
                                                        loopToggleButton = new ToggleableButton()
                                                        {
                                                            ButtonIcon = FontAwesome.Solid.Undo,
                                                            TooltipText = "单曲循环",
                                                        },
                                                        new BottomBarButton()
                                                        {
                                                            ButtonIcon = FontAwesome.Solid.User,
                                                            Action = () => InvokeSolo(),
                                                            TooltipText = "在选歌界面中查看",
                                                        },
                                                        new BottomBarButton()
                                                        {
                                                            ButtonIcon = FontAwesome.Solid.Atom,
                                                            Action = () => playlist.ToggleVisibility(),
                                                            TooltipText = "侧边栏",
                                                        },
                                                    }
                                                },
                                            }
                                        },
                                    }
                                },
                            }
                        },
                        lockButton = new ToggleableOverlayLockButton
                        {
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.BottomCentre,
                        }
                    }
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Child = hoverCheckContainer = new HoverCheckContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    }
                },
                idleTracker = new MouseIdleTracker(2000),
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.MvisBgBlur, BgBlur);

            BgBlur.ValueChanged += _ => UpdateBgBlur();
        }

        protected override void LoadComplete()
        {
            Beatmap.ValueChanged += _ => updateComponentFromBeatmap(Beatmap.Value);
            idleTracker.IsIdle.ValueChanged += _ => UpdateVisuals();
            hoverCheckContainer.ScreenHovered.ValueChanged += _ => UpdateVisuals();
            lockButton.ToggleableValue.ValueChanged += _ => UpdateLockButton();
            loopToggleButton.ToggleableValue.ValueChanged += _ => Beatmap.Value.Track.Looping = loopToggleButton.ToggleableValue.Value;

            inputManager = GetContainingInputManager();
            bgBox.ScaleTo(1.1f);

            playlist.BeatmapSets.BindTo(musicController.BeatmapSets);

            ShowOverlays();

            base.LoadComplete();
        }

        protected override void Update()
        {
            base.Update();

            if (track?.IsDummyDevice == false)
            {
                progressBarContainer.progressBar.EndTime = track.Length;
                progressBarContainer.progressBar.CurrentTime = track.CurrentTime;
            }
            else
            {
                progressBarContainer.progressBar.CurrentTime = 0;
                progressBarContainer.progressBar.EndTime = 1;
            }
        }

        public override void OnEntering(IScreen last)
        {
            base.OnEntering(last);

            updateComponentFromBeatmap(Beatmap.Value);
        }

        public override bool OnExiting(IScreen next)
        {
            track = new TrackVirtual(Beatmap.Value.Track.Length);
            beatmapLogo.Exit();

            this.FadeOut(500, Easing.OutQuint);
            return base.OnExiting(next);
        }

        public bool OnPressed(GlobalAction action)
        {
            switch (action)
            {
                case GlobalAction.MvisMusicPrev:
                    musicController.PreviousTrack();
                    return true;

                case GlobalAction.MvisMusicNext:
                    musicController.NextTrack();
                    return true;

                case GlobalAction.MvisTogglePause:
                    musicController.TogglePause();
                    return true;

                case GlobalAction.MvisTogglePlayList:
                    playlist.ToggleVisibility();
                    return true;

                case GlobalAction.MvisOpenInSongSelect:
                    InvokeSolo();
                    return true;

                case GlobalAction.MvisToggleOverlayLock:
                    lockButton.Toggle();
                    return true;

                case GlobalAction.MvisToggleTrackLoop:
                    loopToggleButton.Toggle();
                    return true;
            }

            return false;
        }

        public void OnReleased(GlobalAction action)
        {
        }

        private void InvokeSolo()
        {
            game?.PresentBeatmap(Beatmap.Value.BeatmapSetInfo);
        }

        private void UpdateVisuals()
        {
            var mouseIdle = idleTracker.IsIdle.Value;

            //如果有其他弹窗显示在播放器上方，解锁切换并显示界面
            if ( !hoverCheckContainer.ScreenHovered.Value )
            {
                if ( lockButton.ToggleableValue.Value && OverlaysHidden )
                    lockButton.Toggle();

                ShowOverlays();
                return;
            }

            switch (mouseIdle)
            {
                case true:
                    TryHideOverlays();
                    break;
            }
        }

        protected override bool Handle(UIEvent e)
        {
            switch (e)
            {
                case MouseMoveEvent _:
                    TryShowOverlays();
                    return base.Handle(e);

                default:
                    return base.Handle(e);
            }
        }

        private void UpdateLockButton()
        {
            lockButton.FadeIn(500, Easing.OutQuint).Then().Delay(2000).FadeOut(500, Easing.OutQuint);
            UpdateVisuals();
        }

        private void HideOverlays()
        {
            game?.Toolbar.Hide();
            bgBox.FadeTo(0.3f, DURATION, Easing.OutQuint);
            buttons.MoveToY(20, DURATION, Easing.OutQuint);
            bottomBar.ResizeHeightTo(0, DURATION, Easing.OutQuint)
                     .FadeTo(0.01f, DURATION, Easing.OutQuint);
            AllowBack = false;
            AllowCursor = false;
            OverlaysHidden = true;
        }

        private void ShowOverlays(bool Locked = false)
        {
            game?.Toolbar.Show();
            bgBox.FadeTo(0.6f, DURATION, Easing.OutQuint);
            buttons.MoveToY(0, DURATION, Easing.OutQuint);
            bottomBar.ResizeHeightTo(BOTTOMPANEL_SIZE.Y, DURATION, Easing.OutQuint)
                     .FadeIn(DURATION, Easing.OutQuint);
            AllowCursor = true;
            AllowBack = true;
            OverlaysHidden = false;
        }

        /// <summary>
        /// 因为未知原因, <see cref="TryHideOverlays"/>调用的<see cref="HideOverlays"/>无法被<see cref="ShowOverlays"/>中断
        /// 因此将相关功能独立出来作为单独的函数用来调用
        /// </summary>
        private void RunHideOverlays()
        {
            if ( !idleTracker.IsIdle.Value || !hoverCheckContainer.ScreenHovered.Value
                 || bottomBar.bar_IsHovered.Value || lockButton.ToggleableValue.Value )
                return;

            HideOverlays();
        }

        private void RunShowOverlays()
        {
            if ( lockButton.ToggleableValue.Value && bottomBar.Alpha == 0.01f )
            {
                lockButton.FadeIn(500, Easing.OutQuint).Then().Delay(2500).FadeOut(500, Easing.OutQuint);
                return;
            }
            ShowOverlays();
        }

        private void TryHideOverlays()
        {
            if ( !canReallyHide || bottomBar.bar_IsHovered.Value)
                return;

            try
            {
                scheduledHideOverlays = Scheduler.AddDelayed(() =>
                {
                    RunHideOverlays();
                }, 1000);
            }
            finally
            {
            }
        }

        private void TryShowOverlays()
        {
            try
            {
                scheduledShowOverlays = Scheduler.AddDelayed(() => 
                {
                    RunShowOverlays();
                }, 0);
            }
            finally
            {
            }
        }

        private void UpdateBgBlur()
        {
            if (Background is BackgroundScreenBeatmap backgroundBeatmap)
            {
                backgroundBeatmap.BlurAmount.Value =  BgBlur.Value * 100;
            }
        }

        private void updateComponentFromBeatmap(WorkingBeatmap beatmap)
        {
            track = Beatmap.Value?.TrackLoaded ?? false ? Beatmap.Value.Track : new TrackVirtual(Beatmap.Value.Track.Length);
            track.RestartPoint = 0;

            Beatmap.Value.Track.Looping = loopToggleButton.ToggleableValue.Value;

            if (Background is BackgroundScreenBeatmap backgroundBeatmap)
            {
                backgroundBeatmap.Beatmap = beatmap;
                backgroundBeatmap.BlurAmount.Value =  BgBlur.Value * 100;
            }
        }

        private class HoverCheckContainer : Container
        {
            public readonly Bindable<bool> ScreenHovered = new Bindable<bool>();

            protected override bool OnHover(Framework.Input.Events.HoverEvent e)
            {
                this.ScreenHovered.Value = true;
                return base.OnHover(e);
            }

            protected override void OnHoverLost(Framework.Input.Events.HoverLostEvent e)
            {
                this.ScreenHovered.Value = false;
                base.OnHoverLost(e);
            }
        }
    }
}