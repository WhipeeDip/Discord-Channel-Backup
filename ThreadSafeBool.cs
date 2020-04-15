using System.Threading;

namespace Discord_Channel_Backup
{
    /// <summary>
    /// Thread safe boolean class.
    /// </summary>
    public class ThreadSafeBool
    {
        private const int TRUE = 1;
        private const int FALSE = 0;
        private int _intBoolean = FALSE;

        /// <summary>
        /// Setter/getter for the boolean value.
        /// </summary>
        public bool Value
        {
            get
            {
                return Interlocked.CompareExchange(ref _intBoolean, TRUE, TRUE) == TRUE;
            }

            set
            {
                if (value)
                {
                    Interlocked.CompareExchange(ref _intBoolean, TRUE, FALSE);
                }
                else
                {
                    Interlocked.CompareExchange(ref _intBoolean, FALSE, TRUE);
                }
            }
        }
    }
}
