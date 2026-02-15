namespace KickAutominer.Animations
{
    public class DotAnimation : IDisposable
    {
        private Timer? _timer;
        private int _dotCount;
        private Action<int>? _updateAction;
        private bool _isDisposed;

        public DotAnimation(int interval = 400)
        {
            if (interval <= 0)
                throw new ArgumentOutOfRangeException(nameof(interval));

            _timer = new Timer(AnimationCallback, null, interval, interval);
        }

        public void Start(Action<int> updateAction)
        {
            if (updateAction == null)
                throw new ArgumentNullException(nameof(updateAction));
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(DotAnimation));

            _updateAction = updateAction;
            _dotCount = 0;
        }

        public void Stop()
        {
            _updateAction = null;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void AnimationCallback(object? state)
        {
            if (_updateAction == null)
                return;

            _dotCount++;
            if (_dotCount > 3)
                _dotCount = 0;

            _updateAction.Invoke(_dotCount);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _timer?.Dispose();
            _timer = null;
            _isDisposed = true;
        }
    }
}