using System;
using System.Collections.Generic;
using ColorBlockJam.Gameplay.UI;
using UnityEngine;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>
    /// A four-slot gameplay HUD that records the last value of every write and counts timer and freeze writes, without
    /// allocating, so allocation tests can drive presenters through it. Click and Close raise the view's events.
    /// </summary>
    public sealed class FakeGameplayHudView : IGameplayHudView
    {
        public const int k_slots = 4;

        public event Action<int> SlotPressed;

        public event Action BannerClosed;

        public int SlotCount => k_slots;

        public bool[] SlotShown { get; } = new bool[k_slots];

        public Sprite[] SlotIcons { get; } = new Sprite[k_slots];

        public string[] SlotCounts { get; } = new string[k_slots];

        public bool[] SlotEmpty { get; } = new bool[k_slots];

        public bool[] SlotSelected { get; } = new bool[k_slots];

        /// <summary>Starts true, as a Button starts enabled.</summary>
        public bool[] SlotEnabled { get; } = { true, true, true, true };

        public int[] BlockedCounts { get; } = new int[k_slots];

        public bool BannerShown { get; private set; }

        public string BannerTitle { get; private set; }

        public string BannerInstruction { get; private set; }

        public Sprite BannerIcon { get; private set; }

        public string Timer { get; private set; }

        public int TimerWrites { get; private set; }

        public bool Frozen { get; private set; }

        public string FreezeText { get; private set; }

        public float FreezeFill { get; private set; }

        public int FreezeWrites { get; private set; }

        /// <summary>The fill the last drain runs to, and over how many seconds; 0 seconds when no drain followed a write.</summary>
        public float FreezeDrainFill { get; private set; }

        public float FreezeDrainSeconds { get; private set; }

        public bool FreezeBarPaused { get; private set; }

        /// <summary>A copy of the last opening icon list (Home only, so the copy never runs in a measured tick).</summary>
        public List<Sprite> OpeningIcons { get; } = new List<Sprite>();

        public bool Opening { get; private set; }

        public void Click(int slot)
        {
            SlotPressed?.Invoke(slot);
        }

        public void Close()
        {
            BannerClosed?.Invoke();
        }

        public bool HasPressListeners => SlotPressed != null && BannerClosed != null;

        public void ShowSlot(int slot, Sprite icon)
        {
            SlotShown[slot] = true;
            SlotIcons[slot] = icon;
        }

        public void HideSlot(int slot)
        {
            SlotShown[slot] = false;
        }

        public void SetSlotStock(int slot, string count, bool empty)
        {
            SlotCounts[slot] = count;
            SlotEmpty[slot] = empty;
        }

        public void SetSlotSelected(int slot, bool selected)
        {
            SlotSelected[slot] = selected;
        }

        public void SetSlotEnabled(int slot, bool enabled)
        {
            SlotEnabled[slot] = enabled;
        }

        public void PlaySlotBlocked(int slot)
        {
            BlockedCounts[slot]++;
        }

        public void ShowBanner(string title, string instruction, Sprite icon)
        {
            BannerShown = true;
            BannerTitle = title;
            BannerInstruction = instruction;
            BannerIcon = icon;
        }

        public void HideBanner()
        {
            BannerShown = false;
        }

        public void SetTimer(string text)
        {
            Timer = text;
            TimerWrites++;
        }

        public void SetFrozen(bool frozen)
        {
            Frozen = frozen;
        }

        public void SetFreezeCounter(string text, float fill)
        {
            FreezeText = text;
            FreezeFill = fill;
            FreezeWrites++;
            FreezeDrainSeconds = 0f;
        }

        public void DrainFreezeBar(float fill, float seconds)
        {
            FreezeDrainFill = fill;
            FreezeDrainSeconds = seconds;
        }

        public void SetFreezeBarPaused(bool paused)
        {
            FreezeBarPaused = paused;
        }

        public void SetOpeningIcons(IReadOnlyList<Sprite> icons)
        {
            OpeningIcons.Clear();
            for (int index = 0; index < icons.Count; index++)
            {
                OpeningIcons.Add(icons[index]);
            }
        }

        public void SetOpening(bool opening)
        {
            Opening = opening;
        }
    }
}
