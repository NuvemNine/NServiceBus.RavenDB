namespace NServiceBus.TimeoutPersisters.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Timeout.Core;
    using Raven.Client;
    using Raven.Client.Linq;

    class QueryTimeouts : IQueryTimeouts
    {
        public QueryTimeouts(IDocumentStore documentStore, string endpointName)
        {
            this.documentStore = documentStore;
            this.endpointName = endpointName;
            TriggerCleanupEvery = TimeSpan.FromMinutes(2);
            CleanupGapFromTimeslice = TimeSpan.FromMinutes(1);
            shutdownTokenSource = new CancellationTokenSource();
        }

        public TimeSpan CleanupGapFromTimeslice { get; set; }
        public TimeSpan TriggerCleanupEvery { get; set; }

        public async Task<TimeoutsChunk> GetNextChunk(DateTime startSlice)
        {
            var now = DateTime.UtcNow;
            List<TimeoutsChunk.Timeout> results;

            // Allow for occasionally cleaning up old timeouts for edge cases where timeouts have been
            // added after startSlice have been set to a later timout and we might have missed them
            // because of stale indexes.
            if (lastCleanupTime == DateTime.MinValue || lastCleanupTime.Add(TriggerCleanupEvery) < now)
            {
                results = (await GetCleanupChunk(startSlice).ConfigureAwait(false)).ToList();
            }
            else
            {
                results = new List<TimeoutsChunk.Timeout>();
            }

            // default return value for when no results are found
            var nextTimeToRunQuery = DateTime.UtcNow.AddMinutes(10);

            RavenQueryStatistics statistics;

            using (var session = documentStore.OpenAsyncSession())
            {
                int totalCount, skipCount = 0;

                do
                {
                    if (CancellationRequested())
                    {
                        return new TimeoutsChunk(Enumerable.Empty<TimeoutsChunk.Timeout>(), nextTimeToRunQuery);
                    }

                    var query = GetChunkQuery(session);

                    var dueTimeouts = await
                        query.Statistics(out statistics)
                            .Where(t => t.Time >= startSlice)
                            .Where(t => t.Time <= now)
                            .Skip(skipCount)
                            .Select(t => new
                            {
                                t.Id,
                                t.Time
                            })
                            .Take(maximumPageSize)
                            .ToListAsync()
                            .ConfigureAwait(false);

                    foreach (var dueTimeout in dueTimeouts)
                    {
                        results.Add(new TimeoutsChunk.Timeout(dueTimeout.Id, dueTimeout.Time));
                    }

                    if (CancellationRequested())
                    {
                        return new TimeoutsChunk(Enumerable.Empty<TimeoutsChunk.Timeout>(), nextTimeToRunQuery);
                    }

                    skipCount = results.Count + statistics.SkippedResults;
                    totalCount = statistics.TotalResults;
                }
                while (results.Count < totalCount);

                var nextDueTimeout = await
                    GetChunkQuery(session)
                        .Where(t => t.Time > now)
                        .Select(t => t.Time)
                        .FirstOrDefaultAsync()
                        .ConfigureAwait(false);

                if (nextDueTimeout != default(DateTime))
                {
                    nextTimeToRunQuery = nextDueTimeout;
                }
            }

            if (statistics.IsStale && results.Count == 0)
            {
                nextTimeToRunQuery = now;
            }

            return new TimeoutsChunk(results, nextTimeToRunQuery);
        }

        public async Task<IEnumerable<TimeoutsChunk.Timeout>> GetCleanupChunk(DateTime startSlice)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var query = await GetChunkQuery(session)
                    .Where(t => t.Time <= startSlice.Subtract(CleanupGapFromTimeslice))
                    .Select(t => new
                    {
                        t.Id,
                        t.Time
                    })
                    .Take(maximumPageSize)
                    .ToListAsync()
                    .ConfigureAwait(false);

                var chunk = query.Select(arg => new TimeoutsChunk.Timeout(arg.Id, arg.Time));

                lastCleanupTime = DateTime.UtcNow;

                return chunk;
            }
        }

        public void Shutdown()
        {
            shutdownTokenSource.Cancel();
        }

        IRavenQueryable<TimeoutData> GetChunkQuery(IAsyncDocumentSession session)
        {
            session.Advanced.AllowNonAuthoritativeInformation = true;
            return session.Query<TimeoutData, TimeoutsIndex>()
                .OrderBy(t => t.Time)
                .Where(
                    t =>
                        t.OwningTimeoutManager == string.Empty ||
                        t.OwningTimeoutManager == endpointName);
        }

        bool CancellationRequested()
        {
            return shutdownTokenSource != null && shutdownTokenSource.IsCancellationRequested;
        }

        string endpointName;
        DateTime lastCleanupTime = DateTime.MinValue;
        IDocumentStore documentStore;
		
        /// <summary>
        /// RavenDB server default maximum page size 
        /// </summary>
        private int maximumPageSize = 1024;
        CancellationTokenSource shutdownTokenSource;
    }
}