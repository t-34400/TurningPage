#nullable enable

using System;

namespace TurningPage.Sample.Domain
{
    public class TurningPageManager
    {
        private int currentTurningPage = 0;

        public int CurrentTurningPage
        {
            get => currentTurningPage;
            private set
            {
                var oldValue = currentTurningPage;

                currentTurningPage = Math.Clamp(value, 0, TotalPageCount - 1);
                
                if (currentTurningPage != oldValue)
                    CurrentPageChanged?.Invoke(currentTurningPage);
            }
        }

        public int TotalPageCount { get; }

        public bool HasNextPage => CurrentTurningPage < TotalPageCount - 1;
        public bool HasPreviousPage => CurrentTurningPage > 0;

        public TurningPageManager(int totalPageCount)
        {
            totalPageCount = Math.Max(totalPageCount, 1);

            TotalPageCount = totalPageCount;
        }

        public event Action<int>? CurrentPageChanged;

        public bool TryTurningNextPage()
        {
            if (!HasNextPage)
                return false;

            ++CurrentTurningPage;
            return true;
        }

        public bool TryTurningPreviousPage()
        {
            if (!HasPreviousPage)
                return false;

            --CurrentTurningPage;
            return true;
        }
    }
}