using System;
using UnityEngine.Profiling;

namespace ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures
{
    /// <summary>
    /// Counts the GC.Alloc samples an action makes on this thread, with the same Recorder that
    /// Is.Not.AllocatingGCMemory uses, for frames whose only expected allocations are named and counted: UI
    /// Toolkit 2022.3 copies each non-empty string a Label is given (one sample per write), so a counter label
    /// that changes its numeral costs one sample even from a prebuilt string. Test code only.
    /// </summary>
    public static class GcAllocations
    {
        public static int Count(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            Recorder recorder = Recorder.Get("GC.Alloc");
            recorder.enabled = false;
            recorder.FilterToCurrentThread();
            recorder.enabled = true;
            try
            {
                action();
            }
            finally
            {
                recorder.enabled = false;
                recorder.CollectFromAllThreads();
            }

            return recorder.sampleBlockCount;
        }
    }
}
