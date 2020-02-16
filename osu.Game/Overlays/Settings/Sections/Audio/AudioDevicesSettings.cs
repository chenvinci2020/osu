﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.Overlays.Settings.Sections.Audio
{
    public class AudioDevicesSettings : SettingsSubsection
    {
        protected override string Header => "输出设备";

        [Resolved]
        private AudioManager audio { get; set; }

        private SettingsDropdown<string> dropdown;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (audio != null)
            {
                audio.OnNewDevice -= onDeviceChanged;
                audio.OnLostDevice -= onDeviceChanged;
            }
        }

        private void updateItems()
        {
            var deviceItems = new List<string> { string.Empty };
            deviceItems.AddRange(audio.AudioDeviceNames);

            var preferredDeviceName = audio.AudioDevice.Value;
            if (deviceItems.All(kv => kv != preferredDeviceName))
                deviceItems.Add(preferredDeviceName);

            // The option dropdown for audio device selection lists all audio
            // device names. Dropdowns, however, may not have multiple identical
            // keys. Thus, we remove duplicate audio device names from
            // the dropdown. BASS does not give us a simple mechanism to select
            // specific audio devices in such a case anyways. Such
            // functionality would require involved OS-specific code.
            dropdown.Items = deviceItems.Distinct().ToList();
        }

        private void onDeviceChanged(string name) => updateItems();

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Children = new Drawable[]
            {
                dropdown = new AudioDeviceSettingsDropdown
                {
                    Keywords = new[] { "扬声器", "耳机", "输出" }
                }
            };

            updateItems();

            dropdown.Bindable = audio.AudioDevice;

            audio.OnNewDevice += onDeviceChanged;
            audio.OnLostDevice += onDeviceChanged;
        }

        private class AudioDeviceSettingsDropdown : SettingsDropdown<string>
        {
            protected override OsuDropdown<string> CreateDropdown() => new AudioDeviceDropdownControl();

            private class AudioDeviceDropdownControl : DropdownControl
            {
                protected override string GenerateItemText(string item)
                    => string.IsNullOrEmpty(item) ? "默认" : base.GenerateItemText(item);
            }
        }
    }
}