using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NzbDrone.Core.Jobs.Durable;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Books.Commands
{
    public class RefreshCommandSubmitter : IRefreshCommandSubmitter
    {
        private readonly IDurableJobScheduler _durableJobScheduler;

        public RefreshCommandSubmitter(IDurableJobScheduler durableJobScheduler)
        {
            _durableJobScheduler = durableJobScheduler;
        }

        public JobAttempt Submit(RefreshAuthorCommand command,
                                 CommandPriority priority = CommandPriority.Normal,
                                 CommandTrigger trigger = CommandTrigger.Unspecified)
        {
            return _durableJobScheduler.Submit(command, GetRefreshAuthorKey(command), priority, trigger);
        }

        public JobAttempt Submit(BulkRefreshAuthorCommand command,
                                 CommandPriority priority = CommandPriority.Normal,
                                 CommandTrigger trigger = CommandTrigger.Unspecified)
        {
            return _durableJobScheduler.Submit(command, GetBulkRefreshAuthorKey(command), priority, trigger);
        }

        public JobAttempt Submit(RefreshBookCommand command,
                                 CommandPriority priority = CommandPriority.Normal,
                                 CommandTrigger trigger = CommandTrigger.Unspecified)
        {
            return _durableJobScheduler.Submit(command, GetRefreshBookKey(command), priority, trigger);
        }

        public JobAttempt Submit(BulkRefreshBookCommand command,
                                 CommandPriority priority = CommandPriority.Normal,
                                 CommandTrigger trigger = CommandTrigger.Unspecified)
        {
            return _durableJobScheduler.Submit(command, GetBulkRefreshBookKey(command), priority, trigger);
        }

        private static string GetRefreshAuthorKey(RefreshAuthorCommand command)
        {
            return $"refresh-author:{command.AuthorId?.ToString() ?? "all"}:{command.IsNewAuthor}";
        }

        private static string GetBulkRefreshAuthorKey(BulkRefreshAuthorCommand command)
        {
            return $"bulk-refresh-author:{NormalizeIds(command.AuthorIds)}:{command.AreNewAuthors}";
        }

        private static string GetRefreshBookKey(RefreshBookCommand command)
        {
            return $"refresh-book:{command.BookId?.ToString() ?? "all"}";
        }

        private static string GetBulkRefreshBookKey(BulkRefreshBookCommand command)
        {
            return $"bulk-refresh-book:{NormalizeIds(command.BookIds)}";
        }

        private static string NormalizeIds(List<int> ids)
        {
            ids ??= new List<int>();

            var normalizedIds = ids.Distinct().OrderBy(x => x).ToList();
            var normalized = string.Join(",", normalizedIds);
            if (normalized.Length <= 120)
            {
                return normalized;
            }

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
            return $"{normalizedIds.Count}:{Convert.ToHexString(bytes)}";
        }
    }
}
