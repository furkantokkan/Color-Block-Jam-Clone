using System;

namespace ColorBlockJam.Player
{
    /// <summary>
    /// Immutable values of the three Settings toggles; every toggle defaults to on. The Vibration toggle gates
    /// haptics (DEC-156); Music and Sound only store and show state (NG-3).
    /// </summary>
    public readonly struct PlayerSettings : IEquatable<PlayerSettings>
    {
        public PlayerSettings(bool music, bool sound, bool vibration)
        {
            Music = music;
            Sound = sound;
            Vibration = vibration;
        }

        public static PlayerSettings Default => new PlayerSettings(true, true, true);

        public bool Music { get; }
        public bool Sound { get; }
        public bool Vibration { get; }

        public bool Get(PlayerSettingId id)
        {
            switch (id)
            {
                case PlayerSettingId.Music:
                    return Music;
                case PlayerSettingId.Sound:
                    return Sound;
                case PlayerSettingId.Vibration:
                    return Vibration;
                default:
                    throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown player setting.");
            }
        }

        public PlayerSettings With(PlayerSettingId id, bool value)
        {
            switch (id)
            {
                case PlayerSettingId.Music:
                    return new PlayerSettings(value, Sound, Vibration);
                case PlayerSettingId.Sound:
                    return new PlayerSettings(Music, value, Vibration);
                case PlayerSettingId.Vibration:
                    return new PlayerSettings(Music, Sound, value);
                default:
                    throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown player setting.");
            }
        }

        public bool Equals(PlayerSettings other)
        {
            return Music == other.Music && Sound == other.Sound && Vibration == other.Vibration;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Music ? 1 : 0) | (Sound ? 2 : 0) | (Vibration ? 4 : 0);
        }
    }
}
