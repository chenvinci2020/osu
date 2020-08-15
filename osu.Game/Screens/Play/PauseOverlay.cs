﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Screens.Play
{
    public class PauseOverlay : GameplayMenuOverlay
    {
        public Action OnResume;

        public override string Header => "游戏暂停";
        public override string Description => "要去做什么呢owo?";
        public override bool IsPresent => base.IsPresent || pauseLoop.IsPlaying;

        private SkinnableSound pauseLoop;

        protected override Action BackAction => () => InternalButtons.Children.First().Click();

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            AddButton("继续", colours.Green, () => OnResume?.Invoke());
            AddButton("重试", colours.YellowDark, () => OnRetry?.Invoke());
            AddButton("退出", new Color4(170, 27, 39, 255), () => OnQuit?.Invoke());

            AddInternal(pauseLoop = new SkinnableSound(new SampleInfo("pause-loop"))
            {
                Looping = true,
                Volume = { Value = 0 }
            });
        }

        protected override void PopIn()
        {
            base.PopIn();

            pauseLoop.VolumeTo(1.0f, TRANSITION_DURATION, Easing.InQuint);
            pauseLoop.Play();
        }

        protected override void PopOut()
        {
            base.PopOut();

            pauseLoop.VolumeTo(0, TRANSITION_DURATION, Easing.OutQuad).Finally(_ => pauseLoop.Stop());
        }
    }
}
