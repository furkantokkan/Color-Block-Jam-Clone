using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// The Pen's colour keys: 1-9 name colour-order positions 1-9, 0 names 10 and - names 11, on the top row or the
    /// keypad, so each key picks the colour whose Colors badge shows its number (user answer of 2026-09-26,
    /// TASK-20260926-180414067-b923c8a9).
    /// </summary>
    internal static class LevelColorKeys
    {
        // Prebuilt key labels by 0-based position, so a tooltip names a key without formatting it.
        private static readonly string[] s_keyLabels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-" };

        /// <summary>The 1-based colour-order position a key names; false for any other key.</summary>
        public static bool TryGetPosition(KeyCode key, out int position)
        {
            if (key >= KeyCode.Alpha1 && key <= KeyCode.Alpha9)
            {
                position = key - KeyCode.Alpha1 + 1;
                return true;
            }

            if (key >= KeyCode.Keypad1 && key <= KeyCode.Keypad9)
            {
                position = key - KeyCode.Keypad1 + 1;
                return true;
            }

            switch (key)
            {
                case KeyCode.Alpha0:
                case KeyCode.Keypad0:
                    position = 10;
                    return true;
                case KeyCode.Minus:
                case KeyCode.KeypadMinus:
                    position = 11;
                    return true;
                default:
                    position = 0;
                    return false;
            }
        }

        /// <summary>The key that names a 1-based colour-order position: "1" to "9", "0" for 10 and "-" for 11.</summary>
        public static string GetKeyLabel(int position)
        {
            return s_keyLabels[position - 1];
        }
    }
}
