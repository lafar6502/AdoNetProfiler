using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace AdoNetProfiler
{
    public class CmdWatcher
    {
        private static long _id = 0;
        private static Timer _tm = null;
        private class ENtry
        {
            public long StartTix = 0;
            public string Query;
        }

        public static void Initialize()
        {
            if (_tm != null) _tm.Dispose();
            _tm = new Timer(new TimerCallback(tick), null, 5000, 33 * 1000);
            
        }

        private static ConcurrentDictionary<long, ENtry> _d = new ConcurrentDictionary<long, ENtry>();

        private static void tick(object st)
        {
            var ts = DateTime.Now.Ticks - TimeSpan.FromSeconds(33).Ticks;

            var kz = _d.Where(x => x.Value.StartTix < ts);
            foreach(var ent in kz)
            {
                
            }
        }

        public static void WatchTime(string info, Action callback)
        {
            
            var mid = Interlocked.Increment(ref _id);
            _d.TryAdd(mid, new ENtry
            {
                Query = info,
                StartTix = DateTime.Now.Ticks
            });
            try
            {
                callback();
            }
            finally
            {
                ENtry en;
                _d.TryRemove(mid, out en);
            }
        }
    }
}
