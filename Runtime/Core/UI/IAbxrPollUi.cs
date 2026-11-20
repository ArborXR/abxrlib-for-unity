using System;
using System.Collections.Generic;
using AbxrLib.Runtime.Types;

namespace AbxrLib.Runtime.Core.UI
{
    public interface IAbxrPollUi
    {
        /// <summary>
        /// Queues a poll to show the user. Polls are shown one at a time in the order they are added.
        /// </summary>
        void AddPoll(string prompt, PollType pollType, List<string> responses, Action<string> callback);
    }
}
