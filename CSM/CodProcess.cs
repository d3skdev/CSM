using System.Diagnostics;
using System.Windows.Threading;

namespace CSM
{
    class CodProcess
    {
        public event EventHandler<int>? ProcessExistEvent;
        private DispatcherTimer processCheckTimer;

        public CodProcess(int checkEverySeconds = 5)
        {
            processCheckTimer = new DispatcherTimer();
            processCheckTimer.Interval = TimeSpan.FromSeconds(checkEverySeconds);
            processCheckTimer.Tick += ProcessCheckTimer_Tick;
            processCheckTimer.Start();
        }

        private void ProcessCheckTimer_Tick(object? sender, EventArgs e)
        {
            var processes = Process.GetProcesses().Where(p => p.ProcessName == "cod");
            if (processes.Any()) ProcessExistEvent?.Invoke(this, processes.First().Id);
            else ProcessExistEvent?.Invoke(this, -1);
        }

        public void Start()
        {
            processCheckTimer.Start();
            ProcessCheckTimer_Tick(null, null);
        }

        public void Stop()
        {
            processCheckTimer.Stop();
        }

    }
}
