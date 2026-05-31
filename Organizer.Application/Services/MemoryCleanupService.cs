using System;
using System.Runtime;
using System.Threading.Tasks;

namespace Organizer.Application.Services;

public static class MemoryCleanupService
{
    private static readonly object SyncRoot = new();
    private static bool _isMemoryCompactionQueued;

    public static void QueueLargeImageMemoryCompaction()
    {
        lock (SyncRoot)
        {
            if (_isMemoryCompactionQueued)
                return;

            _isMemoryCompactionQueued = true;
        }

        _ = Task.Run(() =>
        {
            try
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            }
            finally
            {
                lock (SyncRoot)
                    _isMemoryCompactionQueued = false;
            }
        });
    }
}
